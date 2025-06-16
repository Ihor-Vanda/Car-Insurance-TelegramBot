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

    public async Task<(string RegistrationNumber, int Year)> ExtractVehicleFrontDataAsync(
        Stream imageStream,
        string countryCode,
        CancellationToken ct)
    {
        _logger.LogInformation("ExtractVehicleFrontDataAsync start for country {Country}", countryCode);

        CountryConfig cfg;
        try
        {
            cfg = _settings.GetForCountry(countryCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration for country {Country}", countryCode);
            throw;
        }

        if (string.IsNullOrWhiteSpace(cfg.EndpointNameFront))
        {
            _logger.LogError("No front endpoint configured for {Country}", countryCode);
            throw new InvalidOperationException($"No front endpoint configured for {countryCode}");
        }

        IReadOnlyDictionary<string, GeneratedFeature> fields;
        try
        {
            fields = await EnqueueAndParseAsync(imageStream, cfg.EndpointNameFront, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing front image for country {Country}", countryCode);
            throw;
        }

        if (!fields.TryGetValue("number", out var regFeature))
        {
            _logger.LogError("Missing field number for country {Country}", countryCode);
            throw new InvalidOperationException("Missing field: number");
        }

        string registrationNumber;
        try
        {
            registrationNumber = regFeature.AsStringField().Value?.Trim()
                ?? throw new InvalidOperationException("number is empty");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract number for country {Country}", countryCode);
            throw;
        }

        if (!fields.TryGetValue("year", out var yearFeature))
        {
            _logger.LogError("Missing field year for country {Country}", countryCode);
            throw new InvalidOperationException("Missing field: year");
        }

        int year;
        try
        {
            var yearStr = yearFeature.AsStringField().Value?.Trim()
                          ?? throw new InvalidOperationException("year is empty");
            if (!int.TryParse(yearStr, out year))
                throw new InvalidOperationException("year must be an integer");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract or parse manufacture year for country {Country}", countryCode);
            throw;
        }

        _logger.LogInformation(
            "ExtractVehicleFrontDataAsync success: RegistrationNumber={Reg}, Year={Year}",
            registrationNumber, year);

        return (registrationNumber, year);
    }

    public async Task<(string VIN, string Model, string Make)> ExtractVehicleBackDataAsync(
        Stream imageStream,
        string countryCode,
        CancellationToken ct)
    {
        _logger.LogInformation("ExtractVehicleBackDataAsync start for country {Country}", countryCode);

        CountryConfig cfg;
        try
        {
            cfg = _settings.GetForCountry(countryCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration for country {Country}", countryCode);
            throw;
        }

        if (!cfg.HasBackPage || string.IsNullOrWhiteSpace(cfg.EndpointNameBack))
        {
            _logger.LogError("No back endpoint configured for {Country}", countryCode);
            throw new InvalidOperationException($"No back endpoint configured for {countryCode}");
        }

        IReadOnlyDictionary<string, GeneratedFeature> fields;
        try
        {
            fields = await EnqueueAndParseAsync(imageStream, cfg.EndpointNameBack, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing back image for country {Country}", countryCode);
            throw;
        }

        if (!fields.TryGetValue("vin", out var vinFeature))
        {
            _logger.LogError("Missing field vin for country {Country}", countryCode);
            throw new InvalidOperationException("Missing field: vin");
        }
        string vin;
        try
        {
            vin = vinFeature.AsStringField().Value?.Trim()
                  ?? throw new InvalidOperationException("vin is empty");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract VIN for country {Country}", countryCode);
            throw;
        }

        string make;
        try
        {
            if (!fields.TryGetValue("make", out var makeFeature))
                throw new InvalidOperationException("Missing field: make");
            make = ExtractStringOrList(makeFeature, "make");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract Make for country {Country}", countryCode);
            throw;
        }

        string model;
        try
        {
            if (!fields.TryGetValue("model", out var modelFeature))
                throw new InvalidOperationException("Missing field: model");
            model = ExtractStringOrList(modelFeature, "model");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract Model for country {Country}", countryCode);
            throw;
        }

        _logger.LogInformation(
            "ExtractVehicleBackDataAsync success: VIN={VIN}, Model={Model}, Make={Make}",
            vin, model, make);

        return (vin, model, make);
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

    private async Task<IReadOnlyDictionary<string, GeneratedFeature>> EnqueueAndParseAsync(
        Stream imageStream,
        string endpointName,
        CancellationToken ct)
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
