using System.Text.Json.Serialization;

namespace CarInsuranseTelegramBot.Models;

public class PredictionData
{
    [JsonPropertyName("surname")]
    public PredictionField<string> Surname { get; set; } = null!;

    [JsonPropertyName("given_names")]
    public PredictionField<string>[] GivenNames { get; set; } = Array.Empty<PredictionField<string>>();

    [JsonPropertyName("id_number")]
    public PredictionField<string> PassportNumber { get; set; } = null!;

    [JsonPropertyName("issuance_date")]
    public PredictionField<string> IssueDate { get; set; } = null!;

    [JsonPropertyName("expiry_date")]
    public PredictionField<string> ExpiryDate { get; set; } = null!;

    [JsonPropertyName("birth_date")]
    public PredictionField<string> DateOfBirth { get; set; } = null!;
}
