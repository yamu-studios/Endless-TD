// ============================================================================
// ETD.Gameplay - RunStatModifiers.cs  [NEW]
// Central place to query all active stat modifiers from traits, spec cards,
// and wave-based scaling. Avoids scattering modifier logic everywhere.
// ============================================================================
using UnityEngine;
using ETD.Core;
using ETD.Data;

namespace ETD.Gameplay
{
    public class RunStatModifiers : MonoBehaviour, IRunStatModifiers
    {
        private RunManager _runManager;
        private GameDatabase _database;

        // Cached timed trait states
        private float _burstWindowTimer;
        private bool _burstWindowActive;
        private float _slowPulseTimer;
        private float _goldSurgeTimer;
        private float _goldSurgeMultiplier = 1f;
        private float _overclockTimer;
        private float _overclockBonus;

        public void Initialize(RunManager runManager, GameDatabase database)
        {
            _runManager = runManager;
            _database = database;
            ServiceLocator.Register(this);
            ServiceLocator.Register<IRunStatModifiers>(this);

        }

        private void Update()
        {
            var data = _runManager?.RunData;
            if (data == null) return;

            UpdateTimedTraits(data);
            UpdateTimedBuffs();
        }

        // =================================================================
        // GLOBAL DAMAGE MULTIPLIER (all sources combined)
        // =================================================================

        public float GetGlobalDamageMultiplier()
        {
            var data = _runManager.RunData;
            float mult = 1f;

            // Static/global damage bonuses remain additive inside this bucket.
            // Wave-based bonuses are applied as exponential multipliers below
            // so player scaling follows the same kind of curve as enemy HP.
            mult += GetTraitBonus(TraitEffectType.BonusDamage);
            mult += data.GetSpecBonus(SpecCardEffectType.DamagePercent);
            mult += data.GetSpecBonus(SpecCardEffectType.ProjectileDamage);

            // Trait: Architect of Doom (+x% per owned turret)
            float perTurret = GetTraitBonus(TraitEffectType.DamagePerOwnedTurret);
            if (perTurret > 0)
                mult += perTurret * data.TurretsPlaced;

            // Trait: Singularity Core (+x% per 100 gold spent)
            float perGold = GetTraitBonus(TraitEffectType.DamagePerGoldSpent);
            if (perGold > 0)
                mult += perGold * (data.TotalGoldSpent / 100f);

            // Spec/Trait wave scaling: old logic was linear: +value * wave.
            // New logic is exponential: Pow(1 + value, wave).
            float perWaveDamage = data.GetSpecBonus(SpecCardEffectType.DamagePerWave)
                                + GetTraitBonus(TraitEffectType.AllStatsPerWave);
            if (perWaveDamage > 0f)
                mult *= GetWaveScalingMultiplier(perWaveDamage, data.CurrentWave);

            // Trait: Overcharge burst window
            if (_burstWindowActive)
                mult += GetTraitBonus(TraitEffectType.BurstDamageWindow);

            // Spec: Wealth Loop / Doom Reserve (damage per gold held)
            float perGoldHeld = data.GetSpecBonus(SpecCardEffectType.DamagePerGoldHeld);
            perGoldHeld += data.GetSpecBonus(SpecCardEffectType.DamagePerGoldHeldStrong);
            if (perGoldHeld > 0)
                mult += perGoldHeld * (data.Gold / 1000f);

            float permDmgBonus = 0f;
            var bonuses = data.ActivePermanentBonuses;
            if (bonuses != null && bonuses.Length > 3)
                permDmgBonus = bonuses[3];

            return mult * (1f + permDmgBonus);

        }

        // =================================================================
        // GLOBAL ATTACK SPEED MULTIPLIER
        // =================================================================

