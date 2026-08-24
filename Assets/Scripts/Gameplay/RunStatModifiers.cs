// ============================================================================
// ETD.Gameplay - RunStatModifiers.cs  [NEW]
// Central place to query all active stat modifiers from traits, spec cards,
// and wave-based scaling. Avoids scattering modifier logic everywhere.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;

namespace ETD.Gameplay
{
    public class RunStatModifiers : MonoBehaviour, IRunStatModifiers
    {
        [Header("Entropy Engine (non-lethal current-HP decay)")]
        [Tooltip("Enemies can never be decayed below this fraction of their max HP. " +
                 "Entropy Engine is intentionally non-lethal — turrets must finish enemies off.")]
        [SerializeField] private float _entropyMinHpFraction = ETD.Data.BalanceConstants.EntropyMinHpFraction;

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

            // Covenant downsides: Flame and Storm trade global direct damage for their
            // elemental upside (burn / chain scaling). Fixed -10% each.
            if (GetTraitBonus(TraitEffectType.FlameCovenant) > 0f) mult -= 0.10f;
            if (GetTraitBonus(TraitEffectType.StormCovenant) > 0f) mult -= 0.10f;

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

            // Frost Covenant downside: -10% attack speed for +damage vs slowed/frozen.
            if (GetTraitBonus(TraitEffectType.FrostCovenant) > 0f) mult -= 0.10f;

            // Precision Covenant downside: heavy shots fire slower.
            if (GetTraitBonus(TraitEffectType.PrecisionCovenant) > 0f) mult -= 0.20f;

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
            bonus += GetTraitBonus(TraitEffectType.FrostCovenant); // covenant upside
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
            bonus += GetTraitBonus(TraitEffectType.FlameCovenant); // covenant upside

            // Plague Covenant downside: rot displaces fire — burn output is halved.
            if (GetTraitBonus(TraitEffectType.PlagueCovenant) > 0f)
                return Mathf.Max(0f, (1f + bonus) * 0.5f);

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
            if (GetTraitBonus(TraitEffectType.StormCovenant) > 0f) bonus += 1; // covenant upside
            return bonus;
        }

        public float GetChainDamageMultiplier()
        {
            float mult = 1f + _runManager.RunData.GetSpecBonus(SpecCardEffectType.ChainDamage)
                            + GetTraitBonus(TraitEffectType.StormCovenant); // covenant upside

            // Precision Covenant downside: single-target focus halves chain output.
            if (GetTraitBonus(TraitEffectType.PrecisionCovenant) > 0f)
                mult *= 0.5f;

            return Mathf.Max(0f, mult);
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

            // Void Covenant downside: the run gives up critical hits entirely in
            // exchange for armor corrosion. Checked last so it overrides every source.
            if (GetTraitBonus(TraitEffectType.VoidCovenant) > 0f)
                return 0f;

            return Mathf.Clamp01(chance);
        }

