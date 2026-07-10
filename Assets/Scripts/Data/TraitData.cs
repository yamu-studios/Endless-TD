// ============================================================================
// ETD.Data - TraitData.cs  [UPDATED - matches ETD_FULL_SHEET]
// ============================================================================
using UnityEngine;

namespace ETD.Data
{
    [CreateAssetMenu(fileName = "New Trait", menuName = "ETD/Trait Data")]
    public class TraitData : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public bool IsUnlockedByDefault;
        [TextArea] public string UnlockCondition;
        public SpecCardRarity Grade;

        [Header("Localization")]
        [Tooltip("Base key used for all localized fields. E.g. 'trait_tr001'")]
        public string LocalizationKey;

        [Header("Category")]
        public TraitCategory Category;



        [Header("Effect")]
        public TraitEffectType EffectType;
        public float EffectValue;

        [Tooltip("Bonus per shop upgrade level as a FRACTION of the base effect " +
                 "(0.15 = +15% of base per level). Leave 0 to use the default +15%/level. " +
                 "Shop sells up to 10 levels, so the default maxes at +150% effect.")]
        public float UpgradedEffectValue;

        /// <summary>
        /// Effect value including permanent shop upgrades (SaveData.TraitUpgradeLevels).
        /// Multiplicative-of-base so it works for both fraction-style values (0.01)
        /// and whole-percent-style values (15) without knowing the unit.
        /// </summary>
        public float GetEffectiveValue(int upgradeLevel)
        {
            if (upgradeLevel <= 0) return EffectValue;
            float perLevel = UpgradedEffectValue > 0f ? UpgradedEffectValue : 0.15f;
            return EffectValue * (1f + perLevel * upgradeLevel);
        }

        /// <summary>Keystone Covenants are mutually exclusive — only one per run.</summary>
        public bool IsKeystone =>
            EffectType == TraitEffectType.FlameCovenant ||
            EffectType == TraitEffectType.FrostCovenant ||
            EffectType == TraitEffectType.StormCovenant;

        [Header("Timed Effects (Legendary traits)")]
        public float EffectDuration;
        public float EffectInterval;



        [Header("Unlock Progress Tracking")]
        [Tooltip("Which condition type tracks progress toward this unlock. " +
                 "Must match what ChallengeTracker tracks (e.g. BuildTurrets, ReachWave).")]
        public ChallengeConditionType UnlockConditionType;

        [Tooltip("The target value for the unlock condition (e.g. 50 for 'Build 50 turrets'). " +
                 "Used to display progress. If 0, no progress is shown.")]
        public float UnlockConditionTarget;
        public Color GetRarityColor() => Grade.GetRarityColor();
    }

    public enum TraitCategory
    {
        Global,
        Economy,
        Frost,
        Lightning,
        Inferno,
        Laser,
        Support,
        Boss,
        Control,
        Burst,
        AntiTank,
        Swarm
    }

    public enum TraitEffectType
    {
        // Common (TR001-TR004)
        BonusRange,
        ReduceUpgradeCost,
        BonusAttackSpeed,
        BonusDamage,
        BonusEXP,

        // Uncommon (TR005-TR007)
        SlowStrength,
        ChainTargetBonus,
        BurnDuration,

        // Rare (TR009-TR012)
        CriticalHitChance,
        BonusGold,
        DamageVsHighHP,
        AuraPower,

        // Unique (TR013-TR016)
        BurnSpread,
        DamageVsFrozen,
        ChainBounceBack,
        LaserRefraction,

        // Legendary (TR017-TR023)
        DamagePerGoldSpent,
        GlobalSlowPulse,
        BurstDamageWindow,
        AllStatsPerWave,
        MaxHPDecayPerSecond,
        DamagePerOwnedTurret,
        GradeBonus,

        // Keystone Covenants (mutually exclusive; appended — do not reorder).
        // EffectValue = the covenant's primary upside magnitude; downsides are
        // fixed constants in RunStatModifiers.
        FlameCovenant,   // + burn damage, - global direct damage
        FrostCovenant,   // + damage vs slowed/frozen, - attack speed
        StormCovenant    // +1 chain target & + chain damage, - global direct damage
    }
}
