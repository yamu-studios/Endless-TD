// ============================================================================
// ETD.UI - TooltipContentBuilder.cs  [UPDATED - matches ETD_FULL_SHEET enums]
// ============================================================================
using UnityEngine;
using ETD.Data;
using ETD.Core;
using ETD.Turrets;

namespace ETD.UI
{
    public static class TooltipContentBuilder
    {
        public static TooltipContent FromScriptableObject(ScriptableObject so)
        {
            return so switch
            {
                TurretData t => FromTurretData(t),
                TraitData t => FromTraitData(t),
                SpecCardData s => FromSpecCard(s),
                EnemyData e => FromEnemyData(e),
                ChallengeData c => FromChallengeData(c),
                DynamicTileData d => FromDynamicTile(d),
                _ => TooltipContent.Simple(so.name, "No tooltip data available.")
            };
        }

        // === TURRETS ===

        public static TooltipContent FromTurretData(TurretData data)
        {
            string atkStr = data.IsContinuousBeam
                ? $"DPS: {data.Damage}"
                : $"Damage: {data.Damage}\nInterval: {data.AttackInterval}s";

            string stats = $"{atkStr}\nRange: {data.Range}\nCost: {data.Cost} gold";

            string footer = "";
            if (data.PathA != null)
                footer += $"Evo A: {data.PathA.Name}\n";
            if (data.PathB != null)
                footer += $"Evo B: {data.PathB.Name}";

            return new TooltipContent
            {
                Title = data.DisplayName,
                Body = data.Description ?? GetTurretTypeDescription(data.Type),
                Stats = stats,
                Footer = string.IsNullOrEmpty(footer) ? null : footer
            };
        }

        public static TooltipContent FromPlacedTurret(TurretController turret)
        {
            string name = turret.Data.DisplayName;
            if (turret.IsEvolved)
            {
                var evo = turret.EvolutionPath == 0 ? turret.Data.PathA : turret.Data.PathB;
                name = evo?.Name ?? name;
            }

            string stats = $"Level: {turret.Level}\n" +
                           $"Damage: {turret.Damage:F1}\n" +
                           $"Attack Speed: {turret.AttackSpeed:F2}/s\n" +
                           $"Range: {turret.Range:F1}";

            string footer = $"Upgrade: {turret.GetUpgradeCost()} gold\n" +
                            $"Sell: {turret.GetSellValue()} gold";

            return new TooltipContent
            {
                Title = name,
                Body = turret.Data.Description,
                Stats = stats,
                Footer = footer
            };
        }

        private static string GetTurretTypeDescription(TurretType type)
        {
            return type switch
            {
                TurretType.Basic => "Reliable single-target projectile shooter.",
                TurretType.Frost => "Control tower that slows enemies on hit.",
                TurretType.Laser => "Continuous beam damage, ideal vs high-HP.",
                TurretType.Inferno => "Burns enemies with scaling damage over time.",
                TurretType.Lightning => "Chain damage hitting multiple nearby enemies.",
                TurretType.Support => "Buffs nearby turrets with damage aura.",
                TurretType.Radar => "Reveals stealth enemies in range.",
                _ => ""
            };
        }

        // === TRAITS ===

        public static TooltipContent FromTraitData(TraitData data)
        {
            string lockInfo = data.IsUnlockedByDefault ? null : data.UnlockCondition;

            return new TooltipContent
            {
                Title = SOLocalization.GetName("trait_" + data.LocalizationKey, data.DisplayName),
                Body = BalanceDescriptionFormatter.AppendTraitNumbers(
                    data,
                    SOLocalization.GetDesc("trait_" + data.LocalizationKey, data.Description)),
                Stats = BalanceDescriptionFormatter.FormatTraitEffect(data.EffectType, data.EffectValue),
                TitleColor = data.GetRarityColor(),
                HasTitleColor = data.Grade >= SpecCardRarity.Rare
            };
        }

