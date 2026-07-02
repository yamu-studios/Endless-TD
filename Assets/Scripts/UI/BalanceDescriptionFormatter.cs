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
        public static string AppendTraitNumbers(TraitData data, string localizedDescription)
        {
            if (data == null)
                return localizedDescription ?? string.Empty;

            string effect = FormatTraitEffect(data.EffectType, data.EffectValue);
            return AppendEffectLine(localizedDescription, effect);
        }

        public static string AppendSpecCardNumbers(SpecCardData data, string localizedDescription)
        {
            if (data == null)
                return localizedDescription ?? string.Empty;

            string effect = FormatSpecCardEffect(data.EffectType, data.EffectValue, data.ConditionInterval, data.ConditionThreshold);
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
                TraitEffectType.DamagePerGoldSpent => $"+{NormalizePercent(value) * 100f:0.##}% {LocalizationManager.Get(header + "damage_per_gold_spent", "damage per 100 gold spent")}",
                TraitEffectType.GlobalSlowPulse => $"{FormatPercentUnsigned(value)} {LocalizationManager.Get(header + "global_slow_pulse", "global slow pulse")}",
                TraitEffectType.BurstDamageWindow => $"{pct} {LocalizationManager.Get(header + "burst_damage_window", "burst damage window")}",
                TraitEffectType.AllStatsPerWave => $"+{NormalizePercent(value) * 100f:0.##}% {LocalizationManager.Get(header + "all_stats_per_wave", "all stats per wave")}",
                TraitEffectType.MaxHPDecayPerSecond => $"{FormatPercentUnsigned(value)} {LocalizationManager.Get(header + "max_hp_decay_per_second", "max HP decay per second")}",
                TraitEffectType.DamagePerOwnedTurret => $"{pct} {LocalizationManager.Get(header + "damage_per_owned_turret", "damage per owned turret")}",
                TraitEffectType.GradeBonus => $"{pct} {LocalizationManager.Get(header + "grade_bonus", "higher rarity chance")}",
                _ => $"+{value:0.##} {type}"
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
                SpecCardEffectType.DamagePerDistance => $"+{NormalizePercent(value) * 100f:0.##}% {L("spec_effect_damage_per_distance", "damage per tile distance")}",
                SpecCardEffectType.DamagePerGoldHeld => $"+{NormalizePercent(value) * 100f:0.##}% {L("spec_effect_damage_per_gold", "damage per 1000 gold held")}",
                SpecCardEffectType.DeathExplosion => $"{FormatPercentUnsigned(value)} {L("spec_effect_death_explosion", "death explosion chance")}",
                SpecCardEffectType.BurnSpreadOnDeath => $"{FormatPercentUnsigned(value)} {L("spec_effect_burn_spread_death", "burn spread on death")}",
                SpecCardEffectType.AttackSpeedPerNearbyEnemy => $"+{NormalizePercent(value) * 100f:0.##}% {L("spec_effect_attack_speed_nearby", "attack speed per nearby enemy")}",
                SpecCardEffectType.DamagePerWave => $"+{NormalizePercent(value) * 100f:0.##}% {L("spec_effect_damage_per_wave", "damage per wave")}",
                SpecCardEffectType.AttackSpeedPerWave => $"+{NormalizePercent(value) * 100f:0.##}% {L("spec_effect_attack_speed_per_wave", "attack speed per wave")}",
                SpecCardEffectType.DamagePerGoldHeldStrong => $"+{NormalizePercent(value) * 100f:0.##}% {L("spec_effect_damage_per_gold", "damage per 1000 gold held")}",
                SpecCardEffectType.FreezeAmplifier => $"{pct} {L("spec_effect_freeze_amplifier", "damage vs frozen enemies")}",
                SpecCardEffectType.HealHealth => $"+{value:0} {L("spec_effect_heal", "health")}",
                _ => $"+{value:0.##} {type}"
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
        private static string FormatPercentSigned(float value) => $"{NormalizePercent(value) * 100f:+0.##;-0.##;0}%";
        private static string FormatPercentUnsigned(float value) => $"{System.Math.Abs(NormalizePercent(value)) * 100f:0.##}%";
    }
}
