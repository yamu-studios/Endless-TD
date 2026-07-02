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
        Buffer
    }

    public enum EnemyTier
    {
        Normal,
        Elite,
        Boss
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
        public float Armor = 0f;
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

        [Header("Scaling")]
        public float HealthScalePerWave = 1.05f;
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
            // Use double for the exponential calculation, then clamp back to float.
            // At waves like 835+, 1.1^wave can push elite HP beyond float range.
            // Infinity/Infinity health ratios create NaN UI fill values, which Unity Canvas
            // reports as "Invalid AABB in AABB".
            int safeWave = Mathf.Max(0, waveNumber);
            double baseHealth = Math.Max(1.0, MaxHealth);
            double scale = Math.Max(0.0001, HealthScalePerWave);
            double hp = baseHealth * Math.Pow(scale, safeWave);

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
