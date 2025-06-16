using System.Net.Http.Headers;
using System.Text.Json;
using CarInsuranceTelegramBot.Models;
using Microsoft.Extensions.Options;
using Mindee;
using Mindee.Http;
using Mindee.Input;
using Mindee.Parsing.Generated;
using Mindee.Product.Generated;

namespace CarInsuranceTelegramBot.Services;

public class MindeeService : IReadingDocumentService
{
    private readonly HttpClient _http;
    private readonly ILogger<MindeeService> _logger;
    private readonly MindeeSettings _settings;
    private readonly string _apiKey;

    public MindeeService(
        HttpClient http,
        ILogger<MindeeService> logger,
        IOptions<MindeeSettings> options)
    {
        _http = http;
        _logger = logger;
        _settings = options.Value;
        _apiKey = Environment.GetEnvironmentVariable("MINDEE_API_KEY")
                   ?? throw new InvalidOperationException("MINDEE_API_KEY is not set");

        _logger.LogInformation(
            "MindeeService initialized (Account={Account}, Version={Version})",
            _settings.AccountName, _settings.Version);
    }

    public async Task<PassportData> ExtractPassportDataAsync(
        Stream imageStream,
        CancellationToken ct)
    {
        _logger.LogInformation("ExtractPassportDataAsync start");

        using var content = new MultipartFormDataContent
        {
            {
                new StreamContent(imageStream)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") }
                },
                "document", "passport.jpg"
            }
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(
                "/v1/products/mindee/passport/v1/predict",
                content,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP request to Passport API failed");
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Passport API error: status {StatusCode}",
                response.StatusCode);
            throw new HttpRequestException($"Status code {response.StatusCode}");
        }

        string json;
        try
        {
            json = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reading Passport API response content failed");
            throw;
        }

        _logger.LogInformation("Passport API responded, deserializing");
        PassportData passport;
        try
        {
            passport = ExtractPassportData(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deserializing passport data failed");
            throw;
        }

        _logger.LogInformation("ExtractPassportDataAsync success: {@Passport}", passport);
        return passport;
    }

    public async Task<VehicleData> ExtractVehicleDocumentAsync(IReadOnlyList<Stream> pages, string countryCode, CancellationToken ct)
    {
        var cfg = _settings.GetForCountry(countryCode);
        bool expectsBack = cfg.HasBackPage;

        if (pages.Count < 1 || pages.Count > 2)
            throw new ArgumentException(
                $"Expected 1 {(expectsBack ? "or 2" : "")} page(s), but got {pages.Count}");

        var frontFields = await EnqueueAndParseAsync(
            pages[0],
            cfg.EndpointNameFront,
            ct);

        var reg = ParseString(frontFields, "number");
        var yr = int.Parse(ParseString(frontFields, "year"));

        string? vin = null, make = null, model = null;

        if (expectsBack)
        {
            if (pages.Count < 2)
                throw new InvalidOperationException(
                    "Document for this country requires back page, but none provided.");

            var backFields = await EnqueueAndParseAsync(
                pages[1],
                cfg.EndpointNameBack,
                ct);

            vin = ParseStringOrNull(backFields, "vin");
            make = ParseStringOrNull(backFields, "make");
            model = ParseStringOrNull(backFields, "model");
        }

        if (!expectsBack)
        {
            vin = TryGet(frontFields, "vin");
            make = TryGet(frontFields, "make");
            model = TryGet(frontFields, "model");
        }

        return new VehicleData
        {
            RegistrationNumber = reg,
            Year = yr,
            VIN = vin ?? throw new InvalidOperationException("VIN missing"),
            Make = make ?? throw new InvalidOperationException("Make missing"),
            Model = model ?? throw new InvalidOperationException("Model missing")
        };
    }

    private static string ParseString(IReadOnlyDictionary<string, GeneratedFeature> fields, string key)
    {
        if (!fields.TryGetValue(key, out var f))
            throw new InvalidOperationException($"Missing field: {key}");
        var v = ExtractStringOrList(f, key);
        if (string.IsNullOrEmpty(v))
            throw new InvalidOperationException($"Field {key} is empty");
        return v;
    }
    private static string? ParseStringOrNull(IReadOnlyDictionary<string, GeneratedFeature> fields, string key)
    {
        if (!fields.TryGetValue(key, out var f))
            return null;
        return ExtractStringOrList(f, key);
    }
    private static string? TryGet(IReadOnlyDictionary<string, GeneratedFeature> fields, string key) =>
        fields.TryGetValue(key, out var f)
            ? f.AsStringField().Value?.Trim()
            : null;

    private async Task<IReadOnlyDictionary<string, GeneratedFeature>> EnqueueAndParseAsync(Stream imageStream, string endpointName, CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
        _logger.LogInformation(
            "Saving image to temp file {TempPath} for endpoint {Endpoint}",
            tempPath, endpointName);

        try
        {
            await using var fs = File.Create(tempPath);
            await imageStream.CopyToAsync(fs, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Writing image to temp file failed");
            throw;
        }

        try
        {
            var client = new MindeeClient(_apiKey);
            var input = new LocalInputSource(tempPath);
            var endpoint = new CustomEndpoint(
                endpointName: endpointName,
                accountName: _settings.AccountName!,
                version: _settings.Version!);

            _logger.LogInformation("Sending to Mindee: endpoint {Endpoint}", endpointName);
            var resp = await client
                .EnqueueAndParseAsync<GeneratedV1>(input, endpoint);
            _logger.LogInformation("Received Mindee response for {Endpoint}", endpointName);

            return resp.Document.Inference.Prediction.Fields;
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
                _logger.LogInformation("Deleted temp file {TempPath}", tempPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to delete temp file {TempPath}",
                    tempPath);
            }
        }
    }

    private PassportData ExtractPassportData(string json)
    {
        _logger.LogInformation("Deserializing passport JSON");
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        PassportApiResponse apiResult;
        try
        {
            apiResult = JsonSerializer.Deserialize<PassportApiResponse>(json, opts)
                        ?? throw new InvalidOperationException("Error deserializing passport response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deserialization of JSON to PassportApiResponse failed");
            throw;
        }

        var pred = apiResult.Document.Inference.Prediction;
        PassportData data;
        try
        {
            data = new PassportData
            {
                FullName = $"{pred.Surname.Value} {pred.GivenNames.First().Value}".Trim(),
                PassportNumber = pred.PassportNumber.Value!,
                DateOfBirth = DateTime.Parse(pred.DateOfBirth.Value!),
                IssueDate = DateTime.Parse(pred.IssueDate.Value!),
                ExpiryDate = DateTime.Parse(pred.ExpiryDate.Value!)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting fields from PassportApiResponse");
            throw;
        }

        _logger.LogInformation("Passport parsed: {@PassportData}", data);
        return data;
    }

    private static string ExtractStringOrList(GeneratedFeature feature, string fieldName)
    {
        try
        {
            var first = feature.First().AsStringField().Value?.Trim();
            if (!string.IsNullOrEmpty(first))
                return first;
        }
        catch { }

        try
        {
            var single = feature.AsStringField().Value?.Trim();
            if (!string.IsNullOrEmpty(single))
                return single;
        }
        catch { }

        throw new InvalidOperationException($"{fieldName} is empty");
    }
}