        public float GetGlobalAttackSpeedMultiplier()
        {
            var data = _runManager.RunData;
            float mult = 1f;

            mult += GetTraitBonus(TraitEffectType.BonusAttackSpeed);
            mult += data.GetSpecBonus(SpecCardEffectType.AttackSpeedPercent);

            float perWaveAttackSpeed = data.GetSpecBonus(SpecCardEffectType.AttackSpeedPerWave)
                                     + GetTraitBonus(TraitEffectType.AllStatsPerWave);
            if (perWaveAttackSpeed > 0f)
                mult *= GetWaveScalingMultiplier(perWaveAttackSpeed, data.CurrentWave);

            // Spec: Overrun Battery (+0.5% per nearby enemy) — applied per-turret

            // Overclock spell buff
            if (_overclockTimer > 0)
                mult += _overclockBonus;

            return mult;
        }

        // =================================================================
        // GLOBAL RANGE MULTIPLIER
        // =================================================================

        public float GetGlobalRangeMultiplier()
        {
            var data = _runManager.RunData;
            float mult = 1f;

            mult += GetTraitBonus(TraitEffectType.BonusRange);
            mult += data.GetSpecBonus(SpecCardEffectType.RangePercent);

            float perWaveRange = GetTraitBonus(TraitEffectType.AllStatsPerWave);
            if (perWaveRange > 0f)
                mult *= GetWaveScalingMultiplier(perWaveRange, data.CurrentWave);
            
            return mult;
        }

        // =================================================================
        // CONDITIONAL DAMAGE BONUSES
        // =================================================================

        public float GetDamageVsEliteMultiplier()
        {
            float bonus = GetTraitBonus(TraitEffectType.DamageVsHighHP);
            bonus += _runManager.RunData.GetSpecBonus(SpecCardEffectType.DamageVsElite);
            return 1f + bonus;
        }

        public float GetDamageVsBurningMultiplier()
        {
            return 1f + _runManager.RunData.GetSpecBonus(SpecCardEffectType.DamageVsBurning);
        }

        public float GetDamageVsSlowedFrozenMultiplier()
        {
            float bonus = GetTraitBonus(TraitEffectType.DamageVsFrozen);
            bonus += _runManager.RunData.GetSpecBonus(SpecCardEffectType.DamageVsSlowedFrozen);
            return 1f + bonus;
        }

        // =================================================================
        // FROST / BURN / CHAIN MODIFIERS
        // =================================================================

        public float GetSlowStrengthMultiplier()
        {
            float bonus = GetTraitBonus(TraitEffectType.SlowStrength);
            bonus += _runManager.RunData.GetSpecBonus(SpecCardEffectType.SlowStrength);
            return 1f + bonus;
        }

        public float GetSlowDurationMultiplier()
        {
            return 1f + _runManager.RunData.GetSpecBonus(SpecCardEffectType.SlowDuration);
        }

        public float GetBurnDamageMultiplier()
        {
            float bonus = _runManager.RunData.GetSpecBonus(SpecCardEffectType.BurnDamage);
            bonus += _runManager.RunData.GetSpecBonus(SpecCardEffectType.BurnDamageStrong);
            return 1f + bonus;
        }

        public float GetBurnDurationMultiplier()
        {
            return 1f + GetTraitBonus(TraitEffectType.BurnDuration);
        }

        public int GetBonusChainTargets()
        {
            int bonus = 0;
            float traitBonus = GetTraitBonus(TraitEffectType.ChainTargetBonus);
            if (traitBonus > 0) bonus += Mathf.RoundToInt(traitBonus);
            bonus += Mathf.RoundToInt(_runManager.RunData.GetSpecBonus(SpecCardEffectType.ChainTargetBonus));
            return bonus;
        }

        public float GetChainDamageMultiplier()
        {
            return 1f + _runManager.RunData.GetSpecBonus(SpecCardEffectType.ChainDamage);
        }

        public float GetChainRangeMultiplier()
        {
            return 1f + _runManager.RunData.GetSpecBonus(SpecCardEffectType.ChainRange);
        }

        // =================================================================
        // CRIT
        // =================================================================

