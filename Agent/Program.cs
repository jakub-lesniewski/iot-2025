using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Configuration;
using IndustrialIoT.Agent.Services;

namespace IndustrialIoT.Agent
{
    class AgentApp
    {
        static async Task Main(string[] args)
        {
            ConfigLoader.Load();
            Console.WriteLine($"Uruchamianie agenta #{Settings.DeviceNumber}...");

            try
            {
                using var client = DeviceClient.CreateFromConnectionString(
                    Settings.IotHubConnection, TransportType.Mqtt);
                await client.OpenAsync();

                var cloudConnector = new CloudConnector(client);
                await cloudConnector.RegisterHandlersAsync();

                var plcConnector = new PlcConnector(Settings.OpcUaEndpoint, Settings.DeviceNumber);
                await plcConnector.ConnectAsync();
                cloudConnector.BindConnector(plcConnector);

                Console.WriteLine("Agent połączony z OPC UA i IoT Hub.");
                Console.WriteLine("Naciśnij 'q' aby zakończyć.");

                var cts = new CancellationTokenSource();
                _ = Task.Run(() => MonitorService.Start(
                    cloudConnector, plcConnector, Settings.TelemetryIntervalMs, cts.Token));

                while (Console.ReadKey(true).KeyChar != 'q') { }
                cts.Cancel();

                await plcConnector.DisconnectAsync();
                await client.CloseAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd: {ex.Message}");
            }

            Console.WriteLine("Agent zakończony.");
        }
    }

    static class ConfigLoader
    {
        public static void Load()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true)
                .Build();

            Settings.IotHubConnection = config.GetConnectionString("AzureIoTHub");
            Settings.OpcUaEndpoint = config.GetConnectionString("OpcServer");
            Settings.DeviceNumber = config.GetValue<int>("DeviceSettings:DeviceId");
            Settings.TelemetryIntervalMs = config.GetValue<int>("DeviceSettings:TelemetryIntervalMs");

            if (Settings.DeviceNumber <= 0)
                throw new ArgumentException("DeviceId musi być > 0");
            if (Settings.TelemetryIntervalMs <= 0)
                Settings.TelemetryIntervalMs = 1000;
            if (string.IsNullOrEmpty(Settings.IotHubConnection))
                throw new ArgumentException("Brak connection string do IoT Hub");
            if (string.IsNullOrEmpty(Settings.OpcUaEndpoint))
                throw new ArgumentException("Brak URL serwera OPC UA");

            Console.WriteLine($"Konfiguracja: agent {Settings.DeviceNumber}, " +
                              $"interval {Settings.TelemetryIntervalMs}ms, " +
                              $"OPC UA {Settings.OpcUaEndpoint}");
        }
    }
}