using System.Text.Json.Serialization;

namespace CarInsuranceTelegramBot.Models.VehicleDocument.BackSide;
public class InferenceContainer
{
    [JsonPropertyName("prediction")]
    public Prediction Prediction { get; set; } = default!;
}