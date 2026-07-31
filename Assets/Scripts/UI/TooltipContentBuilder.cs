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

            // Description in Body, numeric effect in Stats (matching the turret
            // tooltip convention). Previously the effect was appended to Body AND
            // shown in Stats, so it appeared twice.
            return new TooltipContent
            {
                Title = SOLocalization.GetName("trait_" + data.LocalizationKey, data.DisplayName),
                Body = SOLocalization.GetDesc("trait_" + data.LocalizationKey, data.Description),
                // Effective value (base * shop upgrades), so upgraded traits are visible.
                Stats = BalanceDescriptionFormatter.FormatTraitEffect(
                    data.EffectType, BalanceDescriptionFormatter.GetDisplayEffectValue(data)),
                TitleColor = data.GetRarityColor(),
                HasTitleColor = data.Grade >= SpecCardRarity.Rare
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
                Stats = $"HP: {data.MaxHealth}\nSpeed: {data.MoveSpeed}\nArmor: {data.Armor:0%}",
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
                EnemyType.Regenerator => "Heals itself over time.",
                EnemyType.Flying => "Flies straight to the exit, ignoring the path.",
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
