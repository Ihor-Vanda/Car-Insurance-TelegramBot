using System.Text.Json.Serialization;

namespace CarInsuranceTelegramBot.Models.VehicleDocument.BackSide;

public class EnqueueResponse
{
    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = default!;
}