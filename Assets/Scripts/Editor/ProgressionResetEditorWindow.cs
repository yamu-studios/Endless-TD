#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ETD.Core;
using ETD.Data;
using UnityEditor;
using UnityEngine;

namespace ETD.EditorTools
{
    /// <summary>
    /// Editor-only save reset tool for testing ETD progression unlocks.
    /// Location: Assets/Scripts/Editor/ProgressionResetEditorWindow.cs
    /// Menu: Tools/ETD/Save/Progression Reset Window
    /// </summary>
    public class ProgressionResetEditorWindow : EditorWindow
    {
        private const string BasicTurretFallbackId = "turret_basic";

        private Vector2 _scroll;
        private GameDatabase _database;
        private string _status = "";

        [MenuItem("Tools/ETD/Save/Progression Reset Window")]
        public static void Open()
        {
            GetWindow<ProgressionResetEditorWindow>("ETD Progression Reset");
        }

        [MenuItem("Tools/ETD/Save/Reset/Turret Unlocks")]
        public static void ResetTurretUnlocksFromMenu()
        {
            if (!Confirm("Reset turret unlocks?", "This will reset unlocked turrets back to default turrets only."))
                return;

            ResetTurretUnlocks(FindFirstGameDatabase());
        }

        [MenuItem("Tools/ETD/Save/Reset/Trait Unlocks")]
        public static void ResetTraitUnlocksFromMenu()
        {
            if (!Confirm("Reset trait unlocks?", "This will reset unlocked traits back to default traits only and clear selected traits."))
                return;

            ResetTraitUnlocks(FindFirstGameDatabase());
        }

        [MenuItem("Tools/ETD/Save/Reset/Challenge Progress")]
        public static void ResetChallengeProgressFromMenu()
        {
            if (!Confirm("Reset challenge progress?", "This will clear completed challenges, claimed challenges, and saved challenge progress."))
                return;

            ResetChallengeProgress();
        }

        [MenuItem("Tools/ETD/Save/Reset/All Unlocks And Challenges")]
        public static void ResetAllUnlocksAndChallengesFromMenu()
        {
            if (!Confirm("Reset all unlocks and challenges?", "This will reset turret unlocks, trait unlocks, challenge completion, challenge claims, and challenge progress. Currency, shop upgrades, settings, and leaderboard data will be kept."))
                return;

            ResetAllUnlocksAndChallenges(FindFirstGameDatabase());
        }

        private void OnEnable()
        {
            _database = FindFirstGameDatabase();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("ETD Progression Reset", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Editor-only tool for testing unlock progression. It edits the unified save file, but keeps currency, shop upgrades, graphics/audio settings, and leaderboard data unless you reset those elsewhere.",
                MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Database", EditorStyles.boldLabel);
                _database = (GameDatabase)EditorGUILayout.ObjectField("Game Database", _database, typeof(GameDatabase), false);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Find GameDatabase"))
                        _database = FindFirstGameDatabase();

                    if (GUILayout.Button("Log Save Path"))
                        Debug.Log($"[ETD Progression Reset] Unified save path: {SavePaths.UnifiedSavePath}");
                }

                if (_database == null)
                {
                    EditorGUILayout.HelpBox(
                        $"No GameDatabase found. Turret reset will still keep '{BasicTurretFallbackId}' as a safe fallback, but default traits cannot be auto-detected.",
                        MessageType.Warning);
                }
            }

            EditorGUILayout.Space(8);
            DrawCurrentState();

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Reset Actions", EditorStyles.boldLabel);

                if (GUILayout.Button("Reset Turret Unlocks"))
                {
                    if (Confirm("Reset turret unlocks?", "This will reset unlocked turrets back to default turrets only."))
                        SetStatus(ResetTurretUnlocks(_database));
                }

                if (GUILayout.Button("Reset Trait Unlocks"))
                {
                    if (Confirm("Reset trait unlocks?", "This will reset unlocked traits back to default traits only, clear selected traits, and clear trait upgrade entries."))
                        SetStatus(ResetTraitUnlocks(_database));
                }

                if (GUILayout.Button("Reset Challenge Progress / Claims"))
                {
                    if (Confirm("Reset challenge progress?", "This will clear completed challenges, claimed challenges, and saved challenge progress."))
                        SetStatus(ResetChallengeProgress());
                }

                EditorGUILayout.Space(6);

                if (GUILayout.Button("Reset Turrets + Traits + Challenges"))
                {
                    if (Confirm("Reset all unlocks and challenges?", "This will reset turret unlocks, trait unlocks, challenge completion, challenge claims, and challenge progress. Currency, shop upgrades, settings, and leaderboard data will be kept."))
                        SetStatus(ResetAllUnlocksAndChallenges(_database));
                }
            }

