// ============================================================================
// ETD.Core - IProjectileSpawnTelemetrySink.cs
// Optional direct sink for high-frequency projectile telemetry.
// Keeps Core independent from the Meta assembly while avoiding EventBus overhead.
// ============================================================================
namespace ETD.Core
{
    public interface IProjectileSpawnTelemetrySink
    {
        void ReportProjectileSpawns(int count);
    }
}
