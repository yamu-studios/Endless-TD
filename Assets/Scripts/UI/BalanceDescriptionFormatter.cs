// ============================================================================
// ETD.UI - BalanceDescriptionFormatter.cs
// Adds numeric, localized effect lines to traits and spec cards.
// ============================================================================
using ETD.Core;
using ETD.Data;

namespace ETD.UI
{
    public static class BalanceDescriptionFormatter
    {
        /// <summary>
        /// Effect value including the player's permanent shop upgrades — what the trait
        /// actually does in a run. All trait displays must use this instead of the raw
        /// EffectValue, otherwise upgraded traits look identical to base ones.
        /// </summary>
        public static float GetDisplayEffectValue(TraitData data)
        {
            if (data == null) return 0f;
            int level = SaveSystem.GetTraitUpgradeLevel(SaveSystem.Load(), data.Id);
            return data.GetEffectiveValue(level);
        }

        public static string AppendTraitNumbers(TraitData data, string localizedDescription)
        {
            if (data == null)
                return localizedDescription ?? string.Empty;

            // Mastery traits move three stats at once, and the third is whatever that
            // turret type's signature mechanic is — so the line is built here, where
            // TargetTurretType is available, rather than in FormatTraitEffect (which
            // only sees the shared effect enum and could not name the mechanic).
            if (data.EffectType == TraitEffectType.TurretTypeMastery && data.TargetsTurretType)
                return AppendEffectLine(localizedDescription, FormatMasteryEffect(data));

            string effect = FormatTraitEffect(data.EffectType, GetDisplayEffectValue(data));
            if (data.TargetsTurretType)
                effect = $"{TurretTypeLabel(data.TargetTurretType)}: {effect}";
            return AppendEffectLine(localizedDescription, effect);
        }

        /// <summary>
        /// Builds a mastery trait's effect line from the SAME allocation the gameplay
        /// code applies (RunStatModifiers.GetMasteryAllocation), so the line can never
        /// advertise a stat the turret does not receive. This is why Support and Radar
        /// masteries show no damage/attack-speed entry — those types never attack —
        /// and why Laser shows range instead of attack speed.
        /// </summary>
        private static string FormatMasteryEffect(TraitData data)
        {
            float v = GetDisplayEffectValue(data);
            var type = data.TargetTurretType;
            ETD.Gameplay.RunStatModifiers.GetMasteryAllocation(
                type, out float dmg, out float spd, out float rng, out float sig);

            var parts = new System.Collections.Generic.List<string>(4);
            if (dmg > 0f)
                parts.Add($"{FormatPercentSigned(v * dmg)} " +
                          LocalizationManager.Get("trait_effect_type_bonus_damage", "damage"));
            if (spd > 0f)
                parts.Add($"{FormatPercentSigned(v * spd)} " +
                          LocalizationManager.Get("trait_effect_type_attack_speed", "attack speed"));
            if (rng > 0f)
                parts.Add($"{FormatPercentSigned(v * rng)} " +
                          LocalizationManager.Get("trait_effect_type_bonus_range", "turret range"));
            if (sig > 0f)
                parts.Add($"{FormatPercentSigned(v * sig)} {SignatureLabel(type)}");

            return $"{TurretTypeLabel(type)}: {string.Join(", ", parts)}";
        }

        /// <summary>Localized turret-type name, used to prefix the effect line of
        /// per-turret-type cards and traits so the number has a subject.</summary>
        internal static string TurretTypeLabel(TurretType type)
        {
            return LocalizationManager.Get(
                "turret_type_" + type.ToString().ToLowerInvariant(), type.ToString());
        }

        /// <summary>Localized name of a turret type's signature mechanic — the thing
        /// signature cards and mastery traits amplify (Void's armor corrosion, Frost's
        /// slow strength, and so on).</summary>
        private static string SignatureLabel(TurretType type)
        {
            string fallback = type switch
            {
                TurretType.Basic => "armor shred",
                TurretType.Frost => "slow strength",
                TurretType.Laser => "ramp ceiling",
                TurretType.Inferno => "burn damage",
                TurretType.Lightning => "chain damage",
                TurretType.Support => "aura power",
                TurretType.Radar => "reveal range",
                TurretType.Void => "armor corrosion",
                TurretType.Toxin => "pure damage",
                TurretType.Railgun => "expose power",
                _ => "signature effect"
            };
            return LocalizationManager.Get(
                "turret_signature_" + type.ToString().ToLowerInvariant(), fallback);
        }

