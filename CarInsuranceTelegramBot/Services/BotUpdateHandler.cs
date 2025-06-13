using System.Globalization;
using CarInsuranceTelegramBot.Models;
using CarInsuranceTelegramBot.Repository;
using CarInsuranceTelegramBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using CarInsuranceTelegramBot.BotSettings;

namespace CarInsuranceTelegramBot;

public class BotUpdateHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly IUserSessionRepository _sessionRepository;
    private readonly IReadingDocumentService _readingDocumentService;
    private readonly ILogger<BotUpdateHandler> _logger;

    public BotUpdateHandler(
        ITelegramBotClient botClient,
        IUserSessionRepository sessionRepository,
        IReadingDocumentService readingDocumentService,
        ILogger<BotUpdateHandler> logger)
    {
        _botClient = botClient;
        _sessionRepository = sessionRepository;
        _readingDocumentService = readingDocumentService;
        _logger = logger;
    }


    // Handles incoming Telegram update
    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        long chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id ?? 0;
        if (chatId == 0)
        {
            _logger.LogError("Chat ID could not be determined for update.");
            return;
        }

        try
        {
            if (IsStartCommand(update.Message?.Text!))
            {
                await HandleStartCommandAsync(chatId, cancellationToken);
                return;
            }

            var session = await GetOrCreateSessionAsync(chatId, cancellationToken);

            if (update.Type == UpdateType.Message && update.Message != null)
            {
                await HandleMessageUpdateAsync(update.Message, session, cancellationToken);
            }
            else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                await _botClient.AnswerCallbackQuery(update.CallbackQuery.Id, cancellationToken: cancellationToken); // Answer the callback query
                await HandleCallbackQueryUpdateAsync(update.CallbackQuery, session, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Unhandled update type: {type} in main handler.", update.Type);
                await SendUnrecognizedMessageResponseAsync(chatId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while processing update for chat {chatId}", chatId);
            await SendErrorMessageAsync(chatId, cancellationToken);
        }
    }

    // Handles message updates based on current conversation state and content
    private async Task HandleMessageUpdateAsync(Message message, UserSession session, CancellationToken cancellationToken)
    {
        long chatId = message.Chat.Id;

        switch (session.State)
        {
            case ConversationState.AwaitingPassport:
                if (message.Photo is { Length: > 0 })
                {
                    await HandlePassportPhotoAsync(chatId, message, session, cancellationToken);
                }
                else
                {
                    await SendUnrecognizedMessageResponseAsync(chatId, cancellationToken);
                }
                break;

            case ConversationState.EnteringPassportData:
                if (!string.IsNullOrEmpty(message.Text))
                {
                    await HandleManualPassportTextAsync(chatId, message.Text, session, cancellationToken);
                }
                else
                {
                    await SendUnrecognizedMessageResponseAsync(chatId, cancellationToken);
                }
                break;

            case ConversationState.AwaitingVehicleFront:
                if (message.Photo is { Length: > 0 })
                {
                    await HandleVehicleFrontPhotoAsync(chatId, message, session, cancellationToken);
                }
                else
                {
                    await SendUnrecognizedMessageResponseAsync(chatId, cancellationToken);
                }
                break;

            case ConversationState.AwaitingVehicleBack:
                if (message.Photo is { Length: > 0 })
                {
                    await HandleVehicleBackPhotoAsync(chatId, message, session, cancellationToken);
                }
                else
                {
                    await SendUnrecognizedMessageResponseAsync(chatId, cancellationToken);
                }
                break;

            case ConversationState.EnteringVehicleData:
                if (!string.IsNullOrEmpty(message.Text))
                {
                    await HandleManualVehicleTextAsync(chatId, message.Text, session, cancellationToken);
                }
                else
                {
                    await SendUnrecognizedMessageResponseAsync(chatId, cancellationToken);
                }
                break;

            case ConversationState.AwaitingPriceConfirmation:
                await RequestPriceConfirmationAsync(chatId, cancellationToken);
                break;

            case ConversationState.Completed:
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: BotMessages.PolicyAlreadyIssued,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );
                break;

            default:
                await SendUnrecognizedMessageResponseAsync(chatId, cancellationToken);
                break;
        }
    }

    // Handles callback query updates based on current conversation state and callback data
    private async Task HandleCallbackQueryUpdateAsync(CallbackQuery callbackQuery, UserSession session, CancellationToken cancellationToken)
    {
        long chatId = callbackQuery.Message!.Chat.Id;

        switch (session.State)
        {
            case ConversationState.ConfirmingPassport:
                await HandlePassportConfirmationCallbacks(callbackQuery.Data!, chatId, session, cancellationToken);
                break;

            case ConversationState.ConfirmingVehicleDocFront:
            case ConversationState.ConfirmingVehicleDocBack:
            case ConversationState.ConformingVehicleDoc:
                await HandleVehicleConfirmationCallbacks(callbackQuery.Data!, chatId, session, cancellationToken);
                break;

            case ConversationState.AwaitingPriceConfirmation:
                await HandlePriceConfirmationCallbacks(callbackQuery.Data!, chatId, session, cancellationToken);
                break;

            case ConversationState.Completed:
                await HandleCompletedStateCallbacks(callbackQuery.Data!, chatId, cancellationToken);
                break;

            default:
                await SendInvalidCallbackResponseAsync(chatId, cancellationToken);
                break;
        }
    }


    #region Specific Handling Logic

    // Processes manual text input for passport data.
    private async Task HandleManualPassportTextAsync(
        long chatId,
        string text,
        UserSession session,
        CancellationToken cancellationToken)
    {
        // Expected Input Format: «FullName;PassportNumber;DateOfBirth;IssueDate;ExpiryDate»
        var parts = text.Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length != 5)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.PassportDataFormatError,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
            return;
        }

        try
        {
            var model = new PassportData
            {
                FullName = parts[0],
                PassportNumber = parts[1],
                DateOfBirth = DateTime.ParseExact(parts[2], "dd.MM.yyyy", CultureInfo.InvariantCulture),
                IssueDate = DateTime.ParseExact(parts[3], "dd.MM.yyyy", CultureInfo.InvariantCulture),
                ExpiryDate = DateTime.ParseExact(parts[4], "dd.MM.yyyy", CultureInfo.InvariantCulture)
            };

            session.PassportData = model;
            session.State = ConversationState.ConfirmingPassport;
            await _sessionRepository.UpdateAsync(session, cancellationToken);

            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.PassportDataConfirmationPrompt(model),
                replyMarkup: BotKeyboards.PassportConfirmationKeyboard,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }
        catch (FormatException)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.PassportDateParseError,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }
    }

    // Processes passport photo and extracts information.
    private async Task HandlePassportPhotoAsync(long chatId, Message message, UserSession session, CancellationToken cancellationToken)
    {
        if (message?.Photo == null || message.Photo.Length == 0)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.PhotoProcessingFailed,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken
            );
            return;
        }

        var fileId = message.Photo.Last().FileId;

        using var memoryStream = new MemoryStream();
        await _botClient.GetInfoAndDownloadFile(
            fileId,
            memoryStream,
            cancellationToken: cancellationToken
        );

        memoryStream.Position = 0;

        session.State = ConversationState.ConfirmingPassport;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        PassportData result;

        try
        {
            result = await _readingDocumentService.ExtractPassportDataAsync(memoryStream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Passport data extraction failed for chat {chatId}", chatId);
            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.PassportExtractionFailed,
                replyMarkup: BotKeyboards.PassportExtractionFailedKeyboard,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken
            );
            return;
        }

        session.PassportData = result;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        await _botClient.SendMessage(
            chatId: chatId,
            text: BotMessages.PassportDataExtracted(result),
            replyMarkup: BotKeyboards.PassportConfirmationKeyboard,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );
    }

    // Handles callbacks specific to passport confirmation state.
    private async Task HandlePassportConfirmationCallbacks(string callbackData, long chatId, UserSession session, CancellationToken cancellationToken)
    {
        switch (callbackData)
        {
            case "retryPassport":
                session.State = ConversationState.AwaitingPassport;
                await _sessionRepository.UpdateAsync(session, cancellationToken);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: BotMessages.AwaitingPassportPhoto,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);
                break;

            case "manualPassport":
                session.State = ConversationState.EnteringPassportData;
                await _sessionRepository.UpdateAsync(session, cancellationToken);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: BotMessages.ManualPassportDataPrompt,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);
                break;

            case "confirmPassport":
                session.State = ConversationState.AwaitingVehicleFront;
                await _sessionRepository.UpdateAsync(session, cancellationToken);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: BotMessages.PassportConfirmedPromptVehicleFront,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);
                break;

            default:
                await SendInvalidCallbackResponseAsync(chatId, cancellationToken);
                break;
        }
    }

    // Processes manual text input for vehicle data.
    private async Task HandleManualVehicleTextAsync(
        long chatId,
        string text,
        UserSession session,
        CancellationToken cancellationToken)
    {
        // Input: «VIN;Make;Model;Year;Registration number»
        var parts = text.Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length != 5)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.ManualVehicleDataFormatError,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
            return;
        }

        try
        {
            var model = new VehicleData
            {
                VIN = parts[0],
                Make = parts[1],
                Model = parts[2],
                Year = int.Parse(parts[3]),
                RegistrationNumber = parts[4]
            };

            session.VehicleData = model;
            session.State = ConversationState.ConformingVehicleDoc;
            await _sessionRepository.UpdateAsync(session, cancellationToken);

            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.VehicleDataConfirmationPrompt(model),
                replyMarkup: BotKeyboards.VehicleDocConfirmationKeyboard,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }
        catch (FormatException)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.VehicleYearParseError,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }
    }

    // Processes vehicle document front photo and extracts information.
    private async Task HandleVehicleFrontPhotoAsync(long chatId, Message message, UserSession session, CancellationToken cancellationToken)
    {
        if (message?.Photo == null || message.Photo.Length == 0)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.PhotoProcessingFailed,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken
            );
            return;
        }

        var fileId = message.Photo.Last().FileId;

        using var memoryStream = new MemoryStream();
        await _botClient.GetInfoAndDownloadFile(
            fileId,
            memoryStream,
            cancellationToken: cancellationToken
        );

        memoryStream.Position = 0;

        session.State = ConversationState.ConfirmingVehicleDocFront;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        (string RegistrationNumber, int Year) result;

        try
        {
            result = await _readingDocumentService.ExtractVehicleFrontDataAsync(memoryStream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vehicle front data extraction failed for chat {chatId}", chatId);
            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.VehicleFrontPhotoExtractionFailed,
                replyMarkup: BotKeyboards.VehicleFrontExtractionFailedKeyboard,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken
            );
            return;
        }

        session.VehicleData ??= new VehicleData();
        session.VehicleData.RegistrationNumber = result.RegistrationNumber;
        session.VehicleData.Year = result.Year;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        await _botClient.SendMessage(
            chatId: chatId,
            text: BotMessages.VehicleFrontDataExtracted(result.RegistrationNumber, result.Year),
            replyMarkup: BotKeyboards.VehicleFrontConfirmationKeyboard,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );
    }

    // Processes vehicle document back photo and extracts information.
    private async Task HandleVehicleBackPhotoAsync(long chatId, Message message, UserSession session, CancellationToken cancellationToken)
    {
        if (message?.Photo == null || message.Photo.Length == 0)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.PhotoProcessingFailed,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken
            );
            return;
        }

        var fileId = message.Photo.Last().FileId;

        using var memoryStream = new MemoryStream();
        await _botClient.GetInfoAndDownloadFile(
            fileId,
            memoryStream,
            cancellationToken: cancellationToken
        );

        memoryStream.Position = 0;

        session.State = ConversationState.ConfirmingVehicleDocBack;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        (string VIN, string Model, string Make) result;

        try
        {
            result = await _readingDocumentService.ExtractVehicleBackDataAsync(memoryStream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vehicle back data extraction failed for chat {chatId}", chatId);
            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.VehicleBackPhotoExtractionFailed,
                replyMarkup: BotKeyboards.VehicleBackExtractionFailedKeyboard,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken
            );
            return;
        }

        session.VehicleData ??= new VehicleData();
        session.VehicleData.Model = result.Model;
        session.VehicleData.Make = result.Make;
        session.VehicleData.VIN = result.VIN;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        await _botClient.SendMessage(
            chatId: chatId,
            text: BotMessages.VehicleBackDataExtracted(result.VIN, result.Model, result.Make),
            replyMarkup: BotKeyboards.VehicleBackConfirmationKeyboard,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );
    }

    // Handles callbacks specific to vehicle document confirmation states.
    private async Task HandleVehicleConfirmationCallbacks(string callbackData, long chatId, UserSession session, CancellationToken cancellationToken)
    {
        switch (callbackData)
        {
            case "retryVehicleFront":
                session.State = ConversationState.AwaitingVehicleFront;
                await _sessionRepository.UpdateAsync(session, cancellationToken);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: BotMessages.AwaitingVehicleFrontPhoto,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);
                break;

            case "confirmVehicleFront":
                session.State = ConversationState.AwaitingVehicleBack;
                await _sessionRepository.UpdateAsync(session, cancellationToken);
                await _botClient.SendMessage(chatId, BotMessages.VehicleFrontConfirmedPromptVehicleBack, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                break;

            case "retryVehicleBack":
                session.State = ConversationState.AwaitingVehicleBack;
                await _sessionRepository.UpdateAsync(session, cancellationToken);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: BotMessages.AwaitingVehicleBackPhoto,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);
                break;

            case "confirmVehicleBack":
                session.State = ConversationState.ConformingVehicleDoc;
                await _sessionRepository.UpdateAsync(session, cancellationToken);
                await RequestVehicleDocConfirmationAsync(chatId, session, cancellationToken);
                break;

            case "confirmVehicleDoc":
                session.State = ConversationState.AwaitingPriceConfirmation;
                await _sessionRepository.UpdateAsync(session, cancellationToken);
                await RequestPriceConfirmationAsync(chatId, cancellationToken);
                break;

            case "manualVehicle":
                session.State = ConversationState.EnteringVehicleData;
                await _sessionRepository.UpdateAsync(session, cancellationToken);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: BotMessages.ManualVehicleDataPrompt,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );
                break;

            default:
                await SendInvalidCallbackResponseAsync(chatId, cancellationToken);
                break;
        }
    }

    // Handles callbacks specific to price confirmation state
    private async Task HandlePriceConfirmationCallbacks(string callbackData, long chatId, UserSession session, CancellationToken cancellationToken)
    {
        switch (callbackData)
        {
            case "agreePrice":
                session.State = ConversationState.GeneratingPolicy;
                await _sessionRepository.UpdateAsync(session, cancellationToken);

                await GenerateAndSendPolicyAsync(chatId, session, cancellationToken);
                break;

            case "declinePrice":
                session.State = ConversationState.AwaitingPriceConfirmation;
                await _sessionRepository.UpdateAsync(session, cancellationToken);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: BotMessages.DeclinedPriceOffer,
                    replyMarkup: BotKeyboards.DeclinedPriceOfferKeyboard,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );
                break;

            default:
                await SendInvalidCallbackResponseAsync(chatId, cancellationToken);
                break;
        }
    }

    // Handles callbacks when the conversation is already completed
    private async Task HandleCompletedStateCallbacks(string callbackData, long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: BotMessages.PolicyAlreadyIssued,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );
    }

    #endregion

    // Handles the /start command
    private async Task HandleStartCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        try
        {
            await _sessionRepository.DeleteAsync(chatId, cancellationToken);

            var session = new UserSession
            {
                ChatId = chatId,
                State = ConversationState.AwaitingPassport,
            };

            await _sessionRepository.AddAsync(session, cancellationToken);

            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.StartMessage,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling start command for chat {chatId}", chatId);
            await SendErrorMessageAsync(chatId, cancellationToken);
        }
    }

    // Sends price confirmation request to the user
    private async Task RequestPriceConfirmationAsync(long chatId, CancellationToken cancellationToken)
    {
        decimal price = 100m;

        await _botClient.SendMessage(
            chatId: chatId,
            text: BotMessages.PriceConfirmationPrompt(price),
            replyMarkup: BotKeyboards.PriceConfirmationKeyboard,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);
    }

    // Sends a request for final vehicle document confirmation to the user.
    private async Task RequestVehicleDocConfirmationAsync(long chatId, UserSession session, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: BotMessages.VehicleDataConfirmationPrompt(session.VehicleData!),
            replyMarkup: BotKeyboards.VehicleDocConfirmationKeyboard,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);
    }

    // Generates insurance policy PDF and sends it to the user
    private async Task GenerateAndSendPolicyAsync(long chatId, UserSession session, CancellationToken cancellationToken)
    {
        try
        {
            var passportData = session.PassportData;
            var vehicleData = session.VehicleData;

            if (passportData == null || vehicleData == null)
            {
                _logger.LogError("Missing data for policy generation. Chat ID: {chatId}", chatId);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: BotMessages.MissingPolicyDataError,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );
                return;
            }

            var policy = new PolicyData
            {
                PolicyNumber = GeneratePolicyNumber(),
                IssueDate = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddYears(1),
                Price = 100m,
                PassportInfo = passportData.GetAllData(),
                VehicleInfo = vehicleData.GetAllData()
            };

            var pdfBytes = GeneratePolicyPdfAsync(policy);

            using var fileStream = new MemoryStream(pdfBytes);

            await _botClient.SendDocument(
                chatId: chatId,
                document: InputFile.FromStream(fileStream, $"policy_{policy.PolicyNumber}.pdf"),
                caption: $"Your insurance policy #{policy.PolicyNumber}",
                cancellationToken: cancellationToken
            );

            session.State = ConversationState.Completed;
            await _sessionRepository.UpdateAsync(session, cancellationToken);

            await _botClient.SendMessage(
                chatId: chatId,
                text: BotMessages.PolicyGenerationSuccess(policy.PolicyNumber),
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating policy for chat {chatId}", chatId);
            await SendErrorMessageAsync(chatId, cancellationToken);
        }
    }

    // Generates a PDF document for insurance policy
    private byte[] GeneratePolicyPdfAsync(PolicyData policy)
    {
        using var memoryStream = new MemoryStream();

        var writer = new iText.Kernel.Pdf.PdfWriter(memoryStream);
        var pdfDoc = new iText.Kernel.Pdf.PdfDocument(writer);
        var document = new iText.Layout.Document(pdfDoc);

        var boldFont = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);

        document.Add(new iText.Layout.Element.Paragraph($"Insurance Policy #{policy.PolicyNumber}")
            .SetFontSize(16)
            .SetFont(boldFont));

        document.Add(new iText.Layout.Element.Paragraph("Policy Details")
            .SetFontSize(14)
            .SetFont(boldFont));

        document.Add(new iText.Layout.Element.Paragraph($"Policy Number: {policy.PolicyNumber}"));
        document.Add(new iText.Layout.Element.Paragraph($"Issue Date: {policy.IssueDate:dd.MM.yyyy}"));
        document.Add(new iText.Layout.Element.Paragraph($"Valid Until: {policy.ValidUntil:dd.MM.yyyy}"));
        document.Add(new iText.Layout.Element.Paragraph($"Price: {policy.Price} USD"));

        document.Add(new iText.Layout.Element.Paragraph("Passport Information")
            .SetFontSize(14)
            .SetFont(boldFont));
        document.Add(new iText.Layout.Element.Paragraph(policy.PassportInfo));

        document.Add(new iText.Layout.Element.Paragraph("Vehicle Information")
            .SetFontSize(14)
            .SetFont(boldFont));
        document.Add(new iText.Layout.Element.Paragraph(policy.VehicleInfo));

        document.Close();

        return memoryStream.ToArray();
    }

    #region Helper Methods

    // Gets or creates a user session
    private async Task<UserSession> GetOrCreateSessionAsync(long chatId, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetAsync(chatId, cancellationToken);
        session ??= new UserSession { ChatId = chatId };
        return session;
    }

    // Checks if the message text is a start command
    private bool IsStartCommand(string text) => text?.Trim() == "/start";

    // Sends an error message to the user
    private async Task SendErrorMessageAsync(long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: BotMessages.ErrorOccurred,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );
    }

    // Sends a response for unrecognized messages
    private async Task SendUnrecognizedMessageResponseAsync(long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: BotMessages.UnrecognizedMessage,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );
    }

    // Sends a response for unrecognized commands
    private async Task SendUnrecognizedCommandResponseAsync(long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: BotMessages.UnrecognizedCommand,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );
    }

    // Sends a response for invalid callback data
    private async Task SendInvalidCallbackResponseAsync(long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: BotMessages.InvalidActionContext,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken
        );
    }

    // Generates a unique policy number
    private string GeneratePolicyNumber() => Guid.NewGuid().ToString().Substring(0, 8).ToUpper();

    #endregion
}