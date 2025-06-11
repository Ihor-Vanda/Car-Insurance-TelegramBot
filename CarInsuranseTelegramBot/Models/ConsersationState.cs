namespace CarInsuranseTelegramBot.Models;

public enum ConversationState
{
    None,
    AwaitingPassport,
    AwaitingVehicleDoc,
    ConfirmingPassport,
    ConfirmingVehicleDoc,
    AwaitingPriceConfirmation,
    GeneratingPolicy,
    Completed
}