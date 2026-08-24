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

        // --- Wave composition (WaveManager) ---
        // The wiki has to quote these from the Hub, where no WaveManager exists.

        /// <summary>Spawn budget at wave 0, before any per-wave growth.</summary>
        public const int WaveBaseBudget = 50;

        /// <summary>Linear part of the per-wave spawn budget growth.</summary>
        public const int WaveBudgetPerWave = 12;

        /// <summary>Quadratic part of the budget growth: budget += wave^2 * this.</summary>
        public const float WaveBudgetQuadraticScale = 0.3f;

        /// <summary>
        /// The quadratic enemy-count curve ends here. Past this wave, budget grows by
        /// WaveBudgetPerWaveAfterSoftCap instead, keeping four-digit waves playable.
        /// </summary>
        public const int WaveBudgetSoftCapWave = 700;

        /// <summary>Linear budget added per wave after WaveBudgetSoftCapWave.</summary>
        public const int WaveBudgetPerWaveAfterSoftCap = 200;

        /// <summary>
        /// Enemies with per-wave movement scaling stop gaining speed here. Health and
        /// wave composition continue scaling normally.
        /// </summary>
        public const int EnemySpeedScalingWaveCap = 800;

        /// <summary>Seconds between spawns at wave 0.</summary>
        public const float WaveBaseSpawnInterval = 0.6f;

        /// <summary>Seconds shaved off the spawn interval per wave.</summary>
        public const float WaveSpawnIntervalReduction = 0.005f;

        /// <summary>Floor on the spawn interval, so late waves stop compressing.</summary>
        public const float WaveMinSpawnInterval = 0.2f;

        /// <summary>An elite wave gains one extra elite every this many waves.</summary>
        public const int EliteCountWaveStep = 15;

        /// <summary>Per-wave player bonuses compound through this wave, then grow linearly.</summary>
        public const int PlayerWaveScalingExponentialCap = 500;

        // --- Freeze / Shock (EnemyController.ApplyStatus) ---

        /// <summary>
        /// Per-enemy window after a freeze ends during which it cannot be refrozen.
        /// Without it, a fast turret re-applying a short freeze every hit would freeze-lock
        /// an enemy permanently.
        /// </summary>
        public const float DefaultFreezeImmunity = 4f;

        /// <summary>
        /// Freeze applied to an immune enemy is downgraded to a slow of this strength,
        /// so Frost still contributes during the lockout instead of doing nothing.
        /// </summary>
        public const float FreezeLockoutSlow = 0.5f;

        /// <summary>Duration of the Shock hard-stop rolled per hit by the shock chance bonus.</summary>
        public const float ShockDuration = 0.25f;

        // --- Health decay (RunStatModifiers, Entropy Engine) ---

        /// <summary>
        /// Floor for non-lethal decay, as a fraction of max HP. Decay can never take an
        /// enemy below this, which is what keeps it from being a standalone win condition.
        /// </summary>
        public const float EntropyMinHpFraction = 0.005f;

        // --- Damage mitigation (EnemyController.ApplyMitigation) ---

        /// <summary>
        /// Ceiling on an enemy's effective Armor. Without it a late-wave armor roll
        /// could reach total immunity, which no amount of pierce could dig out of.
        /// </summary>
        public const float MaxArmorMitigation = 0.9f;

        /// <summary>Ceiling on turret-type affinity RESISTANCE (damage reduction).</summary>
        public const float MaxAffinityResistance = 0.9f;

        /// <summary>
        /// Floor on turret-type affinity, i.e. the strongest possible WEAKNESS.
        /// -1 doubles incoming damage from that turret type.
        /// </summary>
        public const float MaxAffinityWeakness = -1f;

        /// <summary>Every mitigated hit lands for at least this much, so armor can stall a build but never fully wall it.</summary>
        public const float MinDamagePerHit = 1f;

        // --- Slow ---

        /// <summary>
        /// Floor on the slow speed multiplier. Slow alone can never drop an enemy
        /// below 15% of base speed; only Freeze/Shock hard-stop movement.
        /// </summary>
        public const float MinSlowSpeedMultiplier = 0.15f;
    }
}
