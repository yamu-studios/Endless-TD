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

        void NotifyCriticalHit();
    }
}
 