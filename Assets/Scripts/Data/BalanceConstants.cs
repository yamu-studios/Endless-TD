// ============================================================================
// ETD.Data - BalanceConstants.cs  [NEW]
// Single source of truth for tuning values that the in-game wiki has to quote
// but that previously existed only as [SerializeField] defaults on the turret
// prefab or as private consts inside gameplay classes.
//
// These are the DEFAULTS. Live gameplay still reads the serialized field on the
// instance, so a prefab override wins at runtime — see TurretController's
// "Attack Speed Safety" / "Damage Scaling" / "Burn Scaling" headers, whose field
// initializers now reference these constants so the two cannot silently diverge.
// ============================================================================
namespace ETD.Data
{
    public static class BalanceConstants
    {
        // --- Attack speed safety (TurretController) ---

        /// <summary>Lowest allowed projectile attack interval after level scaling.</summary>
        public const float MinProjectileAttackInterval = 0.12f;

        /// <summary>Hard cap on non-laser attacks per second after all buffs.</summary>
        public const float MaxProjectileAttackSpeed = 8f;

        // --- Damage scaling ---

        /// <summary>Caps compounding damage growth per turret upgrade level (+25%/level).</summary>
        public const float MaxDamageGrowthPerLevel = 0.25f;

        /// <summary>Base critical hit multiplier. 2 = 200% damage.</summary>
        public const float BaseCritDamageMultiplier = 2f;

        // --- Identity scaling (Frost slow, Inferno burn floor, etc.) ---

        /// <summary>Status-effect strength/duration gained per turret level.</summary>
        public const float IdentityScalingPerLevel = 0.01f;

        /// <summary>Maximum total identity bonus from levels (+50%, reached at level 50).</summary>
        public const float IdentityScalingCap = 0.5f;

        // --- Burn ---

        /// <summary>
        /// One burn stack's total damage as a fraction of the applying hit, spread
        /// over the burn duration. Catalytic Burn adds to this at runtime.
        /// </summary>
        public const float BurnHitPercent = 0.25f;

        /// <summary>Spread-on-death burns inherit this fraction of DPS and duration.</summary>
        public const float BurnSpreadPower = 0.6f;

        /// <summary>Concurrent burn stacks allowed from regular sources.</summary>
        public const int DefaultMaxBurnStacks = 5;

        // --- Blastfire (Inferno Path A) ---

        /// <summary>Fraction of a normal hit splashed to each enemy in the cone.</summary>
        public const float BlastfireAreaDamagePercent = 0.6f;

        // --- Chain ---

        /// <summary>
        /// Chain Overload's bounce-back re-hits the primary target for this fraction
        /// of the secondary hit that triggered it.
        /// </summary>
        public const float ChainBounceBackDamageFraction = 0.5f;

        // --- Slow ---

        /// <summary>
        /// Floor on the slow speed multiplier. Slow alone can never drop an enemy
        /// below 15% of base speed; only Freeze/Shock hard-stop movement.
        /// </summary>
        public const float MinSlowSpeedMultiplier = 0.15f;
    }
}