            if (!string.IsNullOrWhiteSpace(_status))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(_status, MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCurrentState()
        {
            SaveData save = SaveSystem.Load();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Current Save State", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Unlocked Turrets", Count(save.UnlockedTurretIds).ToString());
                EditorGUILayout.LabelField("Unlocked Traits", Count(save.UnlockedTraitIds).ToString());
                EditorGUILayout.LabelField("Selected Traits", Count(save.SelectedTraitIds).ToString());
                EditorGUILayout.LabelField("Completed Challenges", Count(save.CompletedChallengeIds).ToString());
                EditorGUILayout.LabelField("Claimed Challenges", Count(save.ClaimedChallengeIds).ToString());
                EditorGUILayout.LabelField("Challenge Progress Entries", Count(save.ChallengeProgressTypes).ToString());
            }
        }

        private static string ResetTurretUnlocks(GameDatabase database)
        {
            SaveData save = SaveSystem.Load();
            string[] defaultIds = GetDefaultTurretIds(database);

            save.UnlockedTurretIds = defaultIds;
            save.LastRunNewTurretIds = Array.Empty<string>();
            save.ViewedNewTurretIds = Array.Empty<string>();

            SaveAndRefresh(save);

            string message = $"Turret unlocks reset. Kept {defaultIds.Length} default turret(s): {string.Join(", ", defaultIds)}";
            Debug.Log($"[ETD Progression Reset] {message}");
            return message;
        }

        private static string ResetTraitUnlocks(GameDatabase database)
        {
            SaveData save = SaveSystem.Load();
            string[] defaultIds = GetDefaultTraitIds(database);

            save.UnlockedTraitIds = defaultIds;
            save.SelectedTraitIds = Array.Empty<string>();
            save.TraitUpgradeIds = Array.Empty<string>();
            save.TraitUpgradeLevels = Array.Empty<int>();
            save.LastRunNewTraitIds = Array.Empty<string>();
            save.ViewedNewTraitIds = Array.Empty<string>();

            SaveAndRefresh(save);

            string kept = defaultIds.Length > 0 ? string.Join(", ", defaultIds) : "none";
            string message = $"Trait unlocks reset. Kept {defaultIds.Length} default trait(s): {kept}";
            Debug.Log($"[ETD Progression Reset] {message}");
            return message;
        }

        private static string ResetChallengeProgress()
        {
            SaveData save = SaveSystem.Load();

            save.CompletedChallengeIds = Array.Empty<string>();
            save.ClaimedChallengeIds = Array.Empty<string>();
            save.ChallengeProgressTypes = Array.Empty<int>();
            save.ChallengeProgressValues = Array.Empty<float>();
            save.LastRunCompletedChallengeIds = Array.Empty<string>();
            save.ViewedCompletedChallengeIds = Array.Empty<string>();

            SaveAndRefresh(save);

            const string message = "Challenge completion, claims, progress, and challenge badges reset.";
            Debug.Log($"[ETD Progression Reset] {message}");
            return message;
        }

        private static string ResetAllUnlocksAndChallenges(GameDatabase database)
        {
            SaveData save = SaveSystem.Load();
            string[] defaultTurretIds = GetDefaultTurretIds(database);
            string[] defaultTraitIds = GetDefaultTraitIds(database);

            save.UnlockedTurretIds = defaultTurretIds;
            save.UnlockedTraitIds = defaultTraitIds;
            save.SelectedTraitIds = Array.Empty<string>();
            save.TraitUpgradeIds = Array.Empty<string>();
            save.TraitUpgradeLevels = Array.Empty<int>();

            save.CompletedChallengeIds = Array.Empty<string>();
            save.ClaimedChallengeIds = Array.Empty<string>();
            save.ChallengeProgressTypes = Array.Empty<int>();
            save.ChallengeProgressValues = Array.Empty<float>();

            save.LastRunNewTurretIds = Array.Empty<string>();
            save.LastRunNewTraitIds = Array.Empty<string>();
            save.LastRunCompletedChallengeIds = Array.Empty<string>();
            save.ViewedNewTurretIds = Array.Empty<string>();
            save.ViewedNewTraitIds = Array.Empty<string>();
            save.ViewedCompletedChallengeIds = Array.Empty<string>();

            SaveAndRefresh(save);

            string message = $"All unlocks and challenges reset. Kept {defaultTurretIds.Length} default turret(s) and {defaultTraitIds.Length} default trait(s).";
            Debug.Log($"[ETD Progression Reset] {message}");
            return message;
        }

        private static string[] GetDefaultTurretIds(GameDatabase database)
        {
            var ids = new List<string>();

            if (database != null && database.Turrets != null)
            {
                foreach (TurretData turret in database.Turrets)
                {
                    if (turret == null || !turret.IsUnlockedByDefault || string.IsNullOrWhiteSpace(turret.Id))
                        continue;

                    if (!ids.Contains(turret.Id))
                        ids.Add(turret.Id);
                }
            }

            if (ids.Count == 0)
                ids.Add(BasicTurretFallbackId);

            return ids.ToArray();
        }

        private static string[] GetDefaultTraitIds(GameDatabase database)
        {
            var ids = new List<string>();

            if (database != null && database.Traits != null)
            {
                foreach (TraitData trait in database.Traits)
                {
                    if (trait == null || !trait.IsUnlockedByDefault || string.IsNullOrWhiteSpace(trait.Id))
                        continue;

                    if (!ids.Contains(trait.Id))
                        ids.Add(trait.Id);
                }
            }

            return ids.ToArray();
        }

        private static GameDatabase FindFirstGameDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDatabase");
            if (guids == null || guids.Length == 0)
                return null;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameDatabase database = AssetDatabase.LoadAssetAtPath<GameDatabase>(path);
                if (database != null)
                    return database;
            }

            return null;
        }

        private static void SaveAndRefresh(SaveData save)
        {
            SaveSystem.Save(save);

            if (SaveManager.Instance != null)
                SaveManager.Instance.LoadAll();

            AssetDatabase.Refresh();
        }

        private static bool Confirm(string title, string message)
        {
            return EditorUtility.DisplayDialog(title, message, "Reset", "Cancel");
        }

        private static int Count<T>(T[] array)
        {
            return array == null ? 0 : array.Length;
        }

        private void SetStatus(string message)
        {
            _status = message;
            Repaint();
        }
    }
}
#endif
