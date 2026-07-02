// ============================================================================
// ETD.Data - DynamicTileData.cs  [NEW - matches ETD_FULL_SHEET Dynamic Tiles]
// ScriptableObject defining individual dynamic tile effects.
// 3 Blessings, 3 Curses, 2 Greeds — each with unique stat/tradeoff combos.
// ============================================================================
using UnityEngine;
using ETD.Core;

namespace ETD.Data
{
    public enum DynamicTileType
    {
        // Blessings (pure bonus, no cost)
        SwiftBlessing,     // +40% attack speed
        PowerBlessing,     // +30% damage
        RangeBlessing,     // +35% range

        // Curses (penalty + compensation)
        FragileCurse,      // -35% damage, +80% gold from kills
        SluggishCurse,     // -40% attack speed, +70% damage
        BlindCurse,        // -50% range, +90% damage

        // Greeds (strong bonus + upgrade cost)
        BloodGreed,        // +60% damage, upgrade cost +50%
        SacrificeGreed     // -50% upgrade cost, -25% damage
    }

    [CreateAssetMenu(fileName = "New Dynamic Tile", menuName = "ETD/Dynamic Tile Data")]
    public class DynamicTileData : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public DynamicTileType TileType;
        public TileSpecialty Category; // Blessed, Cursed, or Greed

        [Header("Localization")]
        [Tooltip("Base key used for all localized fields. E.g. 'trait_tr001'")]
        public string LocalizationKey;

        [Header("Primary Effect")]
        public TurretStatModifier PrimaryEffect;

        [Header("Tradeoff (Curses & Greeds only)")]
        public TurretStatModifier Tradeoff;

        [Header("Visuals")]
        public Color TileColor = Color.white;
        public Material TileMaterial;
    }

    [System.Serializable]
    public struct TurretStatModifier
    {
        public StatType Stat;
        public float Value; // Positive = bonus, negative = penalty

        public enum StatType
        {
            None,
            Damage,
            AttackSpeed,
            Range,
            UpgradeCost,
            GoldFromKills
        }
    }
}
