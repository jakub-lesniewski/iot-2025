using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using IndustrialIoT.Agent;
using IndustrialIoT.Agent.Models;


namespace IndustrialIoT.Agent.Services
{
    public class CloudConnector
    {
        private readonly DeviceClient _client;
        private PlcConnector? _plc;
        private DeviceState? _previousState;

        public CloudConnector(DeviceClient deviceClient)
        {
            _client = deviceClient;
        }

        public void BindConnector(PlcConnector opc)
        {
            _plc = opc;
        }

        public async Task RegisterHandlersAsync()
        {
            await _client.SetReceiveMessageHandlerAsync(OnCloudMessageAsync, null);
            await _client.SetMethodHandlerAsync("EmergencyStop", OnEmergencyStopAsync, null);
            await _client.SetMethodHandlerAsync("ResetErrorStatus", OnResetErrorsAsync, null);
            await _client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesAsync, null);
            Console.WriteLine("CloudConnector: Handlery zarejestrowane.");
        }

        public async Task SendTelemetryAsync(TelemetryData data)
        {
            var payload = JsonConvert.SerializeObject(new
            {
                data.DeviceId,
                data.ProductionStatus,
                data.WorkorderId,
                data.GoodCount,
                data.BadCount,
                data.Temperature,
                data.Timestamp
            });

            var message = new Message(Encoding.UTF8.GetBytes(payload))
            {
                ContentType = MediaTypeNames.Application.Json,
                ContentEncoding = "utf-8"
            };
            message.Properties.Add("messageType", "telemetry");

            await _client.SendEventAsync(message);
            Console.WriteLine("Telemetria wysłana.");
        }

        public async Task UpdateTwinAsync(DeviceState state)
        {
            bool changed = _previousState == null
                || _previousState.ProductionRate != state.ProductionRate
                || _previousState.DeviceErrors != state.DeviceErrors;

            if (!changed) return;

            var patch = new TwinCollection
            {
                ["productionRate"] = state.ProductionRate,
                ["deviceErrors"] = state.DeviceErrors.ToString(),
                ["lastUpdated"] = state.LastUpdated
            };

            await _client.UpdateReportedPropertiesAsync(patch);
            _previousState = state;

            Console.WriteLine($"Twin zaktualizowany: Rate={state.ProductionRate}, Errors={state.DeviceErrors}");
        }

        private async Task OnCloudMessageAsync(Message msg, object? ctx)
        {
            var content = Encoding.UTF8.GetString(msg.GetBytes());
            Console.WriteLine($"C2D: {content}");
            await _client.CompleteAsync(msg);
        }

        private async Task<MethodResponse> OnEmergencyStopAsync(MethodRequest req, object? ctx)
        {
            Console.WriteLine("Direct Method: EmergencyStop");
            if (_plc != null) await _plc.TriggerEmergencyStopAsync();
            var resp = new { success = true };
            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(resp)), 200);
        }

        private async Task<MethodResponse> OnResetErrorsAsync(MethodRequest req, object? ctx)
        {
            Console.WriteLine("Direct Method: ResetErrorStatus");
            if (_plc != null) await _plc.ResetErrorsAsync();
            var resp = new { success = true };
            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(resp)), 200);
        }

        private async Task OnDesiredPropertiesAsync(TwinCollection desired, object? ctx)
        {
            if (desired.Contains("productionRate") && _plc != null)
            {
                var rate = Convert.ToInt32(desired["productionRate"]);
                await _plc.SetProductionRateAsync(rate);
                Console.WriteLine($"Desired Property: productionRate -> {rate}%");
            }
        }
    }
}

