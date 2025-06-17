using CarInsuranceTelegramBot.Models;

namespace CarInsuranceTelegramBot.Services;

public interface IReadingDocumentService
{
    Task<PassportData> ExtractPassportDataAsync(Stream imageStream, CancellationToken ct);
    Task<VehicleData> ExtractVehicleDocumentAsync(IReadOnlyList<Stream> imagePages, string countryCode, CancellationToken ct);
}