// ============================================================================
// ETD.Data - SharedEnums.cs
// Enums shared across multiple assemblies. Lives in Data to avoid
// circular dependencies between Core and Data.
// ============================================================================

namespace ETD.Core
{
    public enum StatusEffectType
    {
        None,
        Slow,
        Freeze,
        Burn,
        ChainLightning,
        HPPercentReduce,
        Shock,
        ArmorBreak,

        // v1.0 Phase 3 - appended, do not reorder. See [[etd-v1-full-release]].
        // Weaken: Void turret's signature status. Reduces the target's effective
        // Armor for its duration (generic timed value, consumed in
        // EnemyController.ApplyMitigation — no new tick/DoT infrastructure needed).
        Weaken,

        // Poison: Toxin turret's signature status. Identity/VFX marker only (mirrors
        // Laser's HPPercentReduce) — the actual damage is a direct TakePureDamage call
        // in TurretController.Attack(), not carried through this status's value.
        Poison,

        // Expose: Railgun turret's signature status. Generic timed value (like
        // Weaken) that multiplies ALL mitigated damage taken (consumed as the final
        // step in EnemyController.ApplyMitigation, after affinity/armor). Does not
        // affect pure/true damage sources, which already bypass mitigation entirely.
        Expose
    }

    public enum TileSpecialty
    {
        None,
        Cursed,
        Blessed,
        Greed
    }

  
}
