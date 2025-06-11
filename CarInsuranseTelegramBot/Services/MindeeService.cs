using System.Net.Http.Headers;
using System.Text.Json;
using CarInsuranseTelegramBot.Models;

namespace CarInsuranseTelegramBot.Services;

public class MindeeService : IReadingDocumentService
{
    private readonly HttpClient _http;
    private readonly ILogger _logger;

    public MindeeService(HttpClient http, ILogger logger)
    {
        _http = http;
        _logger = logger;
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
            _logger.LogInformation("Mindee responsed with success status code");

            return ExtractPassportData(json);
        }
        else
        {
            _logger.LogWarning("Mindee responsed with error");
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

    public async Task<VehicleData> ExtractVehicleDataAsync(Stream imageStream, CancellationToken ct)
    {
        //
        throw new HttpRequestException();

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
            "/v1/products/mindee/",
            content,
            ct
        );

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("Mindee responsed with success status code");

            return ExtractVehicleData(json);
        }
        else
        {
            _logger.LogWarning("Mindee responsed with error");
            throw new HttpRequestException();
        }
    }

    private VehicleData ExtractVehicleData(string json)
    {
        return new VehicleData
        {
            VIN = "",
            Make = "",
            Model = "",
            Year = 0,
            RegistrationNumber = ""
        };
    }
}