// ============================================================================
// ETD.Data - EnemyData.cs  [UPDATED - weight + unlock wave]
// ============================================================================
using System;
using UnityEngine;

namespace ETD.Data
{
    public enum EnemyType
    {
        Basic,
        Tank,
        Stealth,
        Berserk,
        Splitter,
        Sprinter,
        Debuffer,
        Buffer,

        // v1.0 Phase 3 - appended, do not reorder. See [[etd-v1-full-release]].
        Regenerator,

        // Flying - appended, do not reorder. Ignores the walking path entirely and
        // flies straight from the entry to the exit, so it covers far less ground
        // and spends much less time inside turret range. Balanced by being fragile.
        Flying
    }

    public enum EnemyTier
    {
        Normal,
        Elite,
        Boss
    }

    /// <summary>
    /// Resistance (or, if negative, weakness) this enemy has against a specific
    /// damage-dealing turret type. Part of the v1.0 turret-type affinity system —
    /// see [[etd-v1-full-release]]. Only the damage-dealing turret types
    /// (Basic/Frost/Inferno/Laser/Lightning, + future damage turrets) are meaningful
    /// here; Support/Radar never deal damage so an entry for them is a no-op.
    /// </summary>
    [System.Serializable]
    public struct TurretTypeAffinity
    {
        public TurretType Type;
        [Range(-1f, 1f)]
        [Tooltip("Positive = damage reduction from this turret type. Negative = weakness (bonus damage taken).")]
        public float ResistancePercent;
    }

