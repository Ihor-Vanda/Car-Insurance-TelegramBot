using System.Text.Json.Serialization;

namespace CarInsuranseTelegramBot.Models;

public class InferenceRoot
{
    [JsonPropertyName("prediction")]
    public PredictionData Prediction { get; set; } = null!;
}
