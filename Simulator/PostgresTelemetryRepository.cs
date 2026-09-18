using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;

namespace Simulator
{
    public class PostgresTelemetryRepository : ITelemetryRepository
    {

        public async Task BulkUpsertTelemetryAsync(List<DeviceTelemetry> batch)
{
    if (batch == null || batch.Count == 0) return;

    foreach (var item in batch)
    {
        await UpsertTelemetryAsync(item);
    }
}

        private readonly string _connectionString;

        public PostgresTelemetryRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task InitializeAsync()
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            string createTableSql = @"
            CREATE TABLE IF NOT EXISTS Telemetries (
                DeviceId INT PRIMARY KEY,
                DeviceStatus TEXT,
                FirmwareVersion TEXT,
                Latitude DOUBLE PRECISION,
                Longitude DOUBLE PRECISION,
                Altitude INT,
                SpeedKmh INT,
                HeadingDegrees INT,
                GpsAccuracy DOUBLE PRECISION,
                SatelliteCount INT,
                ImeiNumber TEXT,
                IpAddress TEXT,
                SignalStrengthDbm INT,
                ConnectionType TEXT,
                NetworkProvider TEXT,
                EngineRpm INT,
                EngineTemperatureC INT,
                FuelLevelPercent INT,
                OdometerKm INT,
                BatteryVoltage DOUBLE PRECISION,
                TirePressureBar DOUBLE PRECISION,
                IsEngineRunning BOOLEAN,
                CabinTemperatureC INT,
                IsDoorOpen BOOLEAN,
                IsTrunkOpen BOOLEAN,
                IsSeatbeltFastened BOOLEAN,
                HarshBrakingEvent BOOLEAN,
                HarshAccelerationEvent BOOLEAN,
                CreatedAt TIMESTAMPTZ,
                UpdatedAt TIMESTAMPTZ
            );";

