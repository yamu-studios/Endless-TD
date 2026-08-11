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

        // v1.0 Phase 5 per-turret-type content. `turretType` is a TurretType cast to
        // int, matching the sourceTurretType convention already used across the
        // damage pipeline, so ETD.Core need not reference ETD.Data.
        float GetTurretTypeSignatureMultiplier(int turretType);
        float GetTurretTypeDamageMultiplier(int turretType);
        float GetTurretTypeAttackSpeedMultiplier(int turretType);
        float GetTurretTypeRangeMultiplier(int turretType);

        /// <summary>Fraction of Toxin's pure-damage identity that non-Toxin turrets
        /// also apply (Plague Covenant). 0 when the covenant is not active.</summary>
        float GetPlagueLeakFraction();

        // v1.0 active spells (Overclock / Gold Surge) — see [[etd-v1-full-release]] Phase 2.
        void ApplyOverclock(float bonus, float duration);
        void ApplyGoldSurge(float multiplier, float duration);

        void NotifyCriticalHit();
    }
}
 