using CarInsuranceTelegramBot;
using CarInsuranceTelegramBot.Repository;
using CarInsuranceTelegramBot.Services;
using dotenv.net;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Telegram.Bot;
using ILogger = Microsoft.Extensions.Logging.ILogger;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

// Load env variables
DotEnv.Load();

var mindeeApiKey = Environment.GetEnvironmentVariable("MINDEE_API_KEY");

// Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Telegram Bot Client
var tgBotToken = Environment.GetEnvironmentVariable("BOT_API");
ArgumentNullException.ThrowIfNull(tgBotToken, "Can't get Telegram Bot Token from Configuration");
builder.Services.AddSingleton<ITelegramBotClient>(sp => new TelegramBotClient(tgBotToken));

// UpdateHandler adn BackGroundService
builder.Services.AddScoped<BotUpdateHandler>();
builder.Services.AddHostedService<BotBackGroundService>();

// DB context and interface
builder.Services.AddDbContext<UserSessionDbContext>(opts => opts.UseInMemoryDatabase("Sessions"));
builder.Services.AddScoped<IUserSessionRepository, UserSessionRepositoryInMemory>();

// Mindee
builder.Services.AddHttpClient<IReadingDocumentService, MindeeService>(client =>
{
    client.BaseAddress = new Uri("https://api.mindee.net");
    client.DefaultRequestHeaders.Add("Authorization", $"Token {mindeeApiKey}");
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
