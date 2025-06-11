using System.Text.Json.Serialization;

namespace CarInsuranseTelegramBot.Models;

public class DocumentInfo
{
    [JsonPropertyName("inference")]
    public InferenceRoot Inference { get; set; } = null!;
}