// ============================================================================
// ETD.Data - SpecCardData.cs  [UPDATED - matches ETD_FULL_SHEET]
// ============================================================================
using UnityEngine;

namespace ETD.Data
{
    public enum SpecCardRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Unique = 3,
        Legendary = 4
    }

    public enum SpecCardEffectType
    {
        // Common (SC001-SC010)
        DamagePercent,           // SC001 +2.5%
        AttackSpeedPercent,      // SC002 +2.5%
        RangePercent,            // SC003 +2%
        CritChance,              // SC004 +5%
        ProjectileDamage,        // SC005 +18%
        ChainDamage,             // SC007 +15%
        AuraRange,               // SC008 +12%
        BurnDamage,              // SC009 +20%
        SlowDuration,            // SC010 +20%

        // Uncommon (SC011-SC020)
        BurnDamageStrong,        // SC011 +30%
        SlowStrength,            // SC012 +20%
        CritChanceStrong,        // SC013 +10%
        ChainRange,              // SC014 +20%
        ExtraProjectilePeriodic, // SC016 +1 every 4th shot    
        SupportRadius,           // SC018 +20%
        DamageVsElite,           // SC019 +25%
        UpgradeCostReduce,       // SC020 -12%

        // Rare (SC021-SC030)
        ChainTargetBonus,        // SC021 +2 targets
        DamageVsBurning,         // SC023 +35%
        DamageVsSlowedFrozen,    // SC024 +30%
        GoldGain,                // SC025 +18%
        ShockChance,             // SC026 20% mini-stun
        DamagePerDistance,        // SC027 +1.5% per tile
        DamagePerGoldHeld,       // SC030 +2% per 1000 gold

        // Unique (SC031-SC036)
        DeathExplosion,          // SC031 30% chance, 80% dmg
        BurnSpreadOnDeath,       // SC033 100% spread
        AttackSpeedPerNearbyEnemy, // SC036 +0.5% per enemy

        // Legendary (SC039-SC043)
        DamagePerWave,           // SC039 +0.5%/wave
        AttackSpeedPerWave,      // SC041 +0.35%/wave
        DamagePerGoldHeldStrong, // SC042 +4% per 1000 gold
        FreezeAmplifier,         // SC043 +100% dmg to frozen + chill spread

        // Utility (always available)
        HealHealth,

        // v0.12 - appended, do not reorder.
        BurnFromHit,  // Catalytic Burn: adds to the burn-from-hit fraction (0.15 = +15pp of hit damage as burn)
        SupportExposure, // Elemental Relay: enemies inside a Support debuff aura take bonus burn/chain damage (capped 25%)

        // v1.0 - appended, do not reorder. See [[etd-v1-full-release]] Phase 1.
        ArmorPierce,     // Ignores this many percentage points of enemy Armor
        AffinityPierce,  // Ignores this many percentage points of enemy turret-type affinity resistance

        // v1.0 Phase 2 - Luck. Feeds RunStatModifiers.GetGradeBonus() alongside the
        // pre-existing (previously contentless) TraitEffectType.GradeBonus / "Lucky
        // Charm" — same mechanic RunManager.GetRarityWeight already consumed for both
        // initial offers and reroll pools, just never had any card/trait granting it.
        Luck
    }

    [CreateAssetMenu(fileName = "New Spec Card", menuName = "ETD/Spec Card Data")]
    public class SpecCardData : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public SpecCardRarity Rarity;
        public SpecCardEffectType EffectType;
        public float EffectValue;

        [Header("Theme")]
        public string Theme; // Generic, Frost, Lightning, Inferno, etc.

        [Header("Unlock")]
        public bool IsUnlockedByDefault = true;
        [TextArea] public string UnlockCondition;

        [Header("Localization")]
        [Tooltip("Base key used for all localized fields. E.g. 'trait_tr001'")]
        public string LocalizationKey;

        [Header("Stacking")]
        public bool CanStack = true;
        public int MaxStacks = 99;

        [Header("Conditional")]
        [Tooltip("For periodic effects: every Nth shot, interval, etc.")]
        public int ConditionInterval;
        [Tooltip("For threshold effects: HP %, distance, etc.")]
        public float ConditionThreshold;

        public Color GetRarityColor()
        {
            return Rarity switch
            {
                SpecCardRarity.Common => new Color(0.8f, 0.8f, 0.8f),
                SpecCardRarity.Uncommon => new Color(0.3f, 0.85f, 0.3f),
                SpecCardRarity.Rare => new Color(0.3f, 0.5f, 1f),
                SpecCardRarity.Unique => new Color(0.7f, 0.3f, 0.9f),
                SpecCardRarity.Legendary => new Color(1f, 0.75f, 0.1f),
                _ => Color.white
            };
        }
    }
}
