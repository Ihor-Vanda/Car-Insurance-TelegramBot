using CarInsuranceTelegramBot;
using CarInsuranceTelegramBot.Repository;
using CarInsuranceTelegramBot.Services;
using dotenv.net;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Telegram.Bot;
using ILogger = Microsoft.Extensions.Logging.ILogger;

var builder = WebApplication.CreateBuilder(args);

DotEnv.Load();

// Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

var startup = new Startup(builder.Configuration);

startup.ConfigureServices(builder.Services);

var app = builder.Build();

app.UseHttpsRedirection();

app.Run();