        private static string FormatTraitEffect(TraitEffectType type, float value)
        {
            float mulitplier = 1;
            if (value <= 1) mulitplier = 100;
            string pct = $"{value * mulitplier:F0}%";
            string header = "trait_effect_type_";
            return type switch
            {
                TraitEffectType.BonusRange => $"+{pct} {LocalizationManager.Get(header+"bonus_range", "turret range")}",
                TraitEffectType.ReduceUpgradeCost => $"-{pct} {LocalizationManager.Get(header + "reduce_upgrade_cost", "upgrade cost")}",
                TraitEffectType.BonusAttackSpeed => $"+{pct} {LocalizationManager.Get(header + "attack_speed", "attack speed")}",
                TraitEffectType.BonusDamage => $"+{pct} {LocalizationManager.Get(header + "bonus_damage", "damage")}",
                TraitEffectType.BonusEXP => $"+{pct} {LocalizationManager.Get(header + "bonus_exp", "experience")}",
                TraitEffectType.SlowStrength => $"+{pct} {LocalizationManager.Get(header + "slow_strength", "slow strength")}",
                TraitEffectType.ChainTargetBonus => $"+{value:F0} {LocalizationManager.Get(header + "chain_target_bonus", "chain targets")}",
                TraitEffectType.BurnDuration => $"+{pct} {LocalizationManager.Get(header + "burn_duration", "burn duration")}",
                TraitEffectType.CriticalHitChance => $"+{pct} {LocalizationManager.Get(header + "critical_hit_chance", "crit_chance")}",
                TraitEffectType.BonusGold => $"+{pct} {LocalizationManager.Get(header + "bonus_gold", "gold gain")}",
                TraitEffectType.DamageVsHighHP => $"+{pct} {LocalizationManager.Get(header + "damage_vs_high_hp", "vs elites/bosses")}",
                TraitEffectType.AuraPower => $"+{pct} {LocalizationManager.Get(header + "aura_power", "support aura power")}",
                TraitEffectType.BurnSpread => $"{pct} {LocalizationManager.Get(header + "burn_spread", "burn spread on kill")}",
                TraitEffectType.DamageVsFrozen => $"+{pct} {LocalizationManager.Get(header + "damage_vs_frozen", "vs frozen enemies")}",
                TraitEffectType.ChainBounceBack => $"{pct} {LocalizationManager.Get(header + "chain_bounce_back", "chain bounce back")}",
                TraitEffectType.LaserRefraction => $"+{value:F0} {LocalizationManager.Get(header + "laser_refraction", "beam refraction")}",
                TraitEffectType.DamagePerGoldSpent => $"+{value:F2}% {LocalizationManager.Get(header + "damage_per_gold_spent", "dmg per 100g spent")}",
                TraitEffectType.GlobalSlowPulse => $"{pct} {LocalizationManager.Get(header + "global_slow_pulse", "global slow pulse")}",
                TraitEffectType.BurstDamageWindow => $"+{pct} {LocalizationManager.Get(header + "burst_damage_window", "burst window")}",
                TraitEffectType.AllStatsPerWave => $"+{value:F2}% {LocalizationManager.Get(header + "all_stats_per_wave", "all stats/wave")}",
                TraitEffectType.MaxHPDecayPerSecond => $"{value:F1}% {LocalizationManager.Get(header + "max_hp_decay_per_second", "HP decay/s")}",
                TraitEffectType.DamagePerOwnedTurret => $"+{pct} {LocalizationManager.Get(header + "damage_per_owned_turret", "per owned turret")}",
                TraitEffectType.GradeBonus => $"+{pct} {LocalizationManager.Get(header + "grade_bonus", "better grade chance")}",
                _ => $"+{value} {type}"
            };
        }

       

        // === SPEC CARDS ===

