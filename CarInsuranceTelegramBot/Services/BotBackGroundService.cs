using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace CarInsuranceTelegramBot.Services;

public class BotBackGroundService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly IServiceScopeFactory _scopeFactory;

    public BotBackGroundService(ITelegramBotClient bot, IServiceScopeFactory scopeFactory)
    {
        _bot = bot;
        _scopeFactory = scopeFactory;
    }

    protected override Task ExecuteAsync(CancellationToken ct)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [
                UpdateType.Message,
                UpdateType.CallbackQuery
            ]
        };

        _bot.StartReceiving(
            updateHandler: async (botClient, update, ct) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<BotUpdateHandler>();
                await handler.HandleUpdateAsync(update, ct);
            },

            errorHandler: (botClient, exception, ct) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<BotUpdateHandler>();
                handler.HandleErrorAsync(exception, ct);
            },

            receiverOptions: receiverOptions,
            cancellationToken: ct
        );

        return Task.CompletedTask;
    }
}