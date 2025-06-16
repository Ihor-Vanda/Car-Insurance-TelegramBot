namespace CarInsuranceTelegramBot.Models;

public enum ConversationState
{
    None,
    AwaitingPassport,
    EnteringPassportData,
    AwaAwaitingVehicleCountry,
    AwaitingVehicleFront,
    ConfirmingVehicleDocFront,
    AwaitingVehicleBack,
    ConfirmingVehicleDocBack,
    EnteringVehicleData,
    ConformingVehicleDoc,
    ConfirmingPassport,
    AwaitingPriceConfirmation,
    GeneratingPolicy,
    Completed
}