using System;

namespace IndustrialIoT.Functions.Models
{
    public class TelemetryMessage
    {
        public int DeviceId { get; set; }
        public ProductionStatus ProductionStatus { get; set; }
        public Guid? WorkorderId { get; set; }
        public long GoodCount { get; set; }
        public long BadCount { get; set; }
        public double Temperature { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DeviceErrorMessage
    {
        public int DeviceId { get; set; }
        public DeviceErrors ErrorFlags { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DeviceErrorEvent
    {
        public int DeviceId { get; set; }
        public string ErrorType { get; set; } = string.Empty;
        public DeviceErrors ErrorFlags { get; set; }
        public int ErrorCode => (int)ErrorFlags;
        public DateTime Timestamp { get; set; }
    }

    public class ProductionKpiMessage
    {
        public int DeviceId { get; set; }
        public double GoodProductionPercentage { get; set; }
        public long TotalGoodCount { get; set; }
        public long TotalBadCount { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
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

    [Flags]
    public enum ProductionStatus
    {
        Stopped = 0,
        Running = 1
    }
}
