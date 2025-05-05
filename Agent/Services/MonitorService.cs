using System;
using System.Threading;
using System.Threading.Tasks;

namespace IndustrialIoT.Agent.Services
{
    /// <summary>
    /// Zarządza pętlą monitorowania: odczyt telemetrii i aktualizacja twin.
    /// </summary>
    public static class MonitorService
    {
        public static async Task Start(
            CloudConnector cloudService, PlcConnector plcService,
            int intervalMs, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Rozpoczęto monitorowanie urządzenia {plcService.DeviceId}...");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var telemetry = await plcService.ReadTelemetryAsync();
                    if (telemetry != null)
                        await cloudService.SendTelemetryAsync(telemetry);

                    var state = await plcService.ReadStateAsync();
                    if (state != null)
                        await cloudService.UpdateTwinAsync(state);

                    await Task.Delay(intervalMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MonitorService error: {ex.Message}");
                    await Task.Delay(5000, cancellationToken);
                }
            }
            Console.WriteLine("Monitorowanie zakończone.");
        }
    }
}