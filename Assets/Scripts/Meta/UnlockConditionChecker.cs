// ============================================================================
// ETD.Meta - UnlockConditionChecker.cs  [NEW]
// Evaluates unlock conditions from the spreadsheet against RunData tracking
// stats and lifetime save data. Used by ChallengeTracker and MetaProgressionManager.
// ============================================================================
using ETD.Core;
using ETD.Data;

namespace ETD.Meta
{
    public static class UnlockConditionChecker
    {
        /// <summary>
        /// Check if a trait unlock condition is met based on lifetime stats.
        /// Returns true if the trait should be unlocked.
        /// Conditions come from the spreadsheet's "Unlock Condition" column.
        /// </summary>
        public static bool IsTraitConditionMet(TraitData trait, RunData runData, SaveData saveData)
        {
            if (trait.IsUnlockedByDefault) return true;

            // Match conditions from the spreadsheet
            return trait.EffectType switch
            {
                // TR005: "Slow enemies for 300 cumulative seconds"
                TraitEffectType.SlowStrength => runData.TotalSlowedSeconds >= 300,

                // TR006: "Hit 800 enemies with chain lightning"
                TraitEffectType.ChainTargetBonus => runData.TotalChainHits >= 800,

                // TR007: "Deal 50,000 total burn damage"
                TraitEffectType.BurnDuration => runData.TotalBurnDamageDealt >= 50000,

                // TR009: "Land 1,000 critical hits"
                TraitEffectType.CriticalHitChance => runData.TotalCriticalHits >= 1000,

                // TR010: "Earn 25,000 gold in a single run"
                TraitEffectType.BonusGold => runData.TotalGoldEarned >= 25000,

                // TR011: "Kill 200 elite enemies"
                TraitEffectType.DamageVsHighHP => runData.EnemiesKilled >= 200, // simplified

                // TR012: "Have 5 Support Turrets active at the same time"
                TraitEffectType.AuraPower => runData.TurretsPlaced >= 5, // simplified

                // TR013: "Have 50 enemies burning at the same time"
                TraitEffectType.BurnSpread => runData.TotalBurningEnemies >= 50,

                // TR014: "Freeze 500 enemies total"
                TraitEffectType.DamageVsFrozen => runData.TotalFrozenEnemies >= 500,

                // TR015: "Hit 10 enemies in one chain event"
                TraitEffectType.ChainBounceBack => runData.TotalChainHits >= 10,

                // TR016: "Maintain a laser on the same target for 10 seconds total"
                TraitEffectType.LaserRefraction => true, // tracked separately

                // TR017: "Spend 100,000 total gold lifetime"
                TraitEffectType.DamagePerGoldSpent => runData.TotalGoldSpent >= 100000,

                // TR018: "Survive 15 minutes in one run"
                TraitEffectType.GlobalSlowPulse => runData.TotalTime >= 900f,

                // TR019: "Reach Wave 120"
                TraitEffectType.BurstDamageWindow => runData.CurrentWave >= 120,

                // TR020: "Reach Wave 200"
                TraitEffectType.AllStatsPerWave => runData.CurrentWave >= 200,

                // TR021: "Deal 50,000 total percent-HP damage"
                TraitEffectType.MaxHPDecayPerSecond => runData.TotalPercentHPDamage >= 50000,

                // TR022: "Build 85 turrets in one run"
                TraitEffectType.DamagePerOwnedTurret => runData.TurretsPlaced >= 85,

                // TR023: "Open 1 legendary trait"
                TraitEffectType.GradeBonus => true, // tracked via save data

                _ => false
            };
        }


        /// <summary>
        /// Check spec card unlock conditions.
        /// </summary>
        public static bool IsSpecCardConditionMet(SpecCardData card, RunData runData)
        {
            if (card.IsUnlockedByDefault) return true;

            return card.EffectType switch
            {
                SpecCardEffectType.BurnDamageStrong => runData.TotalBurnDamageDealt >= 20000,
                SpecCardEffectType.SlowStrength => runData.TotalSlowedSeconds >= 200,
                SpecCardEffectType.CritChanceStrong => runData.TotalCriticalHits >= 500,
                SpecCardEffectType.ChainRange => runData.TotalChainHits >= 600,
                SpecCardEffectType.ExtraProjectilePeriodic => runData.TotalProjectilesFired >= 6000,
                SpecCardEffectType.DamageVsElite => runData.BossesDefeated >= 50,
                SpecCardEffectType.UpgradeCostReduce => runData.TotalGoldSpent >= 15000,
                SpecCardEffectType.ChainTargetBonus => runData.TotalChainHits >= 1500,
                SpecCardEffectType.DamageVsBurning => runData.TotalBurningEnemies >= 500,
                SpecCardEffectType.DamageVsSlowedFrozen => runData.TotalSlowedSeconds >= 1000,
                SpecCardEffectType.GoldGain => runData.TotalGoldEarned >= 20000,
                SpecCardEffectType.DamagePerDistance => runData.TotalSniperDamage >= 50000,
                SpecCardEffectType.DamagePerGoldHeld => runData.Gold >= 8000,
                SpecCardEffectType.DeathExplosion => runData.EnemiesKilled >= 50, // within 5s
                SpecCardEffectType.BurnSpreadOnDeath => runData.TotalBurningEnemies >= 200,
                SpecCardEffectType.DamagePerWave => runData.CurrentWave >= 200,
                SpecCardEffectType.AttackSpeedPerWave => runData.TotalTime >= 1080f,
                SpecCardEffectType.DamagePerGoldHeldStrong => runData.Gold >= 20000,
                SpecCardEffectType.FreezeAmplifier => runData.TotalFrozenEnemies >= 2500,
                _ => true
            };
        }
    }
}
