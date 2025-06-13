using CarInsuranceTelegramBot.Models;

namespace CarInsuranceTelegramBot.Services;

public interface IReadingDocumentService
{
    Task<PassportData> ExtractPassportDataAsync(Stream imageStream, CancellationToken ct);
    Task<(string RegistrationNumber, int Year)> ExtractVehicleFrontDataAsync(Stream imageStream, CancellationToken ct);
    Task<(string VIN, string Model, string Make)> ExtractVehicleBackDataAsync(Stream imageStream, CancellationToken ct);
}