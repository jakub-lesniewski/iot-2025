using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using IndustrialIoT.Functions.Services;

[assembly: FunctionsStartup(typeof(IndustrialIoT.Functions.Startup))]
namespace IndustrialIoT.Functions
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton<IAzureIoTService, AzureIoTService>();
        }
    }
}