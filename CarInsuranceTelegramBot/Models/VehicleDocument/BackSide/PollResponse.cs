using System.Text.Json.Serialization;

namespace CarInsuranceTelegramBot.Models.VehicleDocument.BackSide;

public class PollResponse
{
    [JsonPropertyName("document")]
    public DocumentContainer Document { get; set; } = default!;
}