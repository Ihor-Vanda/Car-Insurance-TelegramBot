using CarInsuranseTelegramBot.Models;

namespace CarInsuranseTelegramBot.Services;

public interface IReadingDocumentService
{
    Task<PassportData> ExtractPassportDataAsync(Stream imageStream, CancellationToken ct);
    Task<VehicleData> ExtractVehicleDataAsync(Stream imageStream, CancellationToken ct);
}