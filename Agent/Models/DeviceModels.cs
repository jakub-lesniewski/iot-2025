using System;

namespace IndustrialIoT.Agent.Models
{
    public class TelemetryData
    {
        public int DeviceId { get; set; }
        public ProductionStatus ProductionStatus { get; set; }
        public Guid WorkorderId { get; set; }
        public long GoodCount { get; set; }
        public long BadCount { get; set; }
        public double Temperature { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DeviceState
    {
        public int DeviceId { get; set; }
        public int ProductionRate { get; set; }
        public DeviceErrors DeviceErrors { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public enum ProductionStatus
    {
        Stopped = 0,
        Running = 1
    }

    [Flags]
    public enum DeviceErrors
    {
        None = 0,
        EmergencyStop = 1,
        PowerFailure = 2,
        SensorFailure = 4,
        Unknown = 8
    }
}
