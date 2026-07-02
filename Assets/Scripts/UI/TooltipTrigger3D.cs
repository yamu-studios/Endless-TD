// =========================================================================
// TILE TOOLTIP TRIGGER 3D
// A lightweight component that tells TooltipManager to show a tooltip
// when the mouse is over this 3D object.
// Uses OnMouseEnter/OnMouseExit — requires a Collider on the GO.
// =========================================================================
using ETD.Core;
using ETD.Data;
using UnityEngine;

namespace ETD.UI
{
    public class TileTooltipTrigger3D : MonoBehaviour
    {
        private TooltipContent _content;

        public void SetData(DynamicTileData data)
        {
            if (data == null) return;

            // Build tooltip from DynamicTileData
            string title = SOLocalization.GetName("dynamic_tile_"+data.LocalizationKey,data.DisplayName);
            //string body = SOLocalization.GetDesc("dynamic_tile_" + data.LocalizationKey, data.DisplayName);

            // Effect summary
            string stats = "";
            if (data.PrimaryEffect.Stat != TurretStatModifier.StatType.None)
                stats += FormatEffect(data.PrimaryEffect);
            if (data.Tradeoff.Stat != TurretStatModifier.StatType.None)
            {
                if (stats.Length > 0) stats += "\n";
                stats += FormatEffect(data.Tradeoff);
            }

            // Color title by specialty type
            Color titleColor = data.Category switch
            {
                TileSpecialty.Cursed => new Color(1f, 0.3f, 0.3f),
                TileSpecialty.Blessed => new Color(0.4f, 1f, 0.4f),
                TileSpecialty.Greed => new Color(1f, 0.85f, 0.2f),
                _ => Color.white
            };

            _content = new TooltipContent
            {
                Title = title,
                
                Stats = stats,
                TitleColor = titleColor,
                HasTitleColor = true
            };
        }

        private static string FormatEffect(TurretStatModifier mod)
        {
            float pct = Mathf.Abs(mod.Value);
            string sign = mod.Value >= 0 ? "+" : "-";
            string statName = mod.Stat switch
            {
                TurretStatModifier.StatType.Damage => LocalizationManager.Get("damage", "Damage"),
                TurretStatModifier.StatType.AttackSpeed => LocalizationManager.Get("attack_speed", "Attack Speed"),
                TurretStatModifier.StatType.Range => LocalizationManager.Get("range", "Range"),
                TurretStatModifier.StatType.UpgradeCost => LocalizationManager.Get("upgrade_cost", "Upgrade Cost"),
                TurretStatModifier.StatType.GoldFromKills => LocalizationManager.Get("gold_from_kills", "Gold from Kills"),
                _ => mod.Stat.ToString()
            };
            return $"{sign}{pct*100f:F0}% {statName}";
        }

        private void OnMouseEnter()
        {

            if (TooltipManager.Instance == null) return;
            TooltipManager.Instance.Show(_content);
        }

        private void OnMouseExit()
        {
            TooltipManager.Instance?.Hide();
        }
    }
}