        public static string AppendSpecCardNumbers(SpecCardData data, string localizedDescription)
        {
            if (data == null)
                return localizedDescription ?? string.Empty;

            // Signature cards name the mechanic they amplify, for the same reason
            // mastery traits do above.
            if (data.EffectType == SpecCardEffectType.TurretTypeSignature && data.TargetsTurretType)
            {
                string signature = $"{TurretTypeLabel(data.TargetTurretType)}: " +
                    $"{FormatPercentSigned(data.EffectValue)} {SignatureLabel(data.TargetTurretType)}";
                return AppendEffectLine(localizedDescription, signature);
            }

            string effect = FormatSpecCardEffect(data.EffectType, data.EffectValue, data.ConditionInterval, data.ConditionThreshold);
            if (data.TargetsTurretType)
                effect = $"{TurretTypeLabel(data.TargetTurretType)}: {effect}";
            return AppendEffectLine(localizedDescription, effect);
        }

        public static string FormatTraitEffect(TraitEffectType type, float value)
        {
            string pct = FormatPercentSigned(value);
            string header = "trait_effect_type_";
            return type switch
            {
                TraitEffectType.BonusRange => $"{pct} {LocalizationManager.Get(header + "bonus_range", "turret range")}",
                TraitEffectType.ReduceUpgradeCost => $"{FormatPercentSigned(-AbsPercent(value))} {LocalizationManager.Get(header + "reduce_upgrade_cost", "upgrade cost")}",
                TraitEffectType.BonusAttackSpeed => $"{pct} {LocalizationManager.Get(header + "attack_speed", "attack speed")}",
                TraitEffectType.BonusDamage => $"{pct} {LocalizationManager.Get(header + "bonus_damage", "damage")}",
                TraitEffectType.BonusEXP => $"{pct} {LocalizationManager.Get(header + "bonus_exp", "experience")}",
                TraitEffectType.SlowStrength => $"{pct} {LocalizationManager.Get(header + "slow_strength", "slow strength")}",
                TraitEffectType.ChainTargetBonus => $"+{value:0} {LocalizationManager.Get(header + "chain_target_bonus", "chain targets")}",
                TraitEffectType.BurnDuration => $"{pct} {LocalizationManager.Get(header + "burn_duration", "burn duration")}",
                TraitEffectType.CriticalHitChance => $"{pct} {LocalizationManager.Get(header + "critical_hit_chance", "critical chance")}",
                TraitEffectType.BonusGold => $"{pct} {LocalizationManager.Get(header + "bonus_gold", "gold gain")}",
                TraitEffectType.DamageVsHighHP => $"{pct} {LocalizationManager.Get(header + "damage_vs_high_hp", "damage vs elites/bosses")}",
                TraitEffectType.AuraPower => $"{pct} {LocalizationManager.Get(header + "aura_power", "support aura power")}",
                TraitEffectType.BurnSpread => $"{FormatPercentUnsigned(value)} {LocalizationManager.Get(header + "burn_spread", "burn spread chance")}",
                TraitEffectType.DamageVsFrozen => $"{pct} {LocalizationManager.Get(header + "damage_vs_frozen", "damage vs frozen/slowed")}",
                TraitEffectType.ChainBounceBack => $"{FormatPercentUnsigned(value)} {LocalizationManager.Get(header + "chain_bounce_back", "chain bounce chance")}",
                TraitEffectType.LaserRefraction => $"{FormatPercentSigned(value)} {LocalizationManager.Get(header + "laser_refraction", "laser refraction damage")}",
                TraitEffectType.DamagePerGoldSpent => $"{FormatPercentPlus(value)} {LocalizationManager.Get(header + "damage_per_gold_spent", "damage per 100 gold spent")}",
                TraitEffectType.GlobalSlowPulse => $"{FormatPercentUnsigned(value)} {LocalizationManager.Get(header + "global_slow_pulse", "global slow pulse")}",
                TraitEffectType.BurstDamageWindow => $"{pct} {LocalizationManager.Get(header + "burst_damage_window", "burst damage window")}",
                TraitEffectType.AllStatsPerWave => $"{FormatPercentPlus(value)} {LocalizationManager.Get(header + "all_stats_per_wave", "all stats per wave")}",
                TraitEffectType.MaxHPDecayPerSecond => $"{FormatPercentUnsigned(value)} {LocalizationManager.Get(header + "max_hp_decay_per_second", "current HP decay/s (can't kill)")}",
                TraitEffectType.DamagePerOwnedTurret => $"{pct} {LocalizationManager.Get(header + "damage_per_owned_turret", "damage per owned turret")}",
                TraitEffectType.GradeBonus => $"{pct} {LocalizationManager.Get(header + "grade_bonus", "higher rarity chance")}",
                TraitEffectType.FlameCovenant => $"{pct} {LocalizationManager.Get(header + "flame_covenant", "burn damage, -10% direct damage")}",
                TraitEffectType.FrostCovenant => $"{pct} {LocalizationManager.Get(header + "frost_covenant", "damage vs slowed/frozen, -10% attack speed")}",
                TraitEffectType.StormCovenant => $"{pct} {LocalizationManager.Get(header + "storm_covenant", "chain damage, +1 chain target, -10% direct damage")}",
                // Fallback only: reached if a mastery asset has TargetsTurretType off,
                // in which case there is no turret type whose mechanic we could name.
                TraitEffectType.TurretTypeMastery => $"{pct} {LocalizationManager.Get(header + "turret_type_mastery", "damage and half that attack speed")}",
                TraitEffectType.VoidCovenant => $"{pct} {LocalizationManager.Get(header + "void_covenant", "Void armor corrosion, +15% armor pierce, no critical hits")}",
                TraitEffectType.PlagueCovenant => $"{pct} {LocalizationManager.Get(header + "plague_covenant", "Toxin pure damage, leaks to all turrets, -50% burn damage")}",
                TraitEffectType.PrecisionCovenant => $"{pct} {LocalizationManager.Get(header + "precision_covenant", "Railgun Expose, +50% crit damage, -50% chain damage, -20% attack speed")}",
                _ => $"+{SmartDigits(value)} {type}"
            };
        }

        public static string FormatSpecCardEffect(SpecCardEffectType type, float value, int interval = 0, float threshold = 0f)
        {
            string pct = FormatPercentSigned(value);
            return type switch
            {
                SpecCardEffectType.DamagePercent => $"{pct} {L("spec_effect_damage_percent", "global damage")}",
                SpecCardEffectType.AttackSpeedPercent => $"{pct} {L("spec_effect_attack_speed_percent", "attack speed")}",
                SpecCardEffectType.RangePercent => $"{pct} {L("spec_effect_range_percent", "range")}",
                SpecCardEffectType.CritChance => $"{pct} {L("spec_effect_crit_chance", "critical chance")}",
                SpecCardEffectType.ProjectileDamage => $"{pct} {L("spec_effect_projectile_damage", "projectile damage")}",
                SpecCardEffectType.ChainDamage => $"{pct} {L("spec_effect_chain_damage", "chain damage")}",
                SpecCardEffectType.AuraRange => $"{pct} {L("spec_effect_aura_range", "aura range")}",
                SpecCardEffectType.BurnDamage => $"{pct} {L("spec_effect_burn_damage", "burn damage")}",
                SpecCardEffectType.SlowDuration => $"{pct} {L("spec_effect_slow_duration", "slow duration")}",
                SpecCardEffectType.BurnDamageStrong => $"{pct} {L("spec_effect_burn_damage", "burn damage")}",
                SpecCardEffectType.SlowStrength => $"{pct} {L("spec_effect_slow_strength", "slow strength")}",
                SpecCardEffectType.CritChanceStrong => $"{pct} {L("spec_effect_crit_chance", "critical chance")}",
                SpecCardEffectType.ChainRange => $"{pct} {L("spec_effect_chain_range", "chain range")}",
                SpecCardEffectType.ExtraProjectilePeriodic => $"+1 {L("spec_effect_extra_projectile", "extra projectile")} {L("spec_effect_every", "every")} {ResolveInterval(interval, 4)} {L("spec_effect_shots", "shots")}",
                SpecCardEffectType.SupportRadius => $"{pct} {L("spec_effect_support_radius", "support radius")}",
                SpecCardEffectType.DamageVsElite => $"{pct} {L("spec_effect_damage_vs_elite", "damage vs elites/bosses")}",
                SpecCardEffectType.UpgradeCostReduce => $"{FormatPercentSigned(-AbsPercent(value))} {L("spec_effect_upgrade_cost", "upgrade cost")}",
                SpecCardEffectType.ChainTargetBonus => $"+{value:0} {L("spec_effect_chain_targets", "chain targets")}",
                SpecCardEffectType.DamageVsBurning => $"{pct} {L("spec_effect_damage_vs_burning", "damage vs burning enemies")}",
                SpecCardEffectType.DamageVsSlowedFrozen => $"{pct} {L("spec_effect_damage_vs_slowed", "damage vs slowed/frozen enemies")}",
                SpecCardEffectType.GoldGain => $"{pct} {L("spec_effect_gold_gain", "gold gain")}",
                SpecCardEffectType.ShockChance => $"{FormatPercentUnsigned(value)} {L("spec_effect_shock_chance", "shock chance")}",
                SpecCardEffectType.DamagePerDistance => $"{FormatPercentPlus(value)} {L("spec_effect_damage_per_distance", "damage per tile distance")}",
                SpecCardEffectType.DamagePerGoldHeld => $"{FormatPercentPlus(value)} {L("spec_effect_damage_per_gold", "damage per 1000 gold held")}",
                SpecCardEffectType.DeathExplosion => $"{FormatPercentUnsigned(value)} {L("spec_effect_death_explosion", "death explosion chance")}",
                SpecCardEffectType.BurnSpreadOnDeath => $"{FormatPercentUnsigned(value)} {L("spec_effect_burn_spread_death", "burn spread on death")}",
                SpecCardEffectType.AttackSpeedPerNearbyEnemy => $"{FormatPercentPlus(value)} {L("spec_effect_attack_speed_nearby", "attack speed per nearby enemy")}",
                SpecCardEffectType.DamagePerWave => $"{FormatPercentPlus(value)} {L("spec_effect_damage_per_wave", "damage per wave")}",
                SpecCardEffectType.AttackSpeedPerWave => $"{FormatPercentPlus(value)} {L("spec_effect_attack_speed_per_wave", "attack speed per wave")}",
                SpecCardEffectType.DamagePerGoldHeldStrong => $"{FormatPercentPlus(value)} {L("spec_effect_damage_per_gold", "damage per 1000 gold held")}",
                SpecCardEffectType.FreezeAmplifier => $"{pct} {L("spec_effect_freeze_amplifier", "damage vs frozen enemies")}",
                SpecCardEffectType.HealHealth => $"+{value:0} {L("spec_effect_heal", "health")}",
                SpecCardEffectType.BurnFromHit => $"{FormatPercentPlus(value)} {L("spec_effect_burn_from_hit", "of hit damage added to burn")}",
                SpecCardEffectType.SupportExposure => $"{FormatPercentPlus(value)} {L("spec_effect_support_exposure", "burn/chain damage vs aura-slowed enemies")}",
                // Fallback only: AppendSpecCardNumbers names the actual mechanic when
                // TargetTurretType is set, which every shipped signature card has.
                SpecCardEffectType.TurretTypeSignature => $"{pct} {L("spec_effect_turret_signature", "to that turret type's signature effect")}",
                _ => $"+{SmartDigits(value)} {type}"
            };
        }

        private static string AppendEffectLine(string description, string effect)
        {
            // v8: show the numeric effect directly. No "Effect:" label, because the
            // card/tooltip already tells the player this line is the gameplay value.
            string line = $"<color=#FFC857>{effect}</color>";
            if (string.IsNullOrWhiteSpace(description))
                return line;
            return description + "\n" + line;
        }

        private static string L(string key, string fallback) => LocalizationManager.Get(key, fallback);

        private static int ResolveInterval(int interval, int fallback) => interval > 0 ? interval : fallback;

        private static float NormalizePercent(float value) => value > 1f ? value * 0.01f : value;
        private static float AbsPercent(float value) => value > 1f ? value * 0.01f : System.Math.Abs(value);

        // Adaptive precision so a small-but-nonzero effect never renders as "0" /
        // "0.00" (which made low per-wave / per-gold traits look like no effect).
        // >=1 shows up to 3 decimals so upgraded trait values (1% -> 1.75%, 1.045%)
        // are visibly different from base; >=0.01 up to 4; smaller up to 6.
        private static string SmartDigits(float x)
        {
            float a = System.Math.Abs(x);
            if (a < 1e-7f) return "0";
            if (a >= 1f) return x.ToString("0.###");
            if (a >= 0.01f) return x.ToString("0.####");
            return x.ToString("0.######");
        }

        private static string FormatPercentSigned(float value)
        {
            float p = NormalizePercent(value) * 100f;
            if (System.Math.Abs(p) < 1e-7f) return "0%";
            return (p > 0f ? "+" : "-") + SmartDigits(System.Math.Abs(p)) + "%";
        }

        private static string FormatPercentUnsigned(float value)
            => SmartDigits(System.Math.Abs(NormalizePercent(value)) * 100f) + "%";

        private static string FormatPercentPlus(float value)
            => "+" + SmartDigits(System.Math.Abs(NormalizePercent(value)) * 100f) + "%";
    }
}
