using System.Text.Json.Serialization;

namespace CarInsuranceTelegramBot.Models;

public class PredictionField<T>
{
    [JsonPropertyName("value")]
    public T? Value { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}