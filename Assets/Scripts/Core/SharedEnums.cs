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
        Shock
    }

    public enum TileSpecialty
    {
        None,
        Cursed,
        Blessed,
        Greed
    }

  
}