        public float GetCritDamageMultiplier()
        {
            // Base critical hits deal 200% damage.
            // Turret-specific evolution bonuses, such as Sniper's CritDamageBonus,
            // are applied inside TurretController because they depend on the attacking turret.
            float mult = 2f;

            // Precision Covenant upside: +50% crit damage, pairing with its Expose
            // amplification into a single-target burst identity.
            if (GetTraitBonus(TraitEffectType.PrecisionCovenant) > 0f)
                mult += 0.5f;

            return mult;
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
            return GetTraitBonus(TraitEffectType.GradeBonus)
                + _runManager.RunData.GetSpecBonus(SpecCardEffectType.Luck);
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

        // Catalytic Burn: adds to TurretController's burn-from-hit fraction.
        public float GetBurnFromHitBonus()
        {
            return _runManager.RunData.GetSpecBonus(SpecCardEffectType.BurnFromHit);
        }

        // Elemental Relay (Exposure): bonus burn/chain damage taken by enemies inside
        // a Support debuff aura. Hard-capped at +25% per the design doc.
        public float GetSupportExposure()
        {
            return Mathf.Clamp(
                _runManager.RunData.GetSpecBonus(SpecCardEffectType.SupportExposure), 0f, 0.25f);
        }

        // v1.0 mitigation pipeline (see [[etd-v1-full-release]] Phase 1): ignores this
        // many percentage points of the enemy's Armor / turret-type affinity
        // resistance respectively, before the remaining resistance is applied.
        // Clamped to 0.9 so mitigation can never be fully negated to guaranteed
        // pierce-through (mirrors the 0.9 clamp on Armor/affinity themselves).
        // Void Covenant upside: every turret, not just Void, ignores this many
        // percentage points of Armor on top of any ArmorPierce cards.
        private const float VoidCovenantArmorPierce = 0.15f;

        public float GetArmorPierce()
        {
            float pierce = _runManager.RunData.GetSpecBonus(SpecCardEffectType.ArmorPierce);
            if (GetTraitBonus(TraitEffectType.VoidCovenant) > 0f)
                pierce += VoidCovenantArmorPierce;
            return Mathf.Clamp(pierce, 0f, 0.9f);
        }

        public float GetAffinityPierce()
        {
            return Mathf.Clamp(
                _runManager.RunData.GetSpecBonus(SpecCardEffectType.AffinityPierce), 0f, 0.9f);
        }

        // =================================================================
        // PER-TURRET-TYPE CONTENT (v1.0 Phase 5)
        // Signature cards (SpecCardEffectType.TurretTypeSignature) and Mastery traits
        // (TraitEffectType.TurretTypeMastery) both carry a TargetTurretType, so both
        // are looked up by type rather than summed globally.
        // =================================================================

        /// <summary>Multiplier on a turret type's signature mechanic — the status it
        /// alone applies (ArmorBreak/Slow/Burn/Weaken/Expose/Poison) or its equivalent
        /// identity (Lightning chain damage, Laser ramp, Support aura, Radar reveal).
        /// Includes the matching Keystone Covenant's upside.</summary>
        public float GetTurretTypeSignatureMultiplier(int turretType)
        {
            var type = (TurretType)turretType;
            float bonus = _runManager.RunData.GetSpecTurretBonus(
                SpecCardEffectType.TurretTypeSignature, type);

            GetMasteryAllocation(type, out _, out _, out _, out float signatureShare);
            bonus += signatureShare
                * GetTurretTypeTraitBonus(TraitEffectType.TurretTypeMastery, type);

            // Covenant upsides act on their own turret type's signature.
            switch (type)
            {
                case TurretType.Void:
                    bonus += GetTraitBonus(TraitEffectType.VoidCovenant);
                    break;
                case TurretType.Toxin:
                    bonus += GetTraitBonus(TraitEffectType.PlagueCovenant);
                    break;
                case TurretType.Railgun:
                    bonus += GetTraitBonus(TraitEffectType.PrecisionCovenant);
                    break;
            }

            return 1f + bonus;
        }

        // ---- Mastery stat allocation -------------------------------------------
        // A mastery trait is a fixed budget spent across three stats, but not every
        // turret type can use all three:
        //   * Support and Radar return out of ManagedUpdate before UpdateCombat, so
        //     they never attack — Damage and AttackSpeed are inert data on them.
        //   * Laser's DPS is Damage * conditional * stack * crit; AttackSpeed appears
        //     nowhere in it, and IsLaserLike turrets tick on a fixed interval, so
        //     attack speed is inert for them too.
        // The budget is therefore redirected to stats each type actually reads,
        // rather than being silently wasted. GetMasteryAllocation is the single
        // source of truth, shared with BalanceDescriptionFormatter's effect line so
        // the displayed numbers can never drift from the applied ones.

        /// <summary>False for turret types that never enter UpdateCombat.</summary>
        public static bool TurretTypeAttacks(TurretType type)
            => type != TurretType.Support && type != TurretType.Radar;

        /// <summary>False for types whose damage output does not read AttackSpeed.</summary>
        public static bool TurretTypeUsesAttackSpeed(TurretType type)
            => TurretTypeAttacks(type) && type != TurretType.Laser;

        /// <summary>Splits a mastery value into the four stats it can grant. Fractions
        /// are of the trait's effect value; 0 means that stat is not granted.</summary>
        public static void GetMasteryAllocation(TurretType type,
            out float damage, out float attackSpeed, out float range, out float signature)
        {
            damage = TurretTypeAttacks(type) ? 1f : 0f;
            attackSpeed = TurretTypeUsesAttackSpeed(type) ? 0.5f : 0f;

            // Laser and Support get the freed attack-speed share as range: Laser's
            // reach is a real lever, and Support's Range IS its aura radius.
            range = (type == TurretType.Laser || type == TurretType.Support) ? 0.5f : 0f;

            // Radar's reveal range is its only usable stat (its Range is unread while
            // RevealRange is set), so its whole budget lands on the signature — which
            // scales reveal reach, and through it the Path A mark and Path B aura radius.
            signature = type == TurretType.Radar ? 2f : 1f;
        }

        public float GetTurretTypeDamageMultiplier(int turretType)
        {
            var type = (TurretType)turretType;
            GetMasteryAllocation(type, out float share, out _, out _, out _);
            if (share <= 0f) return 1f;
            return 1f + share * GetTurretTypeTraitBonus(TraitEffectType.TurretTypeMastery, type);
        }

        public float GetTurretTypeAttackSpeedMultiplier(int turretType)
        {
            var type = (TurretType)turretType;
            GetMasteryAllocation(type, out _, out float share, out _, out _);
            if (share <= 0f) return 1f;
            return 1f + share * GetTurretTypeTraitBonus(TraitEffectType.TurretTypeMastery, type);
        }

        /// <summary>Range share of a mastery — Laser reach, Support aura radius.</summary>
        public float GetTurretTypeRangeMultiplier(int turretType)
        {
            var type = (TurretType)turretType;
            GetMasteryAllocation(type, out _, out _, out float share, out _);
            if (share <= 0f) return 1f;
            return 1f + share * GetTurretTypeTraitBonus(TraitEffectType.TurretTypeMastery, type);
        }

        // Plague Covenant: Toxin's pure-damage identity leaks to every other turret
        // type at this fraction of its rate, which is what makes the covenant a build
        // rather than a single-turret buff.
        private const float PlagueLeakFraction = 0.2f;

        public float GetPlagueLeakFraction()
        {
            return GetTraitBonus(TraitEffectType.PlagueCovenant) > 0f ? PlagueLeakFraction : 0f;
        }

        /// <summary>Sums effective values of active traits with the given effect type
        /// AND matching TargetTurretType. Mirrors GetTraitBonus but type-filtered.</summary>
        private float GetTurretTypeTraitBonus(TraitEffectType type, TurretType turretType)
        {
            float total = 0f;
            var data = _runManager.RunData;
            foreach (var traitId in data.ActiveTraitIds)
            {
                var trait = _database.GetTrait(traitId);
                if (trait != null && trait.EffectType == type
                    && trait.TargetsTurretType && trait.TargetTurretType == turretType)
                    total += GetTraitEffectiveValue(traitId, trait);
            }
            return total;
        }

        public static float GetWaveScalingMultiplier(float perWaveBonus, int wave)
        {
            if (perWaveBonus <= 0f || wave <= 0)
                return 1f;

            // Backward-compatible percent handling:
            // 0.005 = +0.5% per wave, 0.5 = +50% per wave, 5 = +5% per wave.
            float normalized = NormalizePercentLikeValue(perWaveBonus);
            int exponentialWaves = Mathf.Min(wave, BalanceConstants.PlayerWaveScalingExponentialCap);
            double multiplier = System.Math.Pow(1d + normalized, exponentialWaves);

            int linearWaves = wave - exponentialWaves;
            if (linearWaves > 0)
                multiplier *= 1d + normalized * linearWaves;

            return multiplier >= float.MaxValue ? float.MaxValue : (float)multiplier;
        }

        private static float NormalizePercentLikeValue(float value)
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

            // Entropy Engine: non-lethal decay of a fraction of CURRENT HP per second.
            // Softens extreme tanks but can never kill (floored), so it is never a
            // standalone win condition. Counts toward percent-HP damage tracking.
            float hpDecay = GetMaxHPDecayPerSecond();
            if (hpDecay > 0f)
                ApplyGlobalNonLethalDecay(hpDecay * Time.deltaTime);
        }

