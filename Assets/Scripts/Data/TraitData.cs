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
        public float UpgradedEffectValue;

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
        GradeBonus
    }
}
