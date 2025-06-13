using System.Text.Json.Serialization;

namespace CarInsuranceTelegramBot.Models.VehicleDocument.BackSide;

public class DocumentContainer
{ 
    [JsonPropertyName("status")] 
    public string Status { get; set; } = default!;

    [JsonPropertyName("inference")]
    public InferenceContainer Inference { get; set; } = default!;
}