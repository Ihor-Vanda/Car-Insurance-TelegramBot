using System.Globalization;
using CarInsuranceTelegramBot.Models;
using CarInsuranceTelegramBot.Repository;
using CarInsuranceTelegramBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

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
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    await HandleMessageUpdateAsync(update.Message!, cancellationToken);
                    break;
                case UpdateType.CallbackQuery:
                    await HandleCallbackQueryUpdateAsync(update.CallbackQuery!, cancellationToken);
                    break;
                default:
                    _logger.LogInformation("Unhandled update type: {type}", update.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex, cancellationToken);
        }
    }

    // Handles message updates from Telegram
    private async Task HandleMessageUpdateAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;

        try
        {
            if (IsStartCommand(message.Text!))
            {
                await HandleStartCommandAsync(chatId, cancellationToken);
                return;
            }

            var session = await GetOrCreateSessionAsync(chatId, cancellationToken);

            switch (session.State)
            {
                case ConversationState.EnteringPassportData:
                    await HandleManualPassportTextAsync(chatId, message.Text!, session, cancellationToken);
                    return;
                case ConversationState.EnteringVehicleData:
                    await HandleManualVehicleTextAsync(chatId, message.Text!, session, cancellationToken);
                    return;
                case ConversationState.Completed:
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Your insurance policy has already been issued. Use /start to purchase a new policy.",
                        cancellationToken: cancellationToken
                    );
                    return;
            }

            if (HasPhoto(message))
            {
                await HandlePhotoMessageAsync(chatId, message, session, cancellationToken);
                return;
            }

            // Message wasn't handled
            await SendUnrecognizedMessageResponseAsync(chatId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message from chat {chatId}", chatId);
            await SendErrorMessageAsync(chatId, cancellationToken);
        }
    }

    private async Task HandleManualPassportTextAsync(
        long chatId,
        string text,
        UserSession session,
        CancellationToken cancellationToken)
    {
        //Input «FullName;PassportNumber;DateOfBirth;IssueDate;ExpiryDate»
        var parts = text.Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length != 5)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Wrong message format. Try again. And follow this example:\nFull Name;Passport Number;Passport Date Of Birth;Date Of Issue;Date Of Expire\nDate format example: '31.12.2000'",
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

            var kb = new InlineKeyboardMarkup([
                [
                InlineKeyboardButton.WithCallbackData("✅ Confirm", "confirmPassport"),
                InlineKeyboardButton.WithCallbackData("🔄 Try again with photo",  "retryPassport")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✍️ Enter manual again",    "manualPassport")
            ]
            ]);

            await _botClient.SendMessage(
                chatId: chatId,
                text: $"You entered: \n" +
                      $"Full name: {model.FullName}\n" +
                      $"Passport number: {model.PassportNumber}\n" +
                      $"Date of birth: {model.DateOfBirth:dd.MM.yyyy}\n" +
                      $"Date of issue: {model.IssueDate:dd.MM.yyyy}\n" +
                      $"Date of expire: {model.ExpiryDate:dd.MM.yyyy}",
                replyMarkup: kb,
                cancellationToken: cancellationToken);
        }
        catch (FormatException)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Can't read date. Be sure that date have correct format dd.MM.yyyy and try again.",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleManualVehicleTextAsync(
        long chatId,
        string text,
        UserSession session,
        CancellationToken cancellationToken)
    {
        // Input: «VIN;Make,Mode;Year;DocNumber»
        var parts = text.Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length != 5)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Wrong input!",
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

            var kb = new InlineKeyboardMarkup([
                [
                InlineKeyboardButton.WithCallbackData("✅ Confirm", "confirmVehicleDoc"),
                InlineKeyboardButton.WithCallbackData("🔄 Try again with photo","retryVehicleFront")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✍️ Enter manual again","manualVehicle")
            ]
            ]);

            await _botClient.SendMessage(
                chatId: chatId,
                text: $"You entered: \n" +
                      $"VIN: {model.VIN}\n" +
                      $"Make: {model.Make}\n" +
                      $"Model: {model.Model}\n" +
                      $"Year: {model.Year}\n" +
                      $"Registration number: {model.RegistrationNumber}",
                replyMarkup: kb,
                cancellationToken: cancellationToken);
        }
        catch (FormatException)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Can't read year. Be sure that you follow example:\nVIN;Make,Model;Year;Registration Number\n and try again.",
                cancellationToken: cancellationToken);
        }
    }

    // Handles callback query updates from Telegram
    private async Task HandleCallbackQueryUpdateAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message == null)
        {
            _logger.LogWarning("Received null callback query or message");
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;

        try
        {
            var session = await GetOrCreateSessionAsync(chatId, cancellationToken);

            await HandleCallbackDataAsync(callbackQuery.Data!, chatId, session, cancellationToken);
            await _botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling callback query from chat {chatId}", chatId);
            await SendErrorMessageAsync(chatId, cancellationToken);
        }
    }

    // Processes callback data
    private async Task HandleCallbackDataAsync(string callbackData, long chatId, UserSession session, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(callbackData))
        {
            _logger.LogWarning("Received empty callback data for chat {chatId}", chatId);
            await SendUnrecognizedCommandResponseAsync(chatId, cancellationToken);
            return;
        }

        // Always allow restart regardless of state
        if (callbackData == "restart")
        {
            await HandleStartCommandAsync(chatId, cancellationToken);
            return;
        }

        switch (session.State)
        {
            case ConversationState.ConfirmingPassport:
                await HandlePassportConfirmationCallbacks(callbackData, chatId, session, cancellationToken);
                break;

            case ConversationState.ConfirmingVehicleDocFront:
            case ConversationState.ConfirmingVehicleDocBack:
            case ConversationState.ConformingVehicleDoc:
            case ConversationState.AwaitingVehicleFront:
            case ConversationState.AwaitingVehicleBack:
                await HandleVehicleConfirmationCallbacks(callbackData, chatId, session, cancellationToken);
                break;

            case ConversationState.AwaitingPriceConfirmation:
                await HandlePriceConfirmationCallbacks(callbackData, chatId, session, cancellationToken);
                break;

            case ConversationState.Completed:
                await HandleCompletedStateCallbacks(callbackData, chatId, cancellationToken);
                break;

            default:
                _logger.LogWarning("Received callback '{callbackData}' in inappropriate state {session.State} for chat {chatId}", callbackData, session.State, chatId);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "This action is no longer available. Use /start to begin a new session.",
                    cancellationToken: cancellationToken
                );
                break;
        }
    }


    #region Callback Handlers
    // Handles callbacks specific to passport confirmation state
    private async Task HandlePassportConfirmationCallbacks(string callbackData, long chatId, UserSession session, CancellationToken cancellationToken)
    {
        switch (callbackData)
        {
            case "retryPassport":
                session.State = ConversationState.AwaitingPassport;
                await _sessionRepository.UpdateAsync(session, cancellationToken);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "Let's try again. Please send a clear photo of your passport.",
                    cancellationToken: cancellationToken);
                break;

            case "manualPassport":
                session.State = ConversationState.EnteringPassportData;
                await _sessionRepository.UpdateAsync(session, cancellationToken);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "Entering passport data in message next way:\nFull Name;Passport Number;Passport Date Of Birth;Date Of Issue;Date Of Expire\nDate format example: '31.12.2000'",
                    cancellationToken: cancellationToken);
                break;

            case "confirmPassport":
                session.State = ConversationState.AwaitingVehicleFront;
                await _sessionRepository.UpdateAsync(session, cancellationToken);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "Passport confirmed. Please send a photo of your front vehicle registration document.",
                    cancellationToken: cancellationToken);
                break;

            default:
                await SendInvalidCallbackResponseAsync(chatId, cancellationToken);
                break;
        }
    }

    // Handles callbacks specific to vehicle document confirmation state
    private async Task HandleVehicleConfirmationCallbacks(string callbackData, long chatId, UserSession session, CancellationToken cancellationToken)
    {
        switch (callbackData)
        {
            case "retryVehicleFront":
                session.State = ConversationState.AwaitingVehicleFront;
                await _sessionRepository.UpdateAsync(session, cancellationToken);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "Let's try again. Please send a clear photo of your front side vehicle registration.",
                    cancellationToken: cancellationToken);
                break;

            case "confirmVehicleFront":
                session.State = ConversationState.AwaitingVehicleBack;
                await _sessionRepository.UpdateAsync(session, cancellationToken);
                await _botClient.SendMessage(chatId, $"Front side confirmed. Now send the BACK side of your vehicle document.", cancellationToken: cancellationToken);
                break;

            case "retryVehicleBack":
                session.State = ConversationState.AwaitingVehicleBack;
                await _sessionRepository.UpdateAsync(session, cancellationToken);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "Let's try again. Please send a clear photo of your back side vehicle registration.",
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
                    text: "Entering vehicle data in message next way:\n" +
                          "«VIN;Model;Make;Year;Registration number»",
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

                var keyboard = new InlineKeyboardMarkup(
                [
                    [
                        InlineKeyboardButton.WithCallbackData("✅ Accept", "agreePrice"),
                        InlineKeyboardButton.WithCallbackData("🔄 Start over", "restart")
                    ]
                ]);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "Unfortunately, we don't have different pricing options available at this time.",
                    replyMarkup: keyboard,
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
            text: "Your insurance policy has already been issued. Use /start to purchase a new policy.",
            cancellationToken: cancellationToken
        );
    }

    // Sends a response for invalid callback data
    private async Task SendInvalidCallbackResponseAsync(long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: "This action is not valid in the current context. Please follow the provided options.",
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
                text: "Hi! I'm a bot that helps you purchase car insurance. \nTo get started, please send a photo of your passport.",
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling start command for chat {chatId}", chatId);
            await SendErrorMessageAsync(chatId, cancellationToken);
        }
    }

    // Handles photo messages based on current conversation state
    private async Task HandlePhotoMessageAsync(long chatId, Message message, UserSession session, CancellationToken cancellationToken)
    {
        try
        {
            switch (session.State)
            {
                case ConversationState.AwaitingPassport:
                    await HandlePassportPhotoAsync(chatId, message, session, cancellationToken);
                    break;

                case ConversationState.AwaitingVehicleFront:
                    await HandleVehicleFrontPhotoAsync(chatId, message, session, cancellationToken);
                    break;

                case ConversationState.AwaitingVehicleBack:
                    await HandleVehicleBackPhotoAsync(chatId, message, session, cancellationToken);
                    break;

                default:
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "I'm not currently expecting a photo. Use /start to begin a new session.",
                        cancellationToken: cancellationToken
                    );
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing photo for chat {chatId}", chatId);
            await SendErrorMessageAsync(chatId, cancellationToken);
        }
    }

    // Processes passport photo and extracts information
    private async Task HandlePassportPhotoAsync(long chatId, Message message, UserSession session, CancellationToken cancellationToken)
    {
        if (message?.Photo == null || message.Photo.Length == 0)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "I couldn't process this photo. Please try again with a clearer image.",
                cancellationToken: cancellationToken
            );
            return;
        }

        var fileId = message.Photo.Last().FileId;

        // Download the file
        using var memoryStream = new MemoryStream();
        await _botClient.GetInfoAndDownloadFile(
            fileId,
            memoryStream,
            cancellationToken: cancellationToken
        );

        memoryStream.Position = 0;

        session.State = ConversationState.ConfirmingPassport;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        // Process photo
        PassportData result;

        try
        {
            result = await _readingDocumentService.ExtractPassportDataAsync(memoryStream, cancellationToken);
        }
        catch (Exception)
        {
            var kb = new InlineKeyboardMarkup([
                [InlineKeyboardButton.WithCallbackData("🔄 Try again", "retryPassport")],
                [InlineKeyboardButton.WithCallbackData("✍️ Enter manually", "manualPassport")]
            ]);

            await _botClient.SendMessage(
                chatId: chatId,
                text: "Can't extract data from photo. What you want to do?",
                replyMarkup: kb,
                cancellationToken: cancellationToken
            );
            return;
        }

        session.PassportData = result;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        // Create confirmation keyboard
        var keyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("✅ Confirm","confirmPassport"),
                InlineKeyboardButton.WithCallbackData("🔄 Try again with photo","retryPassport")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✍️ Enter manual","manualPassport"),
            ]
        ]);

        // Send response
        await _botClient.SendMessage(
            chatId: chatId,
            text: $"Passport information extracted:\n{result.GetAllData()}\n \nIs this correct?",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken
        );
    }

    // Processes vehicle document front photo and extracts information
    private async Task HandleVehicleFrontPhotoAsync(long chatId, Message message, UserSession session, CancellationToken cancellationToken)
    {
        if (message?.Photo == null || message.Photo.Length == 0)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "I couldn't process this photo. Please try again with a clearer image.",
                cancellationToken: cancellationToken
            );
            return;
        }

        var fileId = message.Photo.Last().FileId;

        // Download the file
        using var memoryStream = new MemoryStream();
        await _botClient.GetInfoAndDownloadFile(
            fileId,
            memoryStream,
            cancellationToken: cancellationToken
        );

        memoryStream.Position = 0;

        session.State = ConversationState.ConfirmingVehicleDocFront;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        // Process photo
        (string RegistrationNumber, int Year) result;

        try
        {
            result = await _readingDocumentService.ExtractVehicleFrontDataAsync(memoryStream, cancellationToken);
        }
        catch (Exception)
        {
            var kb = new InlineKeyboardMarkup([
                [InlineKeyboardButton.WithCallbackData("🔄 Try again","retryVehicleFront")],
                [InlineKeyboardButton.WithCallbackData("✍️ Enter manually","manualVehicle")]
            ]);

            await _botClient.SendMessage(
                chatId: chatId,
                text: "Can't extract data from photo. Next step?",
                replyMarkup: kb,
                cancellationToken: cancellationToken
            );
            return;
        }

        session.VehicleData ??= new VehicleData();
        session.VehicleData.RegistrationNumber = result.RegistrationNumber;
        session.VehicleData.Year = result.Year;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        // Create confirmation keyboard
        var keyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("✅ Confirm","confirmVehicleFront"),
                InlineKeyboardButton.WithCallbackData("🔄 Try again with photo","retryVehicleFront")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✍️ Enter manual","manualVehicle"),
            ]
        ]);

        // Send response
        await _botClient.SendMessage(
            chatId: chatId,
            text: $"Vehicle information extracted:\nRegistration number: {result.RegistrationNumber}\nYear: {result.Year}\n \nIs this correct?",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken
        );
    }

    // Processes vehicle document front photo and extracts information
    private async Task HandleVehicleBackPhotoAsync(long chatId, Message message, UserSession session, CancellationToken cancellationToken)
    {
        if (message?.Photo == null || message.Photo.Length == 0)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "I couldn't process this photo. Please try again with a clearer image.",
                cancellationToken: cancellationToken
            );
            return;
        }

        var fileId = message.Photo.Last().FileId;

        // Download the file
        using var memoryStream = new MemoryStream();
        await _botClient.GetInfoAndDownloadFile(
            fileId,
            memoryStream,
            cancellationToken: cancellationToken
        );

        memoryStream.Position = 0;

        session.State = ConversationState.ConfirmingVehicleDocBack;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        // Process photo
        (string VIN, string Model, string Make) result;

        try
        {
            result = await _readingDocumentService.ExtractVehicleBackDataAsync(memoryStream, cancellationToken);
        }
        catch (Exception)
        {
            var kb = new InlineKeyboardMarkup([
                [InlineKeyboardButton.WithCallbackData("🔄 Try again","retryVehicleBack")],
                [InlineKeyboardButton.WithCallbackData("✍️ Enter manually","manualVehicle")]
            ]);

            await _botClient.SendMessage(
                chatId: chatId,
                text: "Can't extract data from photo. Next step?",
                replyMarkup: kb,
                cancellationToken: cancellationToken
            );
            return;
        }

        session.VehicleData ??= new VehicleData();
        session.VehicleData.Model = result.Model;
        session.VehicleData.Make = result.Make;
        session.VehicleData.VIN = result.VIN;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        // Create confirmation keyboard
        var keyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("✅ Confirm","confirmVehicleBack"),
                InlineKeyboardButton.WithCallbackData("🔄 Try again with photo","retryVehicleBack")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✍️ Enter manual","manualVehicle"),
            ]
        ]);

        // Send response
        await _botClient.SendMessage(
            chatId: chatId,
            text: $"Vehicle information extracted:\nVIN: {result.VIN}\nModel: {result.Model}\nMake: {result.Make}\n \nIs this correct?",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken
        );
    }

    // Sends price confirmation request to the user
    private async Task RequestPriceConfirmationAsync(long chatId, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup(
        [
            [
                InlineKeyboardButton.WithCallbackData("✅ Accept", "agreePrice"),
                InlineKeyboardButton.WithCallbackData("❌ Decline", "declinePrice")
            ]
        ]);

        await _botClient.SendMessage(
            chatId: chatId,
            text: "Your vehicle insurance price is 100 USD. Do you accept this offer?",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task RequestVehicleDocConfirmationAsync(long chatId, UserSession session, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("✅ Confirm","confirmVehicleDoc"),
                InlineKeyboardButton.WithCallbackData("🔄 Try again with photo","retryVehicleFront")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✍️ Enter manual","manualVehicle"),
            ]
        ]);

        await _botClient.SendMessage(
            chatId: chatId,
            text: $"Vehicle document info:\n{session.VehicleData.GetAllData()}\n \nIs it correct?",
            replyMarkup: keyboard,
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
                    text: "Sorry, there was an error generating your policy. Please start over with /start",
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

            // Generate PDF
            var pdfBytes = GeneratePolicyPdfAsync(policy);

            // Send as document
            using var fileStream = new MemoryStream(pdfBytes);
            var inputFile = InputFile.FromStream(fileStream, $"policy_{policy.PolicyNumber}.pdf");

            await _botClient.SendDocument(
                chatId: chatId,
                document: inputFile,
                caption: $"Your insurance policy #{policy.PolicyNumber}",
                cancellationToken: cancellationToken
            );

            // Update session state
            session.State = ConversationState.Completed;
            await _sessionRepository.UpdateAsync(session, cancellationToken);

            // Send completion message
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Thank you for purchasing insurance! Your policy has been issued. Use /start if you'd like to purchase another policy.",
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

        // Create bold font
        var boldFont = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);

        document.Add(new iText.Layout.Element.Paragraph($"Insurance Policy #{policy.PolicyNumber}")
            .SetFontSize(16)
            .SetFont(boldFont));

        // Add policy details
        document.Add(new iText.Layout.Element.Paragraph("Policy Details")
            .SetFontSize(14)
            .SetFont(boldFont));

        document.Add(new iText.Layout.Element.Paragraph($"Policy Number: {policy.PolicyNumber}"));
        document.Add(new iText.Layout.Element.Paragraph($"Issue Date: {policy.IssueDate:dd.MM.yyyy}"));
        document.Add(new iText.Layout.Element.Paragraph($"Valid Until: {policy.ValidUntil:dd.MM.yyyy}"));
        document.Add(new iText.Layout.Element.Paragraph($"Price: {policy.Price} USD"));

        // Add passport information
        document.Add(new iText.Layout.Element.Paragraph("Passport Information")
            .SetFontSize(14)
            .SetFont(boldFont));
        document.Add(new iText.Layout.Element.Paragraph(policy.PassportInfo));

        // Add vehicle information
        document.Add(new iText.Layout.Element.Paragraph("Vehicle Information")
            .SetFontSize(14)
            .SetFont(boldFont));
        document.Add(new iText.Layout.Element.Paragraph(policy.VehicleInfo));

        document.Close();

        return memoryStream.ToArray();
    }

    // Logs exception and returns error task
    public Task HandleErrorAsync(Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An error occurred while processing update");
        return Task.CompletedTask;
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

    // Checks if the message contains photos
    private bool HasPhoto(Message message) => message?.Photo is { Length: > 0 };

    // Creates a confirmation keyboard with confirm and retry buttons
    private InlineKeyboardMarkup CreateConfirmationKeyboard(string confirmCallbackData, string retryCallbackData)
    {
        return new InlineKeyboardMarkup(
        [
            [
                    InlineKeyboardButton.WithCallbackData("✅ Confirm", confirmCallbackData),
                    InlineKeyboardButton.WithCallbackData("🔄 Try again", retryCallbackData)
                ]
        ]);
    }

    // Generates a unique policy number
    private string GeneratePolicyNumber() => Guid.NewGuid().ToString().Substring(0, 8).ToUpper();


    // Sends an error message to the user
    private async Task SendErrorMessageAsync(long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: "Sorry, an error occurred. Please try again or use /start to begin a new session.",
            cancellationToken: cancellationToken
        );
    }

    // Sends a response for unrecognized messages
    private async Task SendUnrecognizedMessageResponseAsync(long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: "I don't understand this message. Please use /start to begin or follow the instructions.",
            cancellationToken: cancellationToken
        );
    }

    // Sends a response for unrecognized commands
    private async Task SendUnrecognizedCommandResponseAsync(long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: "Unrecognized command. Use /start to begin.",
            cancellationToken: cancellationToken
        );
    }

    #endregion
}