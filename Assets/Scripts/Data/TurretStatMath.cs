// ============================================================================
// ETD.Data - TurretStatMath.cs  [NEW]
// Pure, allocation-free stat formulas shared by live gameplay and the in-game
// wiki. Every method takes all of its inputs as parameters and touches no scene
// state, so a Hub screen with no RunManager can compute the exact same numbers
// a placed turret would produce.
//
// These were previously private instance methods on TurretController, which made
// them unreachable from the Hub. Behavior is unchanged: TurretController now
// delegates and passes its own serialized tuning values in.
// ============================================================================
using UnityEngine;

namespace ETD.Data
{
    public static class TurretStatMath
    {
        /// <summary>
        /// Per-level compounding damage growth, as a fraction (0.2 = +20%/level).
        /// DamagePerLevel above 1 is interpreted as legacy flat damage and converted
        /// against the base; at or below 1 it is already a percent. Always clamped.
        /// </summary>
        public static float DamageGrowthPercentPerLevel(
            float baseDamage,
            float damagePerLevel,
            float maxGrowthPerLevel = BalanceConstants.MaxDamageGrowthPerLevel)
        {
            if (damagePerLevel <= 0f)
                return 0f;

            // Legacy assets authored flat damage (base 10, DamagePerLevel 2 -> +20%/level).
            // Newer assets author the percent directly (0.08 -> +8%/level).
            float bonus = damagePerLevel > 1f
                ? damagePerLevel / Mathf.Max(1f, baseDamage)
                : damagePerLevel;

            // A prefab that deserializes the cap field as 0 must still scale, so the
            // caller's non-positive cap falls back to the intended default.
            float maxGrowth = maxGrowthPerLevel > 0f
                ? maxGrowthPerLevel
                : BalanceConstants.MaxDamageGrowthPerLevel;

            return Mathf.Clamp(bonus, 0f, maxGrowth);
        }

        /// <summary>
        /// Damage after upgrade levels. Compounding, so turret upgrades keep pace with
        /// enemy health, which also scales as Pow(HealthScalePerWave, wave).
        /// </summary>
        public static float LeveledDamage(
            float baseDamage,
            float damagePerLevel,
            int levelIndex,
            float maxGrowthPerLevel = BalanceConstants.MaxDamageGrowthPerLevel)
        {
            baseDamage = Mathf.Max(0f, baseDamage);
            if (levelIndex <= 0 || damagePerLevel <= 0f)
                return baseDamage;

            float perLevelBonus = DamageGrowthPercentPerLevel(baseDamage, damagePerLevel, maxGrowthPerLevel);
            return baseDamage * Mathf.Pow(1f + perLevelBonus, levelIndex);
        }

        /// <summary>Attack interval after level scaling, floored so cooldowns never reach zero.</summary>
        public static float LeveledAttackInterval(
            float baseInterval,
            float attackSpeedPerLevel,
            int levelIndex,
            float minInterval = BalanceConstants.MinProjectileAttackInterval)
        {
            return Mathf.Max(minInterval, baseInterval - attackSpeedPerLevel * Mathf.Max(0, levelIndex));
        }

        /// <summary>Range after level scaling. Linear, unlike damage.</summary>
        public static float LeveledRange(float baseRange, float rangePerLevel, int levelIndex)
        {
            return baseRange + rangePerLevel * Mathf.Max(0, levelIndex);
        }

        /// <summary>
        /// Multiplier applied to a turret's IDENTITY effects (Frost slow strength and
        /// duration, Inferno's flat burn floor, armor break, ...) so upgrading improves
        /// what makes the turret special, not just raw damage.
        /// </summary>
        public static float IdentityMultiplier(
            int levelIndex,
            float perLevel = BalanceConstants.IdentityScalingPerLevel,
            float cap = BalanceConstants.IdentityScalingCap)
        {
            return 1f + Mathf.Min(
                Mathf.Max(0f, cap),
                Mathf.Max(0f, perLevel) * Mathf.Max(0, levelIndex));
        }

        /// <summary>
        /// Support damage/attack-speed aura strength. The AuraPower trait multiplier
        /// applies here — note it deliberately does NOT apply to the enemy slow aura.
        /// </summary>
        public static float SupportAuraBonus(
            float baseAura,
            float auraPerLevel,
            int levelIndex,
            float evolutionBonus,
            float auraMultiplier)
        {
            return Mathf.Max(0f, baseAura + auraPerLevel * Mathf.Max(0, levelIndex) + evolutionBonus)
                 * Mathf.Max(0f, auraMultiplier);
        }

        /// <summary>
        /// Suppression Field's enemy slow aura. Unlike the damage/speed auras this is
        /// NOT scaled by the AuraPower trait — only by turret level.
        /// </summary>
        public static float SupportEnemySlow(float baseSlow, float slowPerLevel, int levelIndex)
        {
            return Mathf.Clamp01(baseSlow + slowPerLevel * Mathf.Max(0, levelIndex));
        }

        /// <summary>
        /// Slow-strength bonuses scale the slow AMOUNT, not the enemy's final speed:
        /// a 25% base slow with a +100% bonus becomes a 50% slow, not half speed.
        /// </summary>
        public static float EffectiveSlowStrength(float baseSlow, float slowStrengthMultiplier)
        {
            return baseSlow * Mathf.Max(0f, slowStrengthMultiplier);
        }

        /// <summary>
        /// Enemy speed multiplier for a given slow. Concurrent slow sources take the
        /// strongest rather than summing, so pass the max, not the total.
        /// </summary>
        public static float SlowSpeedMultiplier(
            float slowStrength,
            float minSpeedMultiplier = BalanceConstants.MinSlowSpeedMultiplier)
        {
            if (slowStrength <= 0f)
                return 1f;
            return Mathf.Max(minSpeedMultiplier, 1f - slowStrength);
        }

        /// <summary>
        /// Damage of a chain hop before per-target multipliers. Hop 0 is the primary
        /// target; each further hop compounds the falloff (0.7 -> 70%, 49%, 34.3%, ...).
        /// </summary>
        public static float ChainDamageAtHop(float startDamage, float falloff, int hop)
        {
            if (hop <= 0)
                return startDamage;
            return startDamage * Mathf.Pow(Mathf.Max(0f, falloff), hop);
        }

        /// <summary>
        /// DPS of a single burn stack applied by a hit. The flat BurnDPS from turret
        /// data acts as an early-game floor, so the caller keeps whichever is larger.
        /// </summary>
        public static float BurnDpsFromHit(
            float hitDamage,
            float burnDuration,
            float hitFraction,
            float burnDamageMultiplier)
        {
            if (burnDuration <= 0.01f)
                return 0f;
            return (hitDamage * hitFraction / burnDuration) * burnDamageMultiplier;
        }
    }
}