        public static TooltipContent FromSpecCard(SpecCardData data)
        {
            string lockInfo = data.IsUnlockedByDefault ? null : data.UnlockCondition;

            return new TooltipContent
            {
                Title = SOLocalization.GetName("speccard_" + data.LocalizationKey, data.DisplayName),
                Body = BalanceDescriptionFormatter.AppendSpecCardNumbers(
                    data,
                    SOLocalization.GetDesc("speccard_" + data.LocalizationKey, data.Description)),
                Stats = $"{LocalizationManager.Get("spec_theme", "Theme")}: {data.Theme}\n{LocalizationManager.Get("rarity", "Rarity")}: {RarityColorHelper.GetLocalizedName(data.Rarity)}",
                Footer = string.IsNullOrEmpty(lockInfo) ? null : $"<color=#FF6666>Unlock: {lockInfo}</color>",
                TitleColor = data.GetRarityColor(),
                HasTitleColor = true
            };
        }

        // === ENEMIES ===

        public static TooltipContent FromEnemyData(EnemyData data)
        {
            return new TooltipContent
            {
                Title = data.DisplayName,
                Body = GetEnemyTypeDescription(data.Type),
                Stats = $"HP: {data.MaxHealth}\nSpeed: {data.MoveSpeed}\nArmor: {data.Armor}",
                TitleColor = new Color(1f, 0.4f, 0.4f),
                HasTitleColor = true
            };
        }

        private static string GetEnemyTypeDescription(EnemyType type)
        {
            return type switch
            {
                EnemyType.Basic => "Standard enemy.",
                EnemyType.Tank => "High HP and armor.",
                EnemyType.Stealth => "Invisible unless revealed by Radar.",
                EnemyType.Berserk => "Moves faster as health drops.",
                EnemyType.Splitter => "Splits into two on first death.",
                EnemyType.Sprinter => "Moves very fast.",
                EnemyType.Debuffer => "Reduces nearby turret stats.",
                EnemyType.Buffer => "Strengthens nearby enemies.",
                _ => ""
            };
        }

        // === CHALLENGES ===

        public static TooltipContent FromChallengeData(ChallengeData data)
        {
            return new TooltipContent
            {
                Title = data.DisplayName,
                Body = data.Description,
                Stats = $"Target: {data.TargetValue}"
            };
        }

        // === DYNAMIC TILES ===

        public static TooltipContent FromDynamicTile(DynamicTileData data)
        {
            string primary = FormatTileMod(data.PrimaryEffect, "");//Bonus
            string tradeoff = FormatTileMod(data.Tradeoff, "");//Cost

            Color titleColor = data.Category switch
            {
                TileSpecialty.Blessed => new Color(0.3f, 0.9f, 0.5f),
                TileSpecialty.Cursed => new Color(0.9f, 0.3f, 0.3f),
                TileSpecialty.Greed => new Color(1f, 0.8f, 0.2f),
                _ => Color.white
            };

            return new TooltipContent
            {
                Title = SOLocalization.GetName("dynamic_tile_" + data.LocalizationKey, data.DisplayName),
                Body = SOLocalization.GetDesc("dynamic_tile_" + data.LocalizationKey, data.Description),
                Stats = primary,
                Footer = string.IsNullOrEmpty(tradeoff) ? null : tradeoff,
                TitleColor = titleColor,
                HasTitleColor = true
            };
        }

        private static string FormatTileMod(TurretStatModifier mod, string label)
        {
            if (mod.Stat == TurretStatModifier.StatType.None) return "";
            string pct = $"{mod.Value * 100f:+0;-0}%";
            string statName = mod.Stat switch
            {
                TurretStatModifier.StatType.Damage => LocalizationManager.Get("damage","Damage"),
                TurretStatModifier.StatType.AttackSpeed => LocalizationManager.Get("attack_speed", "Attack Speed"),
                TurretStatModifier.StatType.Range => LocalizationManager.Get("range", "Range"),
                TurretStatModifier.StatType.UpgradeCost => LocalizationManager.Get("upgrade_cost", "Upgrade Cost"),
                TurretStatModifier.StatType.GoldFromKills => LocalizationManager.Get("gold_from_kills", "Gold from Kills"),
                _ => mod.Stat.ToString()
            };
            return $"{label}: {pct} {statName}";
        }
    }
}