        private void UpdateTimedBuffs()
        {
            if (_goldSurgeTimer > 0) _goldSurgeTimer -= Time.deltaTime;

            if (_overclockTimer > 0)
            {
                _overclockTimer -= Time.deltaTime;
                // Attack speed is baked into each turret's cached stats
                // (RecalculateStats), unlike GoldMultiplier which is read live per
                // kill — so expiry needs an explicit recalc to actually revert turrets.
                if (_overclockTimer <= 0f)
                    RefreshTurretStats();
            }
        }

        // =================================================================
        // v1.0 ACTIVE SPELLS (Overclock / Gold Surge) — see [[etd-v1-full-release]]
        // Phase 2. These fields/getters already existed pre-Phase-2 with no caller
        // ever setting them (SpellManager.TryCast is the first). Public setters here
        // are the only new surface; consumption in GetGlobalAttackSpeedMultiplier/
        // GetGoldMultiplier above was already wired.
        // =================================================================

        public void ApplyOverclock(float bonus, float duration)
        {
            _overclockBonus = Mathf.Max(0f, bonus);
            _overclockTimer = Mathf.Max(0f, duration);
            RefreshTurretStats();
        }

        public void ApplyGoldSurge(float multiplier, float duration)
        {
            _goldSurgeMultiplier = Mathf.Max(0f, multiplier);
            _goldSurgeTimer = Mathf.Max(0f, duration);
        }