    [CreateAssetMenu(fileName = "New Enemy", menuName = "ETD/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        public EnemyType Type;
        public Sprite Icon;
        public GameObject Prefab;
        public float yOffset;

        [Header("Base Stats")]
        public float MaxHealth = 100f;
        public float MoveSpeed = 2f;
        [Tooltip("Percentage damage reduction (0.1 = 10% less damage taken from all sources except pure/true damage). Converted from the old flat-subtraction value during the v1.0 mitigation rework — first-pass values, expect a playtesting/retuning pass.")]
        [Range(0f, 0.9f)]
        public float Armor = 0f;
        [Tooltip("Per-turret-type resistance/weakness. Empty = no affinity either way.")]
        public TurretTypeAffinity[] Affinities = System.Array.Empty<TurretTypeAffinity>();
        public int DamageToPlayer = 1;

        [Header("Rewards")]
        public float XPReward = 10f;
        public int GoldReward = 5;

        [Header("Wave Budget")]
        [Tooltip("How much of the wave's weight budget this enemy costs to spawn.")]
        public int SpawnWeight = 10;

        [Tooltip("This enemy will NOT appear before this wave number. Wave 1 = available from start.")]
        [Min(1)]
        public int UnlockWave = 1;

        [Tooltip("Optional: enemy disappears from the pool after this wave. 0 = never.")]
        public int MaxWave = 0;

        [Header("Localization")]
        [Tooltip("Base key used for all localized fields. E.g. 'trait_tr001'")]
        public string LocalizationKey;

        [Header("Type-Specific")]
        [Tooltip("Berserk: speed multiplier at 0 HP")]
        public float BerserkMaxSpeedMultiplier = 2.5f;
        [Tooltip("Splitter: how many split on death")]
        public int SplitCount = 2;
        [Tooltip("Buffer: % increase to nearby enemies")]
        public float BuffPercent = 0.15f;
        [Tooltip("Buffer/Debuffer: effect radius")]
        public float AuraRadius = 3f;
        [Tooltip("Debuffer: turret stat reduction %")]
        public float DebuffPercent = 0.2f;
        [Tooltip("Regenerator: fraction of MAX HP regenerated per second (0.02 = 2%/s). Punishes low-sustained-DPS builds and rewards burst/true damage (Toxin, Railgun).")]
        public float RegenPercentPerSecond = 0.02f;

        [Header("Scaling")]
        [Tooltip("Per-wave HP growth rate at wave 0. 1.06 = +6% per wave early game.")]
        public float HealthGrowthBase = 1.06f;

        [Tooltip("How much extra growth rate is added per wave. 0.0008 means the per-wave growth slowly ramps from +6% toward the cap, so late waves accelerate while early waves stay approachable.")]
        public float HealthGrowthRampPerWave = 0.0008f;

        [Tooltip("Maximum extra growth added by the ramp. 0.06 caps the per-wave growth at HealthGrowthBase + 0.06 (i.e. +12% per wave from ~wave 75 on). The ramp — not a fixed exponent — is what eventually ends a run.")]
        public float HealthGrowthRampCap = 0.06f;

        public float SpeedScalePerWave = 1.002f;

        [Header("Runtime Safety")]
        [Tooltip("Hard cap for scaled runtime HP. Prevents float overflow at very high debug/endless waves and avoids Canvas NaN/AABB errors in health bars.")]
        public float MaxScaledHealthCap = 1e30f;

        [Tooltip("Hard cap for scaled runtime move speed. Prevents invalid movement if endless wave numbers become extremely high.")]
        public float MaxScaledSpeedCap = 40f;

        [Tooltip("If true, this enemy ONLY spawns as elite tier. Never in normal pool.")]
        public bool IsEliteOnly = false;

        [Tooltip("If true, this enemy ONLY spawns as boss tier. Never in normal pool.")]
        public bool IsBossOnly = false;

        public float GetScaledHealth(int waveNumber, EnemyTier tier)
        {
            // Ramping-exponent curve: HP(w) = base * (B + avgRamp(w))^w, where the
            // per-wave growth rate itself climbs from HealthGrowthBase toward
            // HealthGrowthBase + HealthGrowthRampCap. Early waves grow ~+6%/wave
            // (approachable, fast pacing), late waves ~+12%/wave (the run must end).
            // avgRamp is the average of min(cap, ramp*i) over waves 1..w, so the
            // curve is smooth, monotonic, and O(1) to evaluate.
            //
            // Use double for the exponential calculation, then clamp back to float.
            // Very high endless waves can push elite HP beyond float range;
            // Infinity/Infinity health ratios create NaN UI fill values, which Unity
            // Canvas reports as "Invalid AABB in AABB".
            int safeWave = Mathf.Max(0, waveNumber);
            double baseHealth = Math.Max(1.0, MaxHealth);
            double growth = GetHealthGrowthForWave(safeWave);
            double hp = baseHealth * Math.Pow(growth, safeWave);

            switch (tier)
            {
                case EnemyTier.Elite:
                    hp *= 5.0;
                    break;
                case EnemyTier.Boss:
                    hp *= 25.0;
                    break;
            }

            return ClampFiniteToFloat(hp, 1f, GetSafeHealthCap());
        }

        /// <summary>
        /// Effective per-wave growth factor used as the exponent base at the given
        /// wave: HealthGrowthBase plus the historical average of the capped ramp.
        /// Exposed so tools (balance simulator) can plot the same curve the game uses.
        /// </summary>
        public double GetHealthGrowthForWave(int waveNumber)
        {
            double baseGrowth = Math.Max(1.0, HealthGrowthBase);
            double ramp = Math.Max(0.0, HealthGrowthRampPerWave);
            double cap = Math.Max(0.0, HealthGrowthRampCap);

            if (waveNumber <= 0 || ramp <= 0.0 || cap <= 0.0)
                return baseGrowth;

            double capWave = cap / ramp; // wave at which the ramp saturates
            double avgRamp;

            if (waveNumber <= capWave)
            {
                // Average of ramp*i for i = 1..w
                avgRamp = ramp * (waveNumber + 1) * 0.5;
            }
            else
            {
                // Ramping portion (1..capWave) plus saturated portion (capWave..w)
                double rampSum = cap * capWave * 0.5;
                double flatSum = cap * (waveNumber - capWave);
                avgRamp = (rampSum + flatSum) / waveNumber;
            }

            return baseGrowth + Math.Min(cap, avgRamp);
        }

        public float GetScaledSpeed(int waveNumber)
        {
            int safeWave = Mathf.Max(0, waveNumber);
            double speed = Math.Max(0.01, MoveSpeed) * Math.Pow(Math.Max(0.0001, SpeedScalePerWave), safeWave);
            return ClampFiniteToFloat(speed, 0.01f, GetSafeSpeedCap());
        }

        public float GetSafeHealthCap()
        {
            if (float.IsNaN(MaxScaledHealthCap) || float.IsInfinity(MaxScaledHealthCap) || MaxScaledHealthCap <= 0f)
                return 1e30f;

            return Mathf.Clamp(MaxScaledHealthCap, 1f, 1e30f);
        }

        public float GetSafeSpeedCap()
        {
            if (float.IsNaN(MaxScaledSpeedCap) || float.IsInfinity(MaxScaledSpeedCap) || MaxScaledSpeedCap <= 0f)
                return 40f;

            return Mathf.Clamp(MaxScaledSpeedCap, 0.01f, 200f);
        }

        private static float ClampFiniteToFloat(double value, float min, float max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return max;

            if (value <= min)
                return min;

            if (value >= max)
                return max;

            return (float)value;
        }

        /// <summary>Is this enemy available for the given wave number?</summary>
        public bool IsAvailableAt(int wave)
        {
            if (wave < UnlockWave) return false;
            if (MaxWave > 0 && wave > MaxWave) return false;
            return true;
        }
    }
}
