using Opc.UaFx;
using Opc.UaFx.Client;
using System;
using System.Threading.Tasks;
using IndustrialIoT.Agent;
using IndustrialIoT.Agent.Models;


namespace IndustrialIoT.Agent.Services
{
    public class PlcConnector
    {
        private readonly string _endpoint;
        public int DeviceId { get; }
        private OpcClient? _client;

        public PlcConnector(string endpointUrl, int deviceId)
        {
            _endpoint = endpointUrl;
            DeviceId = deviceId;
        }

        public Task ConnectAsync()
        {
            _client = new OpcClient(_endpoint);
            _client.Connect();
            Console.WriteLine($"Connected to OPC UA at {_endpoint}");
            VerifyDeviceNode();
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            _client?.Disconnect();
            _client?.Dispose();
            Console.WriteLine("Disconnected from OPC UA");
            return Task.CompletedTask;
        }

        private void VerifyDeviceNode()
        {
            if (_client == null)
                throw new InvalidOperationException("OPC UA client not initialized");

            var nodePath = $"ns=2;s=Device {DeviceId}/ProductionStatus";
            var status = _client.ReadNode(nodePath).Value;
            Console.WriteLine($"Verified device {DeviceId} status node: {status}");
        }

        public Task<TelemetryData?> ReadTelemetryAsync()
        {
            if (_client == null) throw new InvalidOperationException("Not connected to OPC UA");

            try
            {
                var nsPath = $"ns=2;s=Device {DeviceId}/";
                var status = (int)_client.ReadNode(nsPath + "ProductionStatus").Value;
                var order = (string)_client.ReadNode(nsPath + "WorkorderId").Value;
                var good = (long)_client.ReadNode(nsPath + "GoodCount").Value;
                var bad = (long)_client.ReadNode(nsPath + "BadCount").Value;
                var temp = (double)_client.ReadNode(nsPath + "Temperature").Value;

                return Task.FromResult<TelemetryData?>(new TelemetryData
                {
                    DeviceId = DeviceId,
                    ProductionStatus = (ProductionStatus)status,
                    WorkorderId = string.IsNullOrEmpty(order) ? Guid.Empty : Guid.Parse(order),
                    GoodCount = good,
                    BadCount = bad,
                    Temperature = temp,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Telemetry read error: {ex.Message}");
                return Task.FromResult<TelemetryData?>(null);
            }
        }

        public Task<DeviceState?> ReadStateAsync()
        {
            if (_client == null) throw new InvalidOperationException("Not connected to OPC UA");

            try
            {
                var nsPath = $"ns=2;s=Device {DeviceId}/";
                var rate = (int)_client.ReadNode(nsPath + "ProductionRate").Value;
                var errs = (int)_client.ReadNode(nsPath + "DeviceError").Value;

                return Task.FromResult<DeviceState?>(new DeviceState
                {
                    DeviceId = DeviceId,
                    ProductionRate = rate,
                    DeviceErrors = (DeviceErrors)errs,
                    LastUpdated = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"State read error: {ex.Message}");
                return Task.FromResult<DeviceState?>(null);
            }
        }

        public Task TriggerEmergencyStopAsync()
        {
            if (_client == null) throw new InvalidOperationException("Not connected to OPC UA");
            _client.CallMethod($"ns=2;s=Device {DeviceId}", $"ns=2;s=Device {DeviceId}/EmergencyStop");
            Console.WriteLine($"EmergencyStop on device {DeviceId}");
            return Task.CompletedTask;
        }

        public Task ResetErrorsAsync()
        {
            if (_client == null) throw new InvalidOperationException("Not connected to OPC UA");
            _client.CallMethod($"ns=2;s=Device {DeviceId}", $"ns=2;s=Device {DeviceId}/ResetErrorStatus");
            Console.WriteLine($"ResetErrorStatus on device {DeviceId}");
            return Task.CompletedTask;
        }

        public Task SetProductionRateAsync(int rate)
        {
            if (_client == null) throw new InvalidOperationException("Not connected to OPC UA");
            _client.WriteNode($"ns=2;s=Device {DeviceId}/ProductionRate", rate);
            Console.WriteLine($"ProductionRate set to {rate}% on device {DeviceId}");
            return Task.CompletedTask;
        }
    }
}