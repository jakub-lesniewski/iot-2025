using System;

namespace IndustrialIoT.Agent
{
    public static class Settings
    {
        public static string IotHubConnection { get; set; } = null!;
        public static string OpcUaEndpoint { get; set; } = null!;
        public static int DeviceNumber { get; set; }
        public static int TelemetryIntervalMs { get; set; }
    }
}
