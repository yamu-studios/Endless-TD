// ============================================================================
// ETD.Data - ChallengeData.cs  [UPDATED - border color, two icons]
// ============================================================================
using UnityEngine;
using ETD.Core;

namespace ETD.Data
{
    public enum ChallengeCategory
    {
        Beginner, Intermediate, Advanced, Expert, Bonus
    }

    public enum ChallengeConditionType
    {
        // Existing
        ReachWave,
        BuildTurrets,
        UpgradeTimes,
        UseAllEffects,
        UseSpellTimes,
        DealBurnDamage,
        SlowTotalSeconds,
        ChainHits,
        ContinuousBeamSeconds,
        EarnGoldInRun,
        SingleTurretTypeWave,
        NoSupportWave,
        MaxTurretsWave,
        NoSpellWave,
        UseSpecialTiles,
        SingleDamageTypeWave,
        NoLeaksUntilWave,
        DealTotalDamage,
        ComboEffectsOnEnemy,
        UseGreedTiles,

        // NEW  for trait unlock conditions
        CriticalHits,              // Land 500 critical hits
        TotalProjectilesFired,     // Fire 6,000 total projectiles
        BuffTurretsSimultaneous,   // Buff 12 turrets at the same time
        KillEliteEnemies,          // Kill 50 elite enemies
        SpendGoldInRun,            // Spend 15,000 gold in one run
        KillBurningEnemies,        // Kill 200/500 burning enemies
        DamageSlovedEnemies,       // Damage 1,000 slowed enemies
        HoldGoldAtOnce,            // Hold 8,000 / 20,000 unspent gold at once
        KillSpree,                 // Kill 50 enemies within 5 seconds
        EnemiesInRangeSameTime,    // Have 40 enemies inside turret ranges at once
        SurviveMinutes,            // Survive 18 minutes in one run
        FreezeEnemiesTotal,        // Freeze 2,500 enemies total
        SniperDamage,               // Deal 50,000 sniper damage
        BurningSimultaneous,    // Have 50 enemies burning at the same time
        ChainHitsPerEvent,      // Hit 10 enemies in one chain event
        LaserTimeOnTarget,      // Maintain laser on same target for 10s total (cumulative)
        SpendGoldLifetime,      // Spend 100,000 total gold across all runs (lifetime)
        PercentHPDamage,        // Deal 50,000 total percent-HP damage
    }

    public enum ChallengeRewardType
    {
        FlatCurrency, MetaCurrencyMultiplier, XPMultiplier,
        MaxHPBonus, GoldMultiplier, DamageBonus
    }

    public enum ChallengeStackType { None, Additive, Multiplicative }

    [CreateAssetMenu(fileName = "New Challenge", menuName = "ETD/Challenge Data")]
    public class ChallengeData : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;

        [Header("Icons")]
        [Tooltip("Icon shown when challenge is NOT completed")]
        public Sprite IncompleteIcon;
        [Tooltip("Icon shown when challenge IS completed")]
        public Sprite CompleteIcon;

        [Header("Localization")]
        [Tooltip("Base key used for all localized fields. E.g. 'trait_tr001'")]
        public string LocalizationKey;

        [Header("Appearance")]
        [Tooltip("Color used for item border and info panel border")]
        public Color BorderColor = new Color(0.4f, 0.6f, 1f);

        [Header("Category")]
        public ChallengeCategory Category;

        [Header("Condition")]
        public ChallengeConditionType ConditionType;
        public float TargetValue;
        [TextArea(1, 2)]
        public string ConditionDescription;

        [Header("Reward")]
        public ChallengeRewardType RewardType;
        public float RewardValue;
        public ChallengeStackType StackType;
        public Sprite RewardIcon;

        [Header("Progress")]
        public bool ShowProgress = true;

        // =================================================================
        // HELPERS
        // =================================================================

        public string GetRewardDescription()
        {
            string stack = LocalizationManager.Get($"challenge_stack_{StackType.ToString().ToLower()}", StackType.ToString());

            return RewardType switch
            {
                ChallengeRewardType.FlatCurrency => LocalizationManager.GetFormat("challenge_reward_flat_currency", "+{0:F0} Meta", RewardValue),
                ChallengeRewardType.MetaCurrencyMultiplier => LocalizationManager.GetFormat("challenge_reward_meta_rate", "x{0:F2} Meta Rate ({1})", RewardValue, stack),
                ChallengeRewardType.XPMultiplier => LocalizationManager.GetFormat("challenge_reward_xp", "x{0:F2} XP ({1})", RewardValue, stack),
                ChallengeRewardType.MaxHPBonus => LocalizationManager.GetFormat("challenge_reward_max_hp", "+{0:F0} Max HP", RewardValue),
                ChallengeRewardType.GoldMultiplier => LocalizationManager.GetFormat("challenge_reward_gold", "x{0:F2} Gold ({1})", RewardValue, stack),
                ChallengeRewardType.DamageBonus => LocalizationManager.GetFormat("challenge_reward_damage", "x{0:F2} Damage ({1})", RewardValue, stack),
                _ => $"{RewardValue}"
            };
        }

        public string GetRewardAmountText()
        {
            return RewardType switch
            {
                ChallengeRewardType.FlatCurrency => $"+{RewardValue:F0}",
                ChallengeRewardType.MaxHPBonus   => $"+{RewardValue:F0}",
                _ => $"x{RewardValue:F2}"
            };
        }

        public string GetProgressText(float current)
        {
            if (!ShowProgress) return "";
            return $"{FormatNum(Mathf.Min(current, TargetValue))}/{FormatNum(TargetValue)}";
        }

        private static string FormatNum(float n)
        {
            if (n >= 1_000_000) return $"{n / 1_000_000f:F1}M";
            if (n >= 1_000)     return $"{n / 1_000f:F1}K";
            return $"{n:F0}";
        }
    }
}
