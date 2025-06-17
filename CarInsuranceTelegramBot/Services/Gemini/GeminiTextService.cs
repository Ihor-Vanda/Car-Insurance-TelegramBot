using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CarInsuranceTelegramBot.BotSettings;
using CarInsuranceTelegramBot.Models;
using GenerativeAI;

namespace CarInsuranceTelegramBot.Services.Gemini;

public class GeminiTextService
{
    private readonly GenerativeModel _model;
    private readonly ILogger<GeminiTextService> _logger;
    private const string modelName = "models/gemini-2.0-flash";
    private readonly string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? throw new ArgumentNullException(nameof(apiKey));

    public GeminiTextService(ILogger<GeminiTextService> logger)
    {
        _logger = logger;
        var googleAI = new GoogleAi(apiKey);
        _model = googleAI.CreateGenerativeModel(modelName);
    }

    public async Task<string?> GetAnswerFromGemini(string question, ConversationState currentState)
    {
        try
        {
            string stateInstruction = BotMessages.GetUnrecognizedMessage(currentState)
                                                .Replace("I didn't quite understand that. 🤔 Please follow the current instruction:\n\n", "")
                                                .Trim();

            string fullPrompt = $"You are a Telegram bot that helps users buy car insurance policies. " +
                                $"Current conversation state: {stateInstruction}\n" +
                                $"Your task: " +
                                $"- If the user's message is a meaningful question or statement related to car insurance or telegram bot, respond appropriately within this context. " +
                                $"- If the user's message is gibberish, random symbols, or clearly not human language, respond with: 'I don't understand that message. Please follow the provided rules or use the /start command to begin a new session.'\n\n" +
                                $"User message: {question}";

            var response = await _model.GenerateContentAsync(fullPrompt);

            if (string.IsNullOrEmpty(response.Text())) throw new InvalidOperationException("Response is empty");

            return response.Text();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Gemini API: {ErrorMessage}", ex.Message);
            throw new InvalidOperationException("Can't get response from Gemini: {ex}", ex);
        }
    }
}