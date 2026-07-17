using UnityEngine;

namespace ETD.Core
{

        public interface IRunStatModifiers
    {
        float GetGlobalDamageMultiplier();
        float GetGlobalAttackSpeedMultiplier();
        float GetGlobalRangeMultiplier();

        float GetDamageVsEliteMultiplier();
        float GetDamageVsBurningMultiplier();
        float GetDamageVsSlowedFrozenMultiplier();

        float GetSlowStrengthMultiplier();
        float GetSlowDurationMultiplier();
        float GetBurnDamageMultiplier();
        float GetBurnDurationMultiplier();

        int GetBonusChainTargets();
        float GetChainDamageMultiplier();
        float GetChainRangeMultiplier();

        float GetCritChance();
        float GetCritDamageMultiplier();
        float GetUpgradeCostMultiplier();

        float GetSupportAuraMultiplier();
        float GetSupportRadiusMultiplier();
        float GetAttackSpeedPerNearbyEnemyBonus();
        float GetFreezeAmplifierMultiplier();
        float GetShockChance();

        float GetDamagePerDistanceBonus();
        float GetDeathExplosionChance();
        float GetBurnSpreadChance();
        float GetBurnSpreadOnDeathChance();
        float GetChainBounceBackChance();
        float GetLaserRefractionPercent();
        float GetExtraProjectileEveryNthShot();
        float GetMaxHPDecayPerSecond();
        float GetBurnFromHitBonus();
        float GetSupportExposure();

        float GetArmorPierce();
        float GetAffinityPierce();

        // v1.0 active spells (Overclock / Gold Surge) — see [[etd-v1-full-release]] Phase 2.
        void ApplyOverclock(float bonus, float duration);
        void ApplyGoldSurge(float multiplier, float duration);

        void NotifyCriticalHit();
    }
}
 