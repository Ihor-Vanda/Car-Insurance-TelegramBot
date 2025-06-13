using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace CarInsuranceTelegramBot.BotSettings;

public static class BotKeyboards
{
    public static InlineKeyboardMarkup PassportConfirmationKeyboard => new(
        [
            [
                InlineKeyboardButton.WithCallbackData("✅ Confirm", "confirmPassport"),
                InlineKeyboardButton.WithCallbackData("🔄 Try again with photo", "retryPassport")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✍️ Enter manual again", "manualPassport")
            ]
        ]);

    public static InlineKeyboardMarkup PassportExtractionFailedKeyboard => new(
        [
            [
                InlineKeyboardButton.WithCallbackData("🔄 Try again", "retryPassport")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✍️ Enter manually", "manualPassport")
            ]
        ]);

    public static InlineKeyboardMarkup VehicleFrontConfirmationKeyboard => new(
        [
            [
                InlineKeyboardButton.WithCallbackData("✅ Confirm", "confirmVehicleFront"),
                InlineKeyboardButton.WithCallbackData("🔄 Try again with photo", "retryVehicleFront")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✍️ Enter manual again", "manualVehicle")
            ]
        ]);

    public static InlineKeyboardMarkup VehicleFrontExtractionFailedKeyboard => new(
        [
            [
                InlineKeyboardButton.WithCallbackData("🔄 Try again", "retryVehicleFront")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✍️ Enter manually", "manualVehicle")
            ]
        ]);

    public static InlineKeyboardMarkup VehicleBackConfirmationKeyboard => new(
        [
            [
                InlineKeyboardButton.WithCallbackData("✅ Confirm", "confirmVehicleBack"),
                InlineKeyboardButton.WithCallbackData("🔄 Try again with photo", "retryVehicleBack")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✍️ Enter manual again", "manualVehicle")
            ]
        ]);

    public static InlineKeyboardMarkup VehicleBackExtractionFailedKeyboard => new(
        [
            [
                InlineKeyboardButton.WithCallbackData("🔄 Try again", "retryVehicleBack")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✍️ Enter manually", "manualVehicle")
            ]
        ]);

    public static InlineKeyboardMarkup VehicleDocConfirmationKeyboard => new(
        [
            [
                InlineKeyboardButton.WithCallbackData("✅ Confirm", "confirmVehicleDoc"),
                InlineKeyboardButton.WithCallbackData("🔄 Try again with photo", "retryVehicleFront")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✍️ Enter manual again", "manualVehicle")
            ]
        ]);

    public static InlineKeyboardMarkup PriceConfirmationKeyboard => new(
        [
            [
                InlineKeyboardButton.WithCallbackData("✅ Accept", "agreePrice"),
                InlineKeyboardButton.WithCallbackData("❌ Decline", "declinePrice")
            ]
        ]);

    public static InlineKeyboardMarkup DeclinedPriceOfferKeyboard => new(
        [
            [
                InlineKeyboardButton.WithCallbackData("✅ Accept", "agreePrice"),
                InlineKeyboardButton.WithCallbackData("🔄 Start over", "restart")
            ]
        ]);
}