            await using var cmd = new NpgsqlCommand(createTableSql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpsertTelemetryAsync(DeviceTelemetry t)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
    
            string upsertSql = @"
            INSERT INTO Telemetries (
                DeviceId, DeviceStatus, FirmwareVersion, Latitude, Longitude, Altitude,
                SpeedKmh, HeadingDegrees, GpsAccuracy, SatelliteCount, ImeiNumber, IpAddress,
                SignalStrengthDbm, ConnectionType, NetworkProvider, EngineRpm, EngineTemperatureC,
                FuelLevelPercent, OdometerKm, BatteryVoltage, TirePressureBar, IsEngineRunning,
                CabinTemperatureC, IsDoorOpen, IsTrunkOpen, IsSeatbeltFastened, HarshBrakingEvent,
                HarshAccelerationEvent, CreatedAt, UpdatedAt
            ) VALUES (
                @DeviceId, @DeviceStatus, @FirmwareVersion, @Latitude, @Longitude, @Altitude,
                @SpeedKmh, @HeadingDegrees, @GpsAccuracy, @SatelliteCount, @ImeiNumber, @IpAddress,
                @SignalStrengthDbm, @ConnectionType, @NetworkProvider, @EngineRpm, @EngineTemperatureC,
                @FuelLevelPercent, @OdometerKm, @BatteryVoltage, @TirePressureBar, @IsEngineRunning,
                @CabinTemperatureC, @IsDoorOpen, @IsTrunkOpen, @IsSeatbeltFastened, @HarshBrakingEvent,
                @HarshAccelerationEvent, @CreatedAt, @UpdatedAt
            )
            ON CONFLICT (DeviceId) DO UPDATE SET
                DeviceStatus = EXCLUDED.DeviceStatus,
                FirmwareVersion = EXCLUDED.FirmwareVersion,
                Latitude = EXCLUDED.Latitude,
                Longitude = EXCLUDED.Longitude,
                Altitude = EXCLUDED.Altitude,
                SpeedKmh = EXCLUDED.SpeedKmh,
                HeadingDegrees = EXCLUDED.HeadingDegrees,
                GpsAccuracy = EXCLUDED.GpsAccuracy,
                SatelliteCount = EXCLUDED.SatelliteCount,
                ImeiNumber = EXCLUDED.ImeiNumber,
                IpAddress = EXCLUDED.IpAddress,
                SignalStrengthDbm = EXCLUDED.SignalStrengthDbm,
                ConnectionType = EXCLUDED.ConnectionType,
                NetworkProvider = EXCLUDED.NetworkProvider,
                EngineRpm = EXCLUDED.EngineRpm,
                EngineTemperatureC = EXCLUDED.EngineTemperatureC,
                FuelLevelPercent = EXCLUDED.FuelLevelPercent,
                OdometerKm = EXCLUDED.OdometerKm,
                BatteryVoltage = EXCLUDED.BatteryVoltage,
                TirePressureBar = EXCLUDED.TirePressureBar,
                IsEngineRunning = EXCLUDED.IsEngineRunning,
                CabinTemperatureC = EXCLUDED.CabinTemperatureC,
                IsDoorOpen = EXCLUDED.IsDoorOpen,
                IsTrunkOpen = EXCLUDED.IsTrunkOpen,
                IsSeatbeltFastened = EXCLUDED.IsSeatbeltFastened,
                HarshBrakingEvent = EXCLUDED.HarshBrakingEvent,
                HarshAccelerationEvent = EXCLUDED.HarshAccelerationEvent,
                UpdatedAt = EXCLUDED.UpdatedAt;";

            await using var cmd = new NpgsqlCommand(upsertSql, conn);
            cmd.Parameters.AddWithValue("DeviceId", t.DeviceId);
            cmd.Parameters.AddWithValue("DeviceStatus", t.DeviceStatus);
            cmd.Parameters.AddWithValue("FirmwareVersion", t.FirmwareVersion);
            cmd.Parameters.AddWithValue("Latitude", t.Gps.Latitude);
            cmd.Parameters.AddWithValue("Longitude", t.Gps.Longitude);
            cmd.Parameters.AddWithValue("Altitude", t.Gps.Altitude);
            cmd.Parameters.AddWithValue("SpeedKmh", t.Gps.SpeedKmh);
            cmd.Parameters.AddWithValue("HeadingDegrees", t.Gps.HeadingDegrees);
            cmd.Parameters.AddWithValue("GpsAccuracy", t.Gps.GpsAccuracy);
            cmd.Parameters.AddWithValue("SatelliteCount", t.Gps.SatelliteCount);
            cmd.Parameters.AddWithValue("ImeiNumber", t.Network.ImeiNumber);
            cmd.Parameters.AddWithValue("IpAddress", t.Network.IpAddress);
            cmd.Parameters.AddWithValue("SignalStrengthDbm", t.Network.SignalStrengthDbm);
            cmd.Parameters.AddWithValue("ConnectionType", t.Network.ConnectionType);
            cmd.Parameters.AddWithValue("NetworkProvider", t.Network.NetworkProvider);
            cmd.Parameters.AddWithValue("EngineRpm", t.Engine.EngineRpm);
            cmd.Parameters.AddWithValue("EngineTemperatureC", t.Engine.EngineTemperatureC);
            cmd.Parameters.AddWithValue("FuelLevelPercent", t.Engine.FuelLevelPercent);
            cmd.Parameters.AddWithValue("OdometerKm", t.Engine.OdometerKm);
            cmd.Parameters.AddWithValue("BatteryVoltage", t.Engine.BatteryVoltage);
            cmd.Parameters.AddWithValue("TirePressureBar", t.Engine.TirePressureBar);
            cmd.Parameters.AddWithValue("IsEngineRunning", t.State.IsEngineRunning);
            cmd.Parameters.AddWithValue("CabinTemperatureC", t.State.CabinTemperatureC);
            cmd.Parameters.AddWithValue("IsDoorOpen", t.State.IsDoorOpen);
            cmd.Parameters.AddWithValue("IsTrunkOpen", t.State.IsTrunkOpen);
            cmd.Parameters.AddWithValue("IsSeatbeltFastened", t.State.IsSeatbeltFastened);
            cmd.Parameters.AddWithValue("HarshBrakingEvent", t.State.HarshBrakingEvent);
            cmd.Parameters.AddWithValue("HarshAccelerationEvent", t.State.HarshAccelerationEvent);
            cmd.Parameters.AddWithValue("CreatedAt", t.CreatedAt);
            cmd.Parameters.AddWithValue("UpdatedAt", t.UpdatedAt);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<DeviceTelemetry>> GetTelemetriesAsync(int startId, int endId)
        {
            var list = new List<DeviceTelemetry>();
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = "SELECT * FROM Telemetries WHERE DeviceId BETWEEN @Start AND @End ORDER BY DeviceId LIMIT 1000;";
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("Start", startId);
            cmd.Parameters.AddWithValue("End", endId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DeviceTelemetry
                {
                    DeviceId = reader.GetInt32(reader.GetOrdinal("DeviceId")),
                    DeviceStatus = reader.GetString(reader.GetOrdinal("DeviceStatus")),
                    FirmwareVersion = reader.GetString(reader.GetOrdinal("FirmwareVersion")),
                    Gps = new GpsInfo
                    {
                        Latitude = reader.GetDouble(reader.GetOrdinal("Latitude")),
                        Longitude = reader.GetDouble(reader.GetOrdinal("Longitude")),
                        Altitude = reader.GetInt32(reader.GetOrdinal("Altitude")),
                        SpeedKmh = reader.GetInt32(reader.GetOrdinal("SpeedKmh")),
                        HeadingDegrees = reader.GetInt32(reader.GetOrdinal("HeadingDegrees")),
                        GpsAccuracy = reader.GetDouble(reader.GetOrdinal("GpsAccuracy")),
                        SatelliteCount = reader.GetInt32(reader.GetOrdinal("SatelliteCount"))
                    },
                    Network = new NetworkInfo
                    {
                        ImeiNumber = reader.GetString(reader.GetOrdinal("ImeiNumber")),
                        IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
                        SignalStrengthDbm = reader.GetInt32(reader.GetOrdinal("SignalStrengthDbm")),
                        ConnectionType = reader.GetString(reader.GetOrdinal("ConnectionType")),
                        NetworkProvider = reader.GetString(reader.GetOrdinal("NetworkProvider"))
                    },
                    Engine = new EngineInfo
                    {
                        EngineRpm = reader.GetInt32(reader.GetOrdinal("EngineRpm")),
                        EngineTemperatureC = reader.GetInt32(reader.GetOrdinal("EngineTemperatureC")),
                        FuelLevelPercent = reader.GetInt32(reader.GetOrdinal("FuelLevelPercent")),
                        OdometerKm = reader.GetInt32(reader.GetOrdinal("OdometerKm")),
                        BatteryVoltage = reader.GetDouble(reader.GetOrdinal("BatteryVoltage")),
                        TirePressureBar = reader.GetDouble(reader.GetOrdinal("TirePressureBar"))
                    },
                    State = new VehicleState
                    {
                        IsEngineRunning = reader.GetBoolean(reader.GetOrdinal("IsEngineRunning")),
                        CabinTemperatureC = reader.GetInt32(reader.GetOrdinal("CabinTemperatureC")),
                        IsDoorOpen = reader.GetBoolean(reader.GetOrdinal("IsDoorOpen")),
                        IsTrunkOpen = reader.GetBoolean(reader.GetOrdinal("IsTrunkOpen")),
                        IsSeatbeltFastened = reader.GetBoolean(reader.GetOrdinal("IsSeatbeltFastened")),
                        HarshBrakingEvent = reader.GetBoolean(reader.GetOrdinal("HarshBrakingEvent")),
                        HarshAccelerationEvent = reader.GetBoolean(reader.GetOrdinal("HarshAccelerationEvent"))
                    },
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
                });
            }
            return list;
        }

        public async Task ResetDatabaseAsync()
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("TRUNCATE TABLE Telemetries;", conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}