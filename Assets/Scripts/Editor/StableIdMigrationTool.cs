// ============================================================================
// ETD.EditorTools - StableIdMigrationTool.cs
// One-time editor tool for migrating numeric ScriptableObject IDs to stable IDs.
// Put in: Assets/Scripts/Editor/StableIdMigrationTool.cs
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ETD.EditorTools
{
    public static class StableIdMigrationTool
    {
        private static readonly Dictionary<string, string> StableIdsByAssetName = new()
        {
            // ----------------------------------------------------------------
            // TURRETS
            // ----------------------------------------------------------------
            { "Tower_Basic",     "turret_basic" },
            { "Tower_Frost",     "turret_frost" },
            { "Tower_Inferno",   "turret_inferno" },
            { "Tower_Laser",     "turret_laser" },
            { "Tower_Lightning", "turret_lightning" },
            { "Tower_Radar",     "turret_radar" },
            { "Tower_Support",   "turret_support" },

            // ----------------------------------------------------------------
            // TRAITS
            // ----------------------------------------------------------------
            { "Trait_Sharpshooter",       "trait_sharpshooter" },
            { "Trait_EfficientBuilder",   "trait_efficient_builder" },
            { "Trait_RapidFire",          "trait_rapid_fire" },
            { "Trait_ReinforcedAmmo",     "trait_reinforced_ammo" },
            { "Trait_ColdEfficiency",     "trait_cold_efficiency" },
            { "Trait_ChainMastery",       "trait_chain_mastery" },
            { "Trait_BurningFocus",       "trait_burning_focus" },
            { "Trait_CriticalProtocol",   "trait_critical_protocol" },
            { "Trait_Economist",          "trait_economist" },
            { "Trait_Adaptive Defense",   "trait_adaptive_defense" },
            { "Trait_AuraAmplifier",      "trait_aura_amplifier" },
            { "Trait_Thermal Cascade",    "trait_thermal_cascade" },
            { "Trait_Frozen Fragility",   "trait_frozen_fragility" },
            { "Trait_Chain Overload",     "trait_chain_overload" },
            { "Trait_Singularity Core",   "trait_singularity_core" },
            { "Trait_Time Distortion",    "trait_time_distortion" },
            { "Trait_Infinite Scaling",   "trait_infinite_scaling" },
            { "Trait_Entropy Engine",     "trait_entropy_engine" },
            { "Trait_Architect of Doom",  "trait_architect_of_doom" },

            // If you still have an Overcharge asset later, this is the intended ID.
            { "Trait_Overcharge",         "trait_overcharge" },

            // ----------------------------------------------------------------
            // SPEC CARDS
            // ----------------------------------------------------------------
            { "Damage Boost",           "spec_damage_boost" },
            { "Speed Boost",            "spec_speed_boost" },
            { "Range Boost",            "spec_range_boost" },
            { "Precision Sights",       "spec_precision_sights" },
            { "Packed Rounds",          "spec_packed_rounds" },
            { "Stronger Sparks",        "spec_stronger_sparks" },
            { "Hardened Components",    "spec_hardened_components" },
            { "Burn Primer",            "spec_burn_primer" },
            { "Chill Rounds",           "spec_chill_rounds" },
            { "Slow Boost",             "spec_slow_boost" },
            { "Burn Boost",             "spec_burn_boost" },
            { "Crit Boost",             "spec_crit_boost" },
            { "Chain Reach",            "spec_chain_reach" },
            { "Double Barrel",          "spec_double_barrel" },
            { "Elite Hunter",           "spec_elite_hunter" },
            { "Cost Control",           "spec_cost_control" },
            { "Chain Boost",            "spec_chain_boost" },
            { "Ignite Weakness",        "spec_ignite_weakness" },
            { "Frozen Core",            "spec_frozen_core" },
            { "Rapid Logistics",        "spec_rapid_logistics" },
            { "Arc Pressure",           "spec_arc_pressure" },
            { "Sniper Doctrine",        "spec_sniper_doctrine" },
            { "Wealth Loop",            "spec_wealth_loop" },
            { "Explosion",              "spec_explosion" },
            { "Thermal Bloom",          "spec_thermal_bloom" },
            { "Overrun Battery",        "spec_overrun_battery" },
            { "Infinite Damage",        "spec_infinite_damage" },
            { "Endless Tempo",          "spec_endless_tempo" },
            { "Doom Reserve",           "spec_doom_reserve" },
            { "Zero Law",               "spec_zero_law" },
            { "Support Radius",         "spec_support_radius" },

            // ----------------------------------------------------------------
            // ENEMIES
            // ----------------------------------------------------------------
            { "Enemy_Berserker",                         "enemy_berserker" },
            { "Enemy_Basic",                             "enemy_basic" },
            { "Enemy_Splitter",                          "enemy_splitter" },
            { "Enemy_Fast",                              "enemy_fast" },
            { "Enemy_Tank",                              "enemy_tank" },
            { "Enemy_Stealth",                           "enemy_stealth" },
            { "Enemy_SplitterChild",                     "enemy_splitter_child" },
            { "Enmy_Debuffer",                           "enemy_debuffer" },
            { "Enemy_Debuffer",                          "enemy_debuffer" },
            { "Enmy_Support",                            "enemy_support" },
            { "Enemy_Support",                           "enemy_support" },
            { "Enemy_Elite_Foe",                         "enemy_elite_foe" },
            { "Enemy_Elite_Tank_Berserker",              "enemy_elite_tank_berserker" },
            { "Enemy_Elite_Tank_Speed_Dragon_Albino",    "enemy_elite_tank_speed_dragon_albino" },
            { "Enemy_Elite_Tank_Speed_Dragon_Blue",      "enemy_elite_tank_speed_dragon_blue" },
            { "Enemy_Elite_Tank_Speed_Dragon_Green",     "enemy_elite_tank_speed_dragon_green" },
            { "Enemy_Elite_Tank_Speed_Dragon_Purple",    "enemy_elite_tank_speed_dragon_purple" },
            { "Enemy_Elite_Tank_Splitter",               "enemy_elite_tank_splitter" },
            { "Enemy_Elite_Tank_Tank_Golem",             "enemy_elite_tank_golem" },
            { "Enemy_Elite_SplitterEliteChild",          "enemy_elite_splitter_child" },

            // ----------------------------------------------------------------
            // CHALLENGES
            // ----------------------------------------------------------------
            { "First Defense",      "challenge_first_defense" },
            { "Builder",            "challenge_builder" },
            { "Upgrader",           "challenge_upgrader" },
            { "Elementalist",       "challenge_elementalist" },
            { "Burn Master",        "challenge_burn_master" },
            { "Frost Control",      "challenge_frost_control" },
            { "Lightning Network",  "challenge_lightning_network" },
            { "Laser Focus",        "challenge_laser_focus" },
            { "Economic Genius",    "challenge_economic_genius" },
            { "No Support",         "challenge_no_support" },
            { "Risk Taker",         "challenge_risk_taker" },
            { "Endless Survivor",   "challenge_endless_survivor" },
            { "Scaling Master",     "challenge_scaling_master" },
            { "Tile Strategist",    "challenge_tile_strategist" },
            { "Greed Gambler",      "challenge_greed_gambler" },
            { "Perfect Defense",    "challenge_perfect_defense" },
            { "Overkill",           "challenge_overkill" },
            { "Combo Master",       "challenge_combo_master" },

            // ----------------------------------------------------------------
            // DYNAMIC TILES
            // ----------------------------------------------------------------
            { "Blessed", "dynamic_tile_blessed" },
            { "Cursed",  "dynamic_tile_cursed" },
            { "Greed",   "dynamic_tile_greed" },
        };

        [MenuItem("Tools/ETD/IDs/Dry Run Stable ID Migration")]
        public static void DryRun()
        {
            Migrate(apply: false);
        }

        [MenuItem("Tools/ETD/IDs/Apply Stable ID Migration")]
        public static void Apply()
        {
            bool ok = EditorUtility.DisplayDialog(
                "Apply Stable ID Migration?",
                "This will edit ScriptableObject Id fields for known ETD assets.\n\nUse version control or back up your project first.",
                "Apply",
                "Cancel");

            if (!ok) return;
            Migrate(apply: true);
        }

        private static void Migrate(bool apply)
        {
            int scanned = 0;
            int changed = 0;
            int alreadyOk = 0;
            int missingIdField = 0;
            int unknown = 0;

            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset == null) continue;

                if (!StableIdsByAssetName.TryGetValue(asset.name, out string stableId))
                {
                    unknown++;
                    continue;
                }

                scanned++;

                SerializedObject so = new SerializedObject(asset);
                SerializedProperty idProp = so.FindProperty("Id");

                if (idProp == null || idProp.propertyType != SerializedPropertyType.String)
                {
                    Debug.LogWarning($"[StableIdMigration] '{asset.name}' has no string Id field. Path: {path}", asset);
                    missingIdField++;
                    continue;
                }

                string oldId = idProp.stringValue;

                if (oldId == stableId)
                {
                    alreadyOk++;
                    continue;
                }

                string msg = $"[StableIdMigration] {(apply ? "Changed" : "Would change")} '{asset.name}' Id: '{oldId}' -> '{stableId}'";
                Debug.Log(msg, asset);

                if (apply)
                {
                    idProp.stringValue = stableId;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(asset);
                }

                changed++;
            }

            if (apply)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                $"[StableIdMigration] Done. Apply={apply}. Known scanned={scanned}, changed={changed}, already OK={alreadyOk}, missing Id field={missingIdField}, unknown ScriptableObjects ignored={unknown}.");
        }
    }
}
#endif
