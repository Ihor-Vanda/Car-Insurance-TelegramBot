using Amazon.Lambda.AspNetCoreServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarInsuranceTelegramBot
{
    public class LambdaEntryPoint : APIGatewayHttpApiV2ProxyFunction
    {
        protected override void Init(IWebHostBuilder builder)
        {
            builder
                .UseStartup<Startup>()
                .UseLambdaServer();
        }
    }
}