using System.Text.Json.Serialization;

namespace CarInsuranceTelegramBot.Models;

public class PassportApiResponse
{
    [JsonPropertyName("document")]
    public DocumentInfo Document { get; set; } = null!;
}
