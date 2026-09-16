using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Simulator
{
    public class DeviceTelemetry
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonIgnoreIfDefault]
        [BsonIgnoreIfNull]
        public string? Id { get; set; }

        public int DeviceId { get; set; }
        public string DeviceStatus { get; set; } = "Active";
        public string FirmwareVersion { get; set; } = "v2.1.4";

        public NetworkInfo Network { get; set; } = new();
        public GpsInfo Gps { get; set; } = new();
        public EngineInfo Engine { get; set; } = new();
        public VehicleState State { get; set; } = new();

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class NetworkInfo
    {
        public string ImeiNumber { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int SignalStrengthDbm { get; set; }
        public string ConnectionType { get; set; } = "4G";
        public string NetworkProvider { get; set; } = "GlobalNet";
    }

    public class GpsInfo
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Altitude { get; set; }
        public int SpeedKmh { get; set; }
        public int HeadingDegrees { get; set; }
        public double GpsAccuracy { get; set; }
        public int SatelliteCount { get; set; }
    }

    public class EngineInfo
    {
        public int EngineRpm { get; set; }
        public int EngineTemperatureC { get; set; }
        public int FuelLevelPercent { get; set; }
        public int OdometerKm { get; set; }
        public double BatteryVoltage { get; set; }
        public double TirePressureBar { get; set; }
    }

    public class VehicleState
    {
        public bool IsEngineRunning { get; set; }
        public int CabinTemperatureC { get; set; }
        public bool IsDoorOpen { get; set; }
        public bool IsTrunkOpen { get; set; }
        public bool IsSeatbeltFastened { get; set; }
        public bool HarshBrakingEvent { get; set; }
        public bool HarshAccelerationEvent { get; set; }
    }
}