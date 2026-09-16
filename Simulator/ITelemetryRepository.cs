using System.Collections.Generic;
using System.Threading.Tasks;

namespace Simulator
{
    public interface ITelemetryRepository
    {
        Task InitializeAsync();
        Task<List<DeviceTelemetry>> GetTelemetriesAsync(int startId, int endId);
        Task ResetDatabaseAsync();

        Task BulkUpsertTelemetryAsync(List<DeviceTelemetry> batch);
    }
}
