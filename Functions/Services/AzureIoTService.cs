using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using IndustrialIoT.Functions.Models;

namespace IndustrialIoT.Functions.Services
{
    public interface IAzureIoTService
    {
        Task SaveTelemetryAsync(TelemetryMessage telemetry);
        Task DecreaseProductionRateAsync(int deviceId);
        Task TriggerEmergencyStopAsync(int deviceId);
        Task PublishErrorEventAsync(DeviceErrorEvent errorEvent);
        Task ResetErrorStatusAsync(int deviceId);
    }

    public class AzureIoTService : IAzureIoTService, IDisposable
    {
        private readonly ServiceClient _serviceClient;
        private readonly RegistryManager _registryManager;
        private readonly string _deviceIdTemplate;
        private readonly ILogger<AzureIoTService> _logger;

        public AzureIoTService(ILogger<AzureIoTService> logger)
        {
            _logger = logger;
            var connectionString = Environment.GetEnvironmentVariable("IoTHubConnectionString")
                ?? throw new ArgumentException("IoTHubConnectionString not found in environment variables");

            _serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
            _registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            _deviceIdTemplate = Environment.GetEnvironmentVariable("DeviceIdTemplate") ?? "device{id}";

            _logger.LogInformation("AzureIoTService initialized using device template: {Template}", _deviceIdTemplate);
        }

        public Task SaveTelemetryAsync(TelemetryMessage telemetry)
        {
            _logger.LogInformation(
                "Telemetry saved - Device: {DeviceId}, Status: {Status}, Temp: {Temp}",
                telemetry.DeviceId, telemetry.ProductionStatus, telemetry.Temperature);
            return Task.CompletedTask;
        }

        public async Task DecreaseProductionRateAsync(int deviceId)
        {
            var deviceName = FormatDeviceName(deviceId);
            _logger.LogInformation("Decreasing production rate for device {DeviceName}", deviceName);

            try
            {
                var twin = await _registryManager.GetTwinAsync(deviceName);
                int currentRate = ReadProductionRate(twin);
                int newRate = Math.Max(0, currentRate - 10);

                twin.Properties.Desired["productionRate"] = newRate;
                await _registryManager.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);

                _logger.LogInformation(
                    "Production rate updated for {DeviceName}: {OldRate}% -> {NewRate}%",
                    deviceName, currentRate, newRate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrease production rate for device {DeviceName}", deviceName);
                throw;
            }
        }

        public async Task TriggerEmergencyStopAsync(int deviceId)
        {
            var deviceName = FormatDeviceName(deviceId);
            _logger.LogInformation("Invoking EmergencyStop on {DeviceName}", deviceName);

            try
            {
                var method = new CloudToDeviceMethod("EmergencyStop")
                {
                    ResponseTimeout = TimeSpan.FromSeconds(30),
                    ConnectionTimeout = TimeSpan.FromSeconds(30)
                };
                method.SetPayloadJson(JsonConvert.SerializeObject(new { deviceId }));

                var result = await _serviceClient.InvokeDeviceMethodAsync(deviceName, method);
                if (result.Status != 200)
                {
                    _logger.LogError(
                        "EmergencyStop failed on {DeviceName} - Status: {Status}, Response: {Response}",
                        deviceName, result.Status, result.GetPayloadAsJson());
                }
                else
                {
                    _logger.LogInformation("EmergencyStop executed on {DeviceName}", deviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during EmergencyStop for device {DeviceName}", deviceName);
                throw;
            }
        }

        public async Task PublishErrorEventAsync(DeviceErrorEvent errorEvent)
        {
            try
            {
                string payload = JsonConvert.SerializeObject(errorEvent);
                var deviceName = FormatDeviceName(errorEvent.DeviceId);

                var message = new Message(Encoding.UTF8.GetBytes(payload));
                message.Properties.Add("messageType", "errorEvent");
                message.Properties.Add("deviceId", errorEvent.DeviceId.ToString());

                // Send message to device
                await _serviceClient.SendAsync(deviceName, message);

                _logger.LogInformation(
                    "Published error event for Device {DeviceId} ({DeviceName})",
                    errorEvent.DeviceId, deviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish error event for Device {DeviceId}", errorEvent.DeviceId);
                throw;
            }
        }

        public async Task ResetErrorStatusAsync(int deviceId)
        {
            var deviceName = FormatDeviceName(deviceId);
            _logger.LogInformation("Resetting error status on {DeviceName}", deviceName);

            try
            {
                var method = new CloudToDeviceMethod("ResetErrorStatus")
                {
                    ResponseTimeout = TimeSpan.FromSeconds(30),
                    ConnectionTimeout = TimeSpan.FromSeconds(30)
                };
                method.SetPayloadJson(JsonConvert.SerializeObject(new { deviceId }));

                var result = await _serviceClient.InvokeDeviceMethodAsync(deviceName, method);
                if (result.Status != 200)
                {
                    _logger.LogError(
                        "ResetErrorStatus failed on {DeviceName} - Status: {Status}, Response: {Response}",
                        deviceName, result.Status, result.GetPayloadAsJson());
                }
                else
                {
                    _logger.LogInformation("ResetErrorStatus executed on {DeviceName}", deviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during ResetErrorStatus for device {DeviceName}", deviceName);
                throw;
            }
        }

        private string FormatDeviceName(int deviceId)
        {
            return _deviceIdTemplate.Replace("{id}", deviceId.ToString());
        }

        private int ReadProductionRate(Twin twin)
        {
            try
            {
                if (twin.Properties.Reported.Contains("productionRate"))
                {
                    var value = twin.Properties.Reported["productionRate"];
                    if (value is int intValue)
                        return intValue;
                    if (int.TryParse(value.ToString(), out int parsedValue))
                        return parsedValue;
                }
                return 100; // Default production rate
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read production rate from device twin, using default");
                return 100;
            }
        }

        public void Dispose()
        {
            _serviceClient?.Dispose();
            _registryManager?.Dispose();
        }
    }
}