        public float GetCritChance()
        {
            float chance = GetTraitBonus(TraitEffectType.CriticalHitChance);
            if (chance > 1f) chance *= 0.01f; // data safety: 15 means 15%
            chance += _runManager.RunData.GetSpecBonus(SpecCardEffectType.CritChance);
            chance += _runManager.RunData.GetSpecBonus(SpecCardEffectType.CritChanceStrong);
            return Mathf.Clamp01(chance);
        }

        public float GetCritDamageMultiplier()
        {
            // Base critical hits deal 200% damage.
            // Turret-specific evolution bonuses, such as Sniper's CritDamageBonus,
            // are applied inside TurretController because they depend on the attacking turret.
            return 2f;
        }

        // =================================================================
        // ECONOMY
        // =================================================================

        public float GetGoldMultiplier()
        {
            float mult = 1f;
            mult += GetTraitBonus(TraitEffectType.BonusGold);
            mult += _runManager.RunData.GetSpecBonus(SpecCardEffectType.GoldGain);

            if (_goldSurgeTimer > 0)
                mult *= _goldSurgeMultiplier;

            return mult;
        }

        public float GetUpgradeCostMultiplier()
        {
            float mult = 1f;
            mult -= GetTraitBonus(TraitEffectType.ReduceUpgradeCost);
            mult -= _runManager.RunData.GetSpecBonus(SpecCardEffectType.UpgradeCostReduce);
            return Mathf.Max(0.1f, mult);
        }

        // =================================================================
        // GRADE BONUS
        // =================================================================

        public float GetGradeBonus()
        {
            return GetTraitBonus(TraitEffectType.GradeBonus);
        }

        // =================================================================
        // SUPPORT AURA
        // =================================================================

        public float GetSupportAuraMultiplier()
        {
            return 1f + GetTraitBonus(TraitEffectType.AuraPower);
        }

        public float GetSupportRadiusMultiplier()
        {
            float bonus = _runManager.RunData.GetSpecBonus(SpecCardEffectType.SupportRadius);
            bonus += _runManager.RunData.GetSpecBonus(SpecCardEffectType.AuraRange);
            return 1f + bonus;
        }

        public float GetAttackSpeedPerNearbyEnemyBonus()
        {
            return _runManager.RunData.GetSpecBonus(SpecCardEffectType.AttackSpeedPerNearbyEnemy);
        }

        public float GetFreezeAmplifierMultiplier()
        {
            return 1f + _runManager.RunData.GetSpecBonus(SpecCardEffectType.FreezeAmplifier);
        }

        public float GetShockChance()
        {
            return _runManager.RunData.GetSpecBonus(SpecCardEffectType.ShockChance);
        }


        public float GetDamagePerDistanceBonus()
        {
            return _runManager.RunData.GetSpecBonus(SpecCardEffectType.DamagePerDistance);
        }

        public float GetDeathExplosionChance()
        {
            return Mathf.Clamp01(_runManager.RunData.GetSpecBonus(SpecCardEffectType.DeathExplosion));
        }

        public float GetBurnSpreadChance()
        {
            return Mathf.Clamp01(NormalizePercentLikeValue(
                GetTraitBonus(TraitEffectType.BurnSpread)
            ));
        }

        public float GetBurnSpreadOnDeathChance()
        {
            return Mathf.Clamp01(NormalizePercentLikeValue(
                _runManager.RunData.GetSpecBonus(SpecCardEffectType.BurnSpreadOnDeath)
            ));
        }

        public float GetChainBounceBackChance()
        {
            return Mathf.Clamp01(NormalizePercentLikeValue(GetTraitBonus(TraitEffectType.ChainBounceBack)));
        }

        public float GetLaserRefractionPercent()
        {
            return Mathf.Clamp01(NormalizePercentLikeValue(GetTraitBonus(TraitEffectType.LaserRefraction)));
        }

        public float GetExtraProjectileEveryNthShot()
        {
            return _runManager.RunData.GetSpecBonus(SpecCardEffectType.ExtraProjectilePeriodic);
        }