        private void RefreshTurretStats()
        {
            if (ServiceLocator.TryGet<ETD.Turrets.TurretManager>(out var tm))
                tm.RecalculateAllTurretsAndRefreshAuras();
        }


        // Entropy Engine tick. Removes a fraction of each enemy's CURRENT HP, floored
        // so it can never kill. Reports the removed HP to percent-HP tracking / the
        // Entropy unlock, but deliberately does NOT spawn a floating damage number per
        // enemy per frame (that would spam the screen).
        private void ApplyGlobalNonLethalDecay(float currentHpFraction)
        {
            var enemyMgr = ServiceLocator.Get<Enemies.EnemyManager>();
            if (enemyMgr == null) return;

            var enemies = enemyMgr.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || e.IsDead) continue;

                float applied = e.ApplyNonLethalDecay(currentHpFraction, _entropyMinHpFraction);
                if (applied <= 0f) continue;

                EventBus.Publish(new PercentHPDamageEvent { DamageAmount = applied });

                if (_runManager?.RunData != null)
                    _runManager.RunData.TotalPercentHPDamage += applied;
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

        // Effective trait values (base * shop-upgrade multiplier) cached per run.
        // GetTraitBonus runs in stat-recalc hot paths; the save lookup + math only
        // happens once per trait per run.
        private readonly Dictionary<string, float> _traitEffectiveValueCache = new();

        private float GetTraitEffectiveValue(string traitId, TraitData trait)
        {
            if (_traitEffectiveValueCache.TryGetValue(traitId, out float cached))
                return cached;

            // FIX: shop trait upgrades (SaveData.TraitUpgradeLevels) previously had no
            // gameplay effect at all — every consumer read the raw EffectValue.
            int upgradeLevel = SaveSystem.GetTraitUpgradeLevel(SaveSystem.Load(), traitId);
            float value = trait.GetEffectiveValue(upgradeLevel);
            _traitEffectiveValueCache[traitId] = value;
            return value;
        }

        private float GetTraitBonus(TraitEffectType type)
        {
            float total = 0f;
            var data = _runManager.RunData;
            foreach (var traitId in data.ActiveTraitIds)
            {
                var trait = _database.GetTrait(traitId);
                if (trait != null && trait.EffectType == type)
                    total += GetTraitEffectiveValue(traitId, trait);
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
