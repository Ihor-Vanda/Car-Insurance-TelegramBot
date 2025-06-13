using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CarInsuranceTelegramBot.Repository;
using CarInsuranceTelegramBot.Services;
using dotenv.net;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Telegram.Bot;

namespace CarInsuranceTelegramBot
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            /// Telegram Bot Client
            var tgBotToken = Environment.GetEnvironmentVariable("BOT_API");
            ArgumentNullException.ThrowIfNull(tgBotToken, "Can't get Telegram Bot Token from Configuration");
            services.AddSingleton<ITelegramBotClient>(sp => new TelegramBotClient(tgBotToken));

            // UpdateHandler adn BackGroundService
            services.AddScoped<BotUpdateHandler>();
            // services.AddHostedService<BotBackGroundService>();

            // DB context and interface
            services.AddDbContext<UserSessionDbContext>(opts => opts.UseInMemoryDatabase("Sessions"));
            services.AddScoped<IUserSessionRepository, UserSessionRepositoryInMemory>();

            // Mindee
            var mindeeApiKey = Environment.GetEnvironmentVariable("MINDEE_API_KEY");
            ArgumentNullException.ThrowIfNull(mindeeApiKey, nameof(mindeeApiKey));
            services.AddHttpClient<IReadingDocumentService, MindeeService>(client =>
            {
                client.BaseAddress = new Uri("https://api.mindee.net");
                client.DefaultRequestHeaders.Add("Authorization", $"Token {mindeeApiKey}");
            });
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}