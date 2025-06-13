using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using CarInsuranceTelegramBot.Models;
using Mindee;
using Mindee.Http;
using Mindee.Input;
using Mindee.Product.Generated;


namespace CarInsuranceTelegramBot.Services;

public class MindeeService : IReadingDocumentService
{
    private readonly HttpClient _http;
    private readonly ILogger<MindeeService> _logger;
    private readonly string _apiKey = Environment.GetEnvironmentVariable("MINDEE_API_KEY")!;

    public MindeeService(HttpClient http, ILogger<MindeeService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<(string RegistrationNumber, int Year)> ExtractVehicleFrontDataAsync(
        Stream imageStream,
        CancellationToken ct)
    {

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
        await using (var fs = File.Create(tempPath))
        {
            await imageStream.CopyToAsync(fs, ct);
        }

        try
        {
            var mindeeClient = new MindeeClient(_apiKey);
            var inputSource = new LocalInputSource(tempPath);

            var endpoint = new CustomEndpoint(
                endpointName: "vehicle_doc_front",
                accountName: "Ihor17344",
                version: "1"
            );

            var response = await mindeeClient
                .EnqueueAndParseAsync<GeneratedV1>(inputSource, endpoint);

            var fields = response.Document.Inference.Prediction.Fields;

            if (!fields.TryGetValue("vehicle_registration_number", out var regFeature))
                throw new InvalidOperationException("There is no vehicle_registration_number");

            var regField = regFeature.AsStringField();
            var registrationNumber = regField.Value ??
                                     throw new InvalidOperationException("vehicle_registration_number is empty");

            if (!fields.TryGetValue("vehicle_manufacture_year", out var manufactureYearFeature))
                throw new InvalidOperationException("There is no vehicle_manufacture_year");

            var manufactureYearField = manufactureYearFeature.AsStringField();
            var manufactureYear = manufactureYearField.Value ??
                                  throw new InvalidOperationException("vehicle_manufacture_year is empty");

            if (!int.TryParse(manufactureYear, out var year)) throw new InvalidOperationException("vehicle_manufacture_year must be an integer");

            return (registrationNumber, year);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* silent */ }
        }
    }

    public async Task<(string VIN, string Model, string Make)> ExtractVehicleBackDataAsync(Stream imageStream, CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
        await using (var fs = File.Create(tempPath))
        {
            await imageStream.CopyToAsync(fs, ct);
        }

        try
        {
            var mindeeClient = new MindeeClient(_apiKey);
            var inputSource = new LocalInputSource(tempPath);

            var endpoint = new CustomEndpoint(
                endpointName: "vehicle_doc_back",
                accountName: "Ihor17344",
                version: "1"
            );

            var response = await mindeeClient
                .EnqueueAndParseAsync<GeneratedV1>(inputSource, endpoint);

            var fields = response.Document.Inference.Prediction.Fields;

            if (!fields.TryGetValue("vin", out var vinFeature))
                throw new InvalidOperationException("There is no vin");

            var vin = vinFeature
                         .AsStringField()
                         .Value
                         ?.Trim() ??
                     throw new InvalidOperationException("vin is empty");

            if (!fields.TryGetValue("make", out var makeFeature))
                throw new InvalidOperationException("There is no make");

            var make = makeFeature
                .First()                      // бо це список
                .AsStringField()
                .Value
                ?.Trim() ?? throw new InvalidOperationException("make is empty");

            if (!fields.TryGetValue("type", out var modelFeature))
                throw new InvalidOperationException("There is no type");

            var model = modelFeature
                .First()
                .AsStringField()
                .Value
                ?.Trim() ?? throw new InvalidOperationException("type is empty");

            return (vin, model, make);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* silent */ }
        }
    }

    public async Task<PassportData> ExtractPassportDataAsync(Stream imageStream, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent {
            {
                new StreamContent(imageStream) {
                    Headers = {ContentType = new MediaTypeHeaderValue("image/jpeg")}
                },
                "document",
                "passport.jpg"
            }
        };

        var response = await _http.PostAsync(
            "/v1/products/mindee/passport/v1/predict",
            content,
            ct
        );

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("Mindee responded with success status code");

            return ExtractPassportData(json);
        }
        else
        {
            _logger.LogWarning("Mindee responded with error");
            throw new HttpRequestException();
        }
    }

    private PassportData ExtractPassportData(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var apiResult = JsonSerializer.Deserialize<PassportApiResponse>(json, options)
                        ?? throw new InvalidOperationException("Error getting response from Mindee");

        var pred = apiResult.Document.Inference.Prediction;

        string surname = pred.Surname.Value ?? string.Empty;
        string givenName = pred.GivenNames.FirstOrDefault()?.Value ?? string.Empty;
        string fullName = $"{surname} {givenName}".Trim();
        string passportNo = pred.PassportNumber.Value ?? string.Empty;

        DateTime parseDate(string? s) =>
            DateTime.TryParse(s, out var dt) ? dt : DateTime.MinValue;

        var dateOfBirth = parseDate(pred.DateOfBirth.Value);
        var issueDate = parseDate(pred.IssueDate.Value);
        var expiryDate = parseDate(pred.ExpiryDate.Value);

        return new PassportData
        {
            FullName = fullName,
            PassportNumber = passportNo,
            DateOfBirth = dateOfBirth,
            IssueDate = issueDate,
            ExpiryDate = expiryDate
        };
    }
}