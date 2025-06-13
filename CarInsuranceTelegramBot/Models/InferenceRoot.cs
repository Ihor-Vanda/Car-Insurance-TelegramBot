using System.Text.Json.Serialization;

namespace CarInsuranceTelegramBot.Models;

public class InferenceRoot
{
    [JsonPropertyName("prediction")]
    public PredictionData Prediction { get; set; } = null!;
}
