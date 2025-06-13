using System.Text.Json.Serialization;

namespace CarInsuranceTelegramBot.Models.VehicleDocument.FrontSide;

public class EnqueueResponse
{
    [JsonPropertyName("job")]
    public JobInfo Job { get; set; } = default!;

    public class JobInfo
    {
        [JsonPropertyName("id")]
        public string JobId { get; set; } = default!;

        [JsonPropertyName("polling_url")]
        public string PollingUrl { get; set; } = default!;

        [JsonPropertyName("status")]
        public string Status { get; set; } = default!;
    }
}
