using System.Text.Json.Serialization;

namespace CarInsuranceTelegramBot.Models.VehicleDocument.FrontSide;

public class VehicleFrontResponse
{
    [JsonPropertyName("document")]
    public DocumentInfo Document { get; set; } = default!;

    [JsonPropertyName("job")]
    public JobInfo Job { get; set; } = default!;
}

public class DocumentInfo
{
    [JsonPropertyName("inference")]
    public InferenceInfo Inference { get; set; } = default!;
}

public class InferenceInfo
{
    [JsonPropertyName("prediction")]
    public PredictionInfo Prediction { get; set; } = default!;
}

public class PredictionInfo
{
    [JsonPropertyName("vehicle_registration_number")]
    public ValueContainer VehicleRegistrationNumber { get; set; } = default!;

    [JsonPropertyName("vehicle_manufacture_year")]
    public ValueContainer VehicleManufactureYear { get; set; } = default!;
}

public class ValueContainer
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = default!;
}

public class JobInfo
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = default!;
}