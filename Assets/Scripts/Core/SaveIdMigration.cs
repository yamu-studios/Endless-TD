// ============================================================================
// ETD.Core - SaveIdMigration.cs
// Runtime save migration for moving from numeric IDs to stable string IDs.
// Put in: Assets/Scripts/Core/SaveIdMigration.cs
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace ETD.Core
{
    public static class SaveIdMigration
    {
        public const int CurrentSaveVersion = 2;

        private static readonly Dictionary<string, string> IdMap = new()
        {
            // Turrets
            { "basic_turret", "turret_basic" },
            { "0", "turret_basic" },
            { "1", "turret_frost" },
            { "2", "turret_inferno" },
            { "3", "turret_laser" },
            { "4", "turret_lightning" },
            { "5", "turret_radar" },
            { "6", "turret_support" },

            // Traits
            { "trait_0", "trait_sharpshooter" },
            { "trait_1", "trait_efficient_builder" },
            { "trait_2", "trait_rapid_fire" },
            { "trait_3", "trait_reinforced_ammo" },
            { "trait_4", "trait_cold_efficiency" },
            { "trait_5", "trait_chain_mastery" },
            { "trait_6", "trait_burning_focus" },
            { "trait_7", "trait_critical_protocol" },
            { "trait_8", "trait_economist" },
            { "trait_9", "trait_adaptive_defense" },
            { "trait_10", "trait_aura_amplifier" },
            { "trait_11", "trait_thermal_cascade" },
            { "trait_12", "trait_frozen_fragility" },
            { "trait_13", "trait_chain_overload" },
            { "trait_14", "trait_singularity_core" },
            { "trait_15", "trait_time_distortion" },
            { "trait_17", "trait_infinite_scaling" },
            { "trait_18", "trait_entropy_engine" },
            { "trait_19", "trait_architect_of_doom" },

            // Challenges
            { "challenge_0", "challenge_first_defense" },
            { "challenge_1", "challenge_builder" },
            { "challenge_2", "challenge_upgrader" },
            { "challenge_3", "challenge_elementalist" },
            { "challenge_4", "challenge_burn_master" },
            { "challenge_5", "challenge_frost_control" },
            { "challenge_6", "challenge_lightning_network" },
            { "challenge_7", "challenge_laser_focus" },
            { "challenge_8", "challenge_economic_genius" },
            { "challenge_9", "challenge_no_support" },
            { "challenge_10", "challenge_risk_taker" },
            { "challenge_11", "challenge_endless_survivor" },
            { "challenge_12", "challenge_scaling_master" },
            { "challenge_13", "challenge_tile_strategist" },
            { "challenge_14", "challenge_greed_gambler" },
            { "challenge_15", "challenge_perfect_defense" },
            { "challenge_16", "challenge_overkill" },
            { "challenge_17", "challenge_combo_master" },
        };

        // Context-specific maps are needed because old numeric IDs overlapped.
        // Example: turret "0", trait "0", and challenge "0" are different things.
        private static readonly Dictionary<string, string> TurretIdMap = new()
        {
            { "basic_turret", "turret_basic" },
            { "0", "turret_basic" },
            { "1", "turret_frost" },
            { "2", "turret_inferno" },
            { "3", "turret_laser" },
            { "4", "turret_lightning" },
            { "5", "turret_radar" },
            { "6", "turret_support" },
        };

        private static readonly Dictionary<string, string> TraitIdMap = new()
        {
            { "0", "trait_sharpshooter" },
            { "1", "trait_efficient_builder" },
            { "2", "trait_rapid_fire" },
            { "3", "trait_reinforced_ammo" },
            { "4", "trait_cold_efficiency" },
            { "4 ", "trait_cold_efficiency" },
            { "5", "trait_chain_mastery" },
            { "6", "trait_burning_focus" },
            { "7", "trait_critical_protocol" },
            { "8", "trait_economist" },
            { "9", "trait_adaptive_defense" },
            { "10", "trait_aura_amplifier" },
            { "11", "trait_thermal_cascade" },
            { "12", "trait_frozen_fragility" },
            { "13", "trait_chain_overload" },
            { "14", "trait_singularity_core" },
            { "15", "trait_time_distortion" },
            { "17", "trait_infinite_scaling" },
            { "18", "trait_entropy_engine" },
            { "19", "trait_architect_of_doom" },
        };

        private static readonly Dictionary<string, string> ChallengeIdMap = new()
        {
            { "0", "challenge_first_defense" },
            { "1", "challenge_builder" },
            { "2", "challenge_upgrader" },
            { "3", "challenge_elementalist" },
            { "4", "challenge_burn_master" },
            { "5", "challenge_frost_control" },
            { "6", "challenge_lightning_network" },
            { "7", "challenge_laser_focus" },
            { "8", "challenge_economic_genius" },
            { "9", "challenge_no_support" },
            { "10", "challenge_risk_taker" },
            { "11", "challenge_endless_survivor" },
            { "12", "challenge_scaling_master" },
            { "13", "challenge_tile_strategist" },
            { "14", "challenge_greed_gambler" },
            { "15", "challenge_perfect_defense" },
            { "16", "challenge_overkill" },
            { "17", "challenge_combo_master" },
        };

        public static bool Migrate(SaveData data)
        {
            if (data == null) return false;

            bool changed = false;

            changed |= MigrateArray(ref data.UnlockedTurretIds, TurretIdMap);
            changed |= MigrateArray(ref data.UnlockedTraitIds, TraitIdMap);
            changed |= MigrateArray(ref data.SelectedTraitIds, TraitIdMap);
            changed |= MigrateArray(ref data.TraitUpgradeIds, TraitIdMap);
            changed |= MigrateArray(ref data.CompletedChallengeIds, ChallengeIdMap);

            // Make sure a fresh/old save always has the basic turret.
            if (data.UnlockedTurretIds == null || data.UnlockedTurretIds.Length == 0)
            {
                data.UnlockedTurretIds = new[] { "turret_basic" };
                changed = true;
            }

            // Remove the old default if it survived beside the new one.
            changed |= RemoveDuplicates(ref data.UnlockedTurretIds);
            changed |= RemoveDuplicates(ref data.UnlockedTraitIds);
            changed |= RemoveDuplicates(ref data.SelectedTraitIds);
            changed |= RemoveDuplicates(ref data.TraitUpgradeIds);
            changed |= RemoveDuplicates(ref data.CompletedChallengeIds);

            if (data.SaveVersion < CurrentSaveVersion)
            {
                data.SaveVersion = CurrentSaveVersion;
                changed = true;
            }

            if (changed)
                Debug.Log("[SaveIdMigration] Save data migrated to stable IDs.");

            return changed;
        }

        private static bool MigrateArray(ref string[] ids, Dictionary<string, string> map)
        {
            if (ids == null) return false;

            bool changed = false;

            for (int i = 0; i < ids.Length; i++)
            {
                string oldId = ids[i];

                if (string.IsNullOrWhiteSpace(oldId))
                    continue;

                string key = oldId.Trim();

                if (map.TryGetValue(key, out string newId) && oldId != newId)
                {
                    ids[i] = newId;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool RemoveDuplicates(ref string[] ids)
        {
            if (ids == null || ids.Length <= 1) return false;

            var unique = new List<string>();
            var seen = new HashSet<string>();

            foreach (string id in ids)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (seen.Add(id)) unique.Add(id);
            }

            if (unique.Count == ids.Length) return false;

            ids = unique.ToArray();
            return true;
        }
    }
}
