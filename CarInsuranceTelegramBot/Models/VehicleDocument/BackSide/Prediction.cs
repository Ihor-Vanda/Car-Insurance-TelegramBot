using System.Text.Json.Serialization;

namespace CarInsuranceTelegramBot.Models.VehicleDocument.BackSide;

public class Prediction
{
    [JsonPropertyName("make")]
    public string Make { get; set; } = default!;

    [JsonPropertyName("type")]
    public string Type { get; set; } = default!;
    
    [JsonPropertyName("vin")]
    public string VIN { get; set; } = default!;
}