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
                InlineKeyboardButton.WithCallbackData("✍️ Enter manual", "manualPassport")
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

    public static InlineKeyboardMarkup VehicleExtractionFailedKeyboard => new(
        [
            [
                InlineKeyboardButton.WithCallbackData("🔄 Try again", "retryVehicleFront")
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
                InlineKeyboardButton.WithCallbackData("✍️ Enter manual", "manualVehicle")
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

    public static InlineKeyboardMarkup VehicleCountry => new(
        [
            [
                InlineKeyboardButton.WithCallbackData("🇺🇦 Ukraine", "vehCountry_UA"),
                InlineKeyboardButton.WithCallbackData("🇺🇸 US (Massachusetts)", "vehCountry_US")
            ]
        ]);
}