// ============================================================================
// ETD.Data - RarityColorHelper.cs  [NEW]
// Extension method so any SpecCardRarity can call .GetRarityColor()
// Used by TraitData, SpecCardData, TooltipContentBuilder, etc.
// ============================================================================
using ETD.Core;
using UnityEngine;

namespace ETD.Data
{
    public static class RarityColorHelper
    {

        public static Color GetRarityColor(this SpecCardRarity rarity)
        {
            return rarity switch
            {
                SpecCardRarity.Common =>  new Color(0.0f, 1f, 0.0f),
                SpecCardRarity.Uncommon => new Color(0f, 0.0f, 1f),
                SpecCardRarity.Rare => new Color(1f, 1f, 0f),
                SpecCardRarity.Unique => new Color(1f, 0.0f, 0.0f),
                SpecCardRarity.Legendary => new Color(.5f, 0.0f, 1f),
                _ => Color.white
            };
        }

        public static string GetLocalizedName(this SpecCardRarity rarity)
    => LocalizationManager.Get($"rarity_{rarity.ToString().ToLower()}", rarity.ToString());
    }
}
