using System.Text.Json.Serialization;

namespace CarInsuranseTelegramBot.Models;

public class PassportApiResponse
{
    [JsonPropertyName("document")]
    public DocumentInfo Document { get; set; } = null!;
}
