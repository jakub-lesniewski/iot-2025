using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using IndustrialIoT.Functions.Models;
using IndustrialIoT.Functions.Services;

namespace IndustrialIoT.Functions
{
    public class BusinessLogicFunctions
    {
        private readonly IAzureIoTService _azureIoTService;
        private static readonly Dictionary<int, Queue<DateTime>> _deviceErrors = new();

        public BusinessLogicFunctions(IAzureIoTService azureIoTService)
        {
            _azureIoTService = azureIoTService;
        }

        [FunctionName("ProcessIoTMessages")]
        public async Task ProcessIoTMessages(
            [EventHubTrigger("messages/events", Connection = "IoTHubEventHubConnectionString")] string eventData,
            ILogger log)
        {
            log.LogInformation($"Received message: {eventData}");

            try
            {
                // Try to parse as different message types
                var jsonObject = JsonConvert.DeserializeObject<dynamic>(eventData);

                if (jsonObject?.ErrorFlags != null)
                {
                    // This is a device error message
                    var errorMsg = JsonConvert.DeserializeObject<DeviceErrorMessage>(eventData)!;
                    await HandleDeviceError(errorMsg.DeviceId, errorMsg.ErrorFlags, log);
                }
                else if (jsonObject?.ProductionStatus != null)
                {
                    // This is a telemetry message
                    var telemetry = JsonConvert.DeserializeObject<TelemetryMessage>(eventData)!;
                    log.LogInformation(
                        $"Telemetry - Device {telemetry.DeviceId}: Status={telemetry.ProductionStatus}, Temp={telemetry.Temperature:F1}°C");
                    await _azureIoTService.SaveTelemetryAsync(telemetry);
                }
                else
                {
                    log.LogWarning($"Unknown message type: {eventData}");
                }
            }
            catch (JsonException ex)
            {
                log.LogWarning($"Invalid JSON message: {ex.Message}");
            }
        }

        [FunctionName("ProcessProductionKPI")]
        public async Task ProcessProductionKPI(
            [BlobTrigger("kpi-data/{name}", Connection = "StorageConnectionString")] string kpiData,
            string name,
            ILogger log)
        {
            log.LogInformation($"Processing KPI blob: {name}");
            var lines = kpiData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                ProductionKpiMessage kpi;
                try
                {
                    kpi = JsonConvert.DeserializeObject<ProductionKpiMessage>(line)!;
                }
                catch (JsonException)
                {
                    continue;
                }

                log.LogInformation(
                    $"Device {kpi.DeviceId} KPI: {kpi.GoodProductionPercentage:F1}% efficiency");

                if (kpi.GoodProductionPercentage < 90.0)
                {
                    log.LogWarning(
                        $"Efficiency below threshold for {kpi.DeviceId}, decreasing production rate");
                    await _azureIoTService.DecreaseProductionRateAsync(kpi.DeviceId);
                }
            }
        }

        [FunctionName("PeriodicBusinessLogicCheck")]
        public Task PeriodicBusinessLogicCheck(
            [TimerTrigger("0 */1 * * * *")] TimerInfo timer,
            ILogger log)
        {
            log.LogInformation("Periodic error queue cleanup");
            var cutoff = DateTime.UtcNow.AddMinutes(-1);
            foreach (var key in _deviceErrors.Keys.ToList())
            {
                var queue = _deviceErrors[key];
                while (queue.Any() && queue.Peek() < cutoff)
                    queue.Dequeue();
                if (!queue.Any()) _deviceErrors.Remove(key);
            }
            log.LogInformation($"Cleanup done. Active error devices: {_deviceErrors.Count}");
            return Task.CompletedTask;
        }

        private async Task HandleDeviceError(int deviceId, DeviceErrors errors, ILogger log)
        {
            if (errors == DeviceErrors.None)
                return;

            if (!_deviceErrors.ContainsKey(deviceId))
                _deviceErrors[deviceId] = new Queue<DateTime>();

            _deviceErrors[deviceId].Enqueue(DateTime.UtcNow);
            var recentCount = _deviceErrors[deviceId].Count;

            log.LogInformation(
                $"Device {deviceId} error count in last minute: {recentCount}");

            if (recentCount > 3)
            {
                log.LogError($"Emergency stop for {deviceId}");
                await _azureIoTService.TriggerEmergencyStopAsync(deviceId);
            }

            var errorEvent = new DeviceErrorEvent
            {
                DeviceId = deviceId,
                ErrorFlags = errors,
                Timestamp = DateTime.UtcNow
            };
            await _azureIoTService.PublishErrorEventAsync(errorEvent);
        }
    }
}