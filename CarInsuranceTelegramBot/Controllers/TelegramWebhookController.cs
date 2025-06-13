using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types;
using Serilog;
using Telegram.Bot.Types.Enums;

namespace CarInsuranceTelegramBot.Controllers
{
    [ApiController]
    [Route("api/telegram/webhook")]
    public class TelegramWebhookController : ControllerBase
    {
        private readonly ITelegramBotClient _botClient;
        private readonly BotUpdateHandler _updateHandler;
        private readonly ILogger<TelegramWebhookController> _logger;

        public TelegramWebhookController(ITelegramBotClient botClient, BotUpdateHandler updateHandler, ILogger<TelegramWebhookController> logger)
        {
            _botClient = botClient;
            _updateHandler = updateHandler;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Update update, CancellationToken cancellationToken)
        {
            if (update == null)
            {
                _logger.LogWarning("Received null update from Telegram webhook.");
                return BadRequest();
            }

            _logger.LogInformation("Received Telegram update of type: {UpdateType}", update.Type);

            try
            {
                await _updateHandler.HandleUpdateAsync(update, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Telegram update.");
                await _updateHandler.HandleErrorAsync(ex, cancellationToken);
            }

            return Ok();
        }

        [HttpGet("set")]
        public async Task<IActionResult> SetWebhook([FromQuery] string hostUrl)
        {
            if (string.IsNullOrEmpty(hostUrl))
            {
                return BadRequest("hostUrl parameter is required.");
            }

            var webhookUrl = $"{hostUrl}/api/telegram/webhook";

            try
            {


                await _botClient.SetWebhook(
                    url: webhookUrl,
                    allowedUpdates: [UpdateType.Message, UpdateType.CallbackQuery],
                    dropPendingUpdates: true,
                    cancellationToken: default
                );

                _logger.LogInformation("Telegram webhook set to: {WebhookUrl}", webhookUrl);
                return Ok($"Webhook set to: {webhookUrl}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set Telegram webhook.");
                return StatusCode(500, $"Failed to set webhook: {ex.Message}");
            }
        }

        [HttpGet("remove")]
        public async Task<IActionResult> RemoveWebhook()
        {
            try
            {
                await _botClient.DeleteWebhook(cancellationToken: default);
                _logger.LogInformation("Telegram webhook successfully removed.");
                return Ok("Webhook removed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove Telegram webhook.");
                return StatusCode(500, $"Failed to remove webhook: {ex.Message}");
            }
        }
    }
}