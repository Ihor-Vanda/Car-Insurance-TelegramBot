using System.Text.Json.Serialization;

namespace CarInsuranceTelegramBot.Models;

public class DocumentInfo
{
    [JsonPropertyName("inference")]
    public InferenceRoot Inference { get; set; } = null!;
}