        public float GetMaxHPDecayPerSecond()
        {
            return NormalizePercentLikeValue(GetTraitBonus(TraitEffectType.MaxHPDecayPerSecond));
        }

        private float GetWaveScalingMultiplier(float perWaveBonus, int wave)
        {
            if (perWaveBonus <= 0f || wave <= 0)
                return 1f;

            // Backward-compatible percent handling:
            // 0.005 = +0.5% per wave, 0.5 = +50% per wave, 5 = +5% per wave.
            float normalized = NormalizePercentLikeValue(perWaveBonus);
            return Mathf.Pow(1f + normalized, wave);
        }

        private float NormalizePercentLikeValue(float value)
        {
            return value > 1f ? value * 0.01f : value;
        }

        public void NotifyCriticalHit()
        {
            if (_runManager?.RunData != null)
                _runManager.RunData.TotalCriticalHits++;
        }

        // =================================================================
        // TIMED TRAITS (Legendary)
        // =================================================================

        private void UpdateTimedTraits(RunData data)
        {
            // Overcharge: +25% damage for 3s every 30s
            float burstVal = GetTraitBonus(TraitEffectType.BurstDamageWindow);
            if (burstVal > 0)
            {
                _burstWindowTimer += Time.deltaTime;
                if (_burstWindowTimer >= 30f)
                {
                    _burstWindowActive = true;
                    _burstWindowTimer = 0f;
                }
                if (_burstWindowActive && _burstWindowTimer >= 3f)
                    _burstWindowActive = false;
            }

            // Time Distortion: 50% global slow every 60s
            float slowPulse = GetTraitBonus(TraitEffectType.GlobalSlowPulse);
            if (slowPulse > 0)
            {
                _slowPulseTimer += Time.deltaTime;
                if (_slowPulseTimer >= 60f)
                {
                    _slowPulseTimer = 0f;
                    ApplyGlobalSlow(slowPulse, 2f);
                }
            }

            // Entropy Engine: percent of max HP per second to all enemies.
            float hpDecay = GetMaxHPDecayPerSecond();
            if (hpDecay > 0f)
                ApplyGlobalPureMaxHPDamage(hpDecay * Time.deltaTime);
        }

        private void UpdateTimedBuffs()
        {
            if (_goldSurgeTimer > 0) _goldSurgeTimer -= Time.deltaTime;
            if (_overclockTimer > 0) _overclockTimer -= Time.deltaTime;
        }


        private void ApplyGlobalPureMaxHPDamage(float maxHpFraction)
        {
            var enemyMgr = ServiceLocator.Get<Enemies.EnemyManager>();
            if (enemyMgr == null) return;

            var enemies = enemyMgr.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || e.IsDead) continue;

                float damage = e.MaxHealth * maxHpFraction;
                EventBus.Publish(new PercentHPDamageEvent
                {
                    DamageAmount = damage
                });

                e.TakePureDamage(damage);

                if (_runManager?.RunData != null)
                    _runManager.RunData.TotalPercentHPDamage += damage;
            }
        }

        private void ApplyGlobalSlow(float strength, float duration)
        {
            var enemyMgr = ServiceLocator.Get<Enemies.EnemyManager>();
            if (enemyMgr == null) return;
            var enemies = enemyMgr.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (!enemies[i].IsDead)
                    enemies[i].ApplyStatus(StatusEffectType.Slow, strength, duration);
            }
        }

      
        // =================================================================
        // HELPERS
        // =================================================================

        private float GetTraitBonus(TraitEffectType type)
        {
            float total = 0f;
            var data = _runManager.RunData;
            foreach (var traitId in data.ActiveTraitIds)
            {
                var trait = _database.GetTrait(traitId);
                if (trait != null && trait.EffectType == type)
                    total += trait.EffectValue;
            }
            return total;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<IRunStatModifiers>();
            ServiceLocator.Unregister<RunStatModifiers>();
        }
    }
}
