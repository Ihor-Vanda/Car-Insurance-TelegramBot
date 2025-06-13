namespace CarInsuranceTelegramBot.Models;

public class UserSession
{
    public long ChatId { get; init; }
    public ConversationState State { get; set; } = ConversationState.None;
    public PassportData? PassportData { get; set; }
    public int ManualEntryStepPassportData { get; set; }
    public VehicleData? VehicleData { get; set; }
    public int ManualEntryStepVehicleData { get; set; }

}
