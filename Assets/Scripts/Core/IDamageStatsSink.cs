// ============================================================================
// ETD.Core - IDamageStatsSink.cs
// Direct low-overhead damage telemetry for end-game stats.
// ============================================================================
namespace ETD.Core
{
    public interface IDamageStatsSink
    {
        void OnDamageDealt(int sourceTurretType, int sourceTurretId, int damageKind, float amount);
    }
}
