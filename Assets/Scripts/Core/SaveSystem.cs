// ============================================================================
// ETD.Core - SaveSystem.cs  [UPDATED - persistent planning selections]
// ============================================================================
using UnityEngine;

namespace ETD.Core
{
    [System.Serializable]
    public class SaveData
    {
        public int MetaCurrency;
        public int HighScore;
        public int HighestWave;
        public int TotalRuns;
        public int TotalKills;
        // Legacy numeric score leaderboard. Kept for save compatibility.
        public int[] TopScores = new int[10];

        // Current leaderboard: score is the highest wave reached.
        // This is intentionally separate from TopScores so old large score-attack
        // values do not pollute the wave-based leaderboard after updating.
        public int HighWaveScore;
        public int[] TopWaves = new int[10];
        public int TraitSlotCount = GameConstants.MAX_TRAIT_SLOTS_DEFAULT;
        public string[] UnlockedTraitIds = System.Array.Empty<string>();
        public string[] UnlockedTurretIds = new[] { "turret_basic" };
        public int[] TurretRelicSlotCounts = System.Array.Empty<int>();
        public int[] TurretBaseUpgrades = System.Array.Empty<int>();
        public string[] CompletedChallengeIds = System.Array.Empty<string>();
        public float[] PermanentBonuses = new float[10];
        public string SelectedLanguage = "en";
        public float MasterVolume = 1f;
        public float GameSoundsVolume = 0.8f;   // replaces old SFXVolume
        public float UIVolume = 0.8f;   // new separate UI volume
        public float MusicVolume = 0.7f;
        // Shop purchase level trackers (05 each)
        public int ShopMetaBonusLevel = 0;
        public int ShopXPMultiplierLevel = 0;
        public int ShopGoldMultiplierLevel = 0;
        public int ShopMaxHPLevel = 0;
        public int ShopRerollLevel = 0;
        public int RerollTokens = 0;
        // Per-trait upgrade levels in shop (parallel arrays: Id + Level)
        // Index matches by trait ID lookup at runtime
        public string[] TraitUpgradeIds = System.Array.Empty<string>();
        public int[] TraitUpgradeLevels = System.Array.Empty<int>();

        // === VIDEO ===
        public bool VSyncEnabled = true;
        // First-time players start on the HIGHEST tier (index 0 = High) for the
        // best first impression; this default only applies when there is no save
        // file yet. Existing players keep whatever they last chose. Players on
        // weaker hardware can drop to Medium/Low in Settings.
        public int QualityLevel = 0;      // index into QualitySettings.names (0=High,1=Medium,2=Low)
        public int ResolutionIndex = -1;     // legacy fallback only; size fields below are preferred
        public int ResolutionWidth = 0;      // 0 = native/default
        public int ResolutionHeight = 0;     // 0 = native/default
        public int WindowModeIndex = 0;

        // === GAMEPLAY ===
        public float CameraPanSpeed = 20f;
        public float CameraZoomSpeed = 5f;

        // Persistent planning selections (survive app restart)
        public string[] SelectedTraitIds = System.Array.Empty<string>();

        // v1.0 active spell system: single selection (not an array like traits — only
        // one spell is ever equipped) + shop upgrade level (cooldown reduction).
        public string SelectedSpellId = "";
        public int ShopSpellUpgradeLevel = 0;

        public int[] ChallengeProgressTypes = System.Array.Empty<int>();
        public float[] ChallengeProgressValues = System.Array.Empty<float>();
        // Lifetime stats  persist across all runs
        public float LifetimeGoldSpent = 0f;

        public int SaveVersion = 0;

        // Challenge claiming (separate from completion)
        public string[] ClaimedChallengeIds = System.Array.Empty<string>();

        // Last run new items (cleared each run start, populated during run)
        public string[] LastRunNewTurretIds = System.Array.Empty<string>();
        public string[] LastRunNewTraitIds = System.Array.Empty<string>();
        public string[] LastRunCompletedChallengeIds = System.Array.Empty<string>();

        // Badge tracking: which items player has VIEWED (badge dismissed)
        public string[] ViewedNewTurretIds = System.Array.Empty<string>();
        public string[] ViewedNewTraitIds = System.Array.Empty<string>();
        public string[] ViewedCompletedChallengeIds = System.Array.Empty<string>();

        // Always skip prep phase option
        public bool AlwaysSkipPrep = false;

        // Automatically cast the selected global ability once ready during a wave.
        public bool AutoUseGlobalAbility = false;

        // Floating damage numbers option. Initializer flag lets old saves migrate to enabled by default.
        public bool FloatingDamageNumbersPreferenceInitialized = false;
        public bool FloatingDamageNumbersEnabled = true;

        // Rebindable keyboard shortcuts. Parallel arrays keep JsonUtility compatibility.
        public string[] KeybindActionNames = System.Array.Empty<string>();
        public string[] KeybindKeyNames = System.Array.Empty<string>();

        // Run snapshot for resume feature
        public bool HasSavedRun = false;
        public RunSnapshot SavedRun = null;

        public bool TutorialCompleted = false;
        public bool HubTutorialSeen = false;
    }

    [System.Serializable]
    public class RunSnapshot
    {
        public int Wave;
        public int Gold;
        public int Lives;
        public int MaxLives;
        public int Level;
        public int XP;
        public int XPToNextLevel;
        public int Score;
        public int RerollTokens;
        public string[] ActiveTraitIds;
        public float[] ActivePermanentBonuses;
        public float[] SpecBonuses;        // serialized as flat array of (int type, float value) pairs
        public int[] SpecBonusTypes;
        public int[] SpecStacks;           // parallel to SpecBonusTypes; how many of each card were taken (null in old saves)
        // Per-turret-type spec bonuses (RunData.SpecTurretBonuses). Four parallel
        // arrays because Unity's JsonUtility cannot serialize a tuple-keyed
        // dictionary. All null in saves made before v1.0 Phase 5.
        public int[] SpecTurretBonusTypes;
        public int[] SpecTurretBonusTurretTypes;
        public float[] SpecTurretBonuses;
        public int[] SpecTurretStacks;
        // The spell is locked in for the lifetime of a run. Storing it (and its live
        // cooldown) on the snapshot rather than reading SaveData.SelectedSpellId on
        // resume stops two exploits: swapping spells mid-run by leaving to the Hub,
        // and refreshing a long cooldown by leaving and continuing.
        // Empty / -1 in snapshots written before this existed.
        public string SelectedSpellId = "";
        public float SpellCooldownRemaining = -1f;

        public PlacedTurretSnapshot[] PlacedTurrets;
        // Cumulative counters restored so run-dynamic traits (e.g. "damage per
        // owned turret", "damage per gold spent") keep their value across
        // leave/continue instead of resetting to zero.
        public int TurretsPlaced;
        public int TotalGoldSpent;
        public float TotalTime;
        public bool IsLevelUpPending;
        public string[] PendingSpecCardIds;   // IDs of the 3 presented spec cards
        public SavedTileSpecialty[] TileSpecialties; // dynamic tile positions+types

      
    }
    [System.Serializable]
    public class SavedTileSpecialty
    {
        public int GridX;
        public int GridY;
        public TileSpecialty Specialty;
        public int DynamicTileType;  // cast to ETD.Data.DynamicTileType on restore
    }
    [System.Serializable]
    public class PlacedTurretSnapshot
    {
        public string TurretDataId;
        public int GridX;
        public int GridY;
        public int Level;
        public bool IsEvolved;
        public int EvolutionPath;  // 0=A, 1=B, -1=not evolved
        public bool IsEvolvedTier2; // v1.0 level-25 shared second evolution tier; false on old saves
        public int Gold;           // gold invested
        public int TargetingMode;  // cast to ETD.Data.TargetingMode; 0 = First (old saves)
    }

    public static class SaveSystem
    {
        private const string LEGACY_PLAYER_PREFS_KEY = "ETD_SaveData";

        private static SaveData _cachedData;
        private static bool _migrationAttempted;

        /// <summary>
        /// Single save authority for progression, settings, local leaderboard, and run resume.
        /// Backed by SavePaths.UnifiedSavePath so Steam Auto-Cloud can sync it.
        /// Existing gameplay code should continue using SaveSystem.Load()/Save().
        /// </summary>
        public static SaveData Load()
        {
            if (_cachedData != null)
                return _cachedData;

            SaveData data = null;
            bool loadedUnified = JsonSaveSystem.TryLoad(SavePaths.UnifiedSavePath, out data) && data != null;

            if (!loadedUnified)
                data = TryLoadLegacyPlayerPrefs();
            else
                _migrationAttempted = true;

            if (data == null)
                data = new SaveData();

            Normalize(data);

            bool migratedIds = SaveIdMigration.Migrate(data);
            bool shouldWriteUnifiedSave = migratedIds || !loadedUnified;
            if (shouldWriteUnifiedSave)
                Save(data);

            _cachedData = data;
            return _cachedData;
        }

        public static void Save(SaveData data)
        {
            if (data == null)
                data = new SaveData();

            Normalize(data);
            _cachedData = data;

            // Sandboxed tutorial run (see [[etd-v1-full-release]] tutorial redesign):
            // keep mutations in memory for the session (so the tutorial's economy/spec
            // cards/etc. behave normally) but never persist them to disk. GameManager
            // discards the in-memory copy via ReloadFromDisk() when returning to Hub.
            if (GameManager.Instance != null && GameManager.Instance.IsTutorialMode)
                return;

            JsonSaveSystem.Save(SavePaths.UnifiedSavePath, data);
        }

        public static void ReloadFromDisk()
        {
            _cachedData = null;
            _migrationAttempted = false;
            Load();
        }

        /// <summary>
        /// True until the player has completed at least one real run.
        /// Used to keep automation-style settings such as Always Skip Prep disabled
        /// during the first-time player experience.
        /// </summary>
        public static bool IsFirstTimePlayer()
        {
            return IsFirstTimePlayer(Load());
        }

        public static bool IsFirstTimePlayer(SaveData data)
        {
            if (data == null)
                return true;

            return data.TotalRuns <= 0 && data.HighestWave <= 0;
        }

        private static SaveData TryLoadLegacyPlayerPrefs()
        {
            _migrationAttempted = true;

            if (!PlayerPrefs.HasKey(LEGACY_PLAYER_PREFS_KEY))
                return null;

            try
            {
                string json = PlayerPrefs.GetString(LEGACY_PLAYER_PREFS_KEY);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                SaveData data = JsonUtility.FromJson<SaveData>(json);
                if (data != null)
                    Debug.Log("[SaveSystem] Migrated legacy PlayerPrefs save to unified JSON save.");

                return data;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[SaveSystem] Legacy PlayerPrefs migration failed. A new unified save will be created. " + ex.Message);
                return null;
            }
        }

        private static void Normalize(SaveData data)
        {
            data.TopScores ??= new int[10];
            data.TopWaves ??= new int[10];
            data.UnlockedTraitIds ??= System.Array.Empty<string>();
            data.UnlockedTurretIds ??= new[] { "turret_basic" };
            data.TurretRelicSlotCounts ??= System.Array.Empty<int>();
            data.TurretBaseUpgrades ??= System.Array.Empty<int>();
            data.CompletedChallengeIds ??= System.Array.Empty<string>();
            data.PermanentBonuses ??= new float[10];
            data.SelectedTraitIds ??= System.Array.Empty<string>();
            data.ChallengeProgressTypes ??= System.Array.Empty<int>();
            data.ChallengeProgressValues ??= System.Array.Empty<float>();
            data.ClaimedChallengeIds ??= System.Array.Empty<string>();
            data.LastRunNewTurretIds ??= System.Array.Empty<string>();
            data.LastRunNewTraitIds ??= System.Array.Empty<string>();
            data.LastRunCompletedChallengeIds ??= System.Array.Empty<string>();
            data.ViewedNewTurretIds ??= System.Array.Empty<string>();
            data.ViewedNewTraitIds ??= System.Array.Empty<string>();
            data.ViewedCompletedChallengeIds ??= System.Array.Empty<string>();
            data.TraitUpgradeIds ??= System.Array.Empty<string>();
            data.TraitUpgradeLevels ??= System.Array.Empty<int>();
            data.KeybindActionNames ??= System.Array.Empty<string>();
            data.KeybindKeyNames ??= System.Array.Empty<string>();

            if (data.KeybindKeyNames.Length < data.KeybindActionNames.Length)
            {
                string[] expanded = new string[data.KeybindActionNames.Length];
                System.Array.Copy(data.KeybindKeyNames, expanded, data.KeybindKeyNames.Length);
                data.KeybindKeyNames = expanded;
            }

            if (!data.FloatingDamageNumbersPreferenceInitialized)
            {
                data.FloatingDamageNumbersEnabled = true;
                data.FloatingDamageNumbersPreferenceInitialized = true;
            }

            // First-time players should experience the normal preparation phase at least once.
            // This also prevents old/dev saves with no recorded runs from starting with auto-skip enabled.
            if (IsFirstTimePlayer(data))
                data.AlwaysSkipPrep = false;

            if (data.TopScores.Length < 10)
            {
                int[] expanded = new int[10];
                System.Array.Copy(data.TopScores, expanded, data.TopScores.Length);
                data.TopScores = expanded;
            }

            if (data.TopWaves.Length < 10)
            {
                int[] expanded = new int[10];
                System.Array.Copy(data.TopWaves, expanded, data.TopWaves.Length);
                data.TopWaves = expanded;
            }

            // First run after this patch: keep the local player row useful without
            // importing old score-attack values from TopScores.
            if (data.HighWaveScore <= 0 && data.HighestWave > 0)
                data.HighWaveScore = data.HighestWave;

            bool hasStoredWave = false;
            for (int i = 0; i < data.TopWaves.Length; i++)
            {
                if (data.TopWaves[i] > 0)
                {
                    hasStoredWave = true;
                    break;
                }
            }

            if (!hasStoredWave && data.HighWaveScore > 0)
                data.TopWaves[0] = data.HighWaveScore;

            if (data.TraitUpgradeLevels.Length < data.TraitUpgradeIds.Length)
            {
                int[] expanded = new int[data.TraitUpgradeIds.Length];
                System.Array.Copy(data.TraitUpgradeLevels, expanded, data.TraitUpgradeLevels.Length);
                data.TraitUpgradeLevels = expanded;
            }
        }

        public static void AddMetaCurrency(int amount)
        {
            if (amount == 0)
                return;

            var data = Load();
            data.MetaCurrency = Mathf.Max(0, data.MetaCurrency + amount);
            Save(data);
            EventBus.Publish(new MetaCurrencyChangedEvent
            {
                Current = data.MetaCurrency,
                Delta = amount
            });
        }

        public static void AddLifetimeGoldSpent(float amount)
        {
            if (amount <= 0f)
                return;

            var save = Load();
            save.LifetimeGoldSpent += amount;
            Save(save);
        }

        public static void RecordRunEnded(int waveReached, int kills)
        {
            var data = Load();
            data.TotalRuns++;
            data.TotalKills += Mathf.Max(0, kills);
            data.HighestWave = Mathf.Max(data.HighestWave, waveReached);
            Save(data);
        }

        /// <summary>
        /// Updates the local leaderboard. In the current design the leaderboard score
        /// is the wave reached, not the old score-attack formula. The parameter name
        /// remains "score" for API compatibility with existing callers/services.
        /// </summary>
        public static void UpdateLeaderboard(int score)
        {
            int waveReached = Mathf.Max(0, score);
            if (waveReached <= 0)
                return;

            var data = Load();
            data.TopWaves ??= new int[10];

            var waves = new System.Collections.Generic.List<int>(data.TopWaves);
            waves.Add(waveReached);
            waves.Sort((a, b) => b.CompareTo(a));
            waves.RemoveAll(v => v <= 0);
            if (waves.Count > 10) waves.RemoveRange(10, waves.Count - 10);

            while (waves.Count < 10)
                waves.Add(0);

            data.TopWaves = waves.ToArray();
            data.HighWaveScore = Mathf.Max(data.HighWaveScore, waveReached);
            data.HighestWave = Mathf.Max(data.HighestWave, waveReached);

            // Keep old HighScore/TopScores compatible for UI/code that still reads them.
            // From this patch forward, HighScore means best wave score.
            data.HighScore = data.HighWaveScore;
            data.TopScores = (int[])data.TopWaves.Clone();

            Save(data);
        }

        public static void SavePlanningSelections(string[] traitIds)
        {
            var data = Load();
            data.SelectedTraitIds = traitIds ?? System.Array.Empty<string>();
            Save(data);
        }

        public static void SaveSelectedSpell(string spellId)
        {
            var data = Load();
            data.SelectedSpellId = spellId ?? "";
            Save(data);
        }

        public static void DeleteSave()
        {
            JsonSaveSystem.Delete(SavePaths.UnifiedSavePath);
            PlayerPrefs.DeleteKey(LEGACY_PLAYER_PREFS_KEY);
            PlayerPrefs.Save();
            _cachedData = null;
            _migrationAttempted = false;
        }

        public static int GetTraitUpgradeLevel(SaveData save, string traitId)
        {
            if (save == null || save.TraitUpgradeIds == null || save.TraitUpgradeLevels == null)
                return 0;

            for (int i = 0; i < save.TraitUpgradeIds.Length && i < save.TraitUpgradeLevels.Length; i++)
                if (save.TraitUpgradeIds[i] == traitId) return save.TraitUpgradeLevels[i];
            return 0;
        }

        public static void SetTraitUpgradeLevel(SaveData save, string traitId, int level)
        {
            if (save == null || string.IsNullOrWhiteSpace(traitId))
                return;

            save.TraitUpgradeIds ??= System.Array.Empty<string>();
            save.TraitUpgradeLevels ??= System.Array.Empty<int>();

            for (int i = 0; i < save.TraitUpgradeIds.Length; i++)
            {
                if (save.TraitUpgradeIds[i] != traitId)
                    continue;

                if (i >= save.TraitUpgradeLevels.Length)
                {
                    var expanded = new int[save.TraitUpgradeIds.Length];
                    System.Array.Copy(save.TraitUpgradeLevels, expanded, save.TraitUpgradeLevels.Length);
                    save.TraitUpgradeLevels = expanded;
                }

                save.TraitUpgradeLevels[i] = level;
                return;
            }

            var ids = new System.Collections.Generic.List<string>(save.TraitUpgradeIds);
            var levels = new System.Collections.Generic.List<int>(save.TraitUpgradeLevels);
            ids.Add(traitId);
            levels.Add(level);
            save.TraitUpgradeIds = ids.ToArray();
            save.TraitUpgradeLevels = levels.ToArray();
        }

        public static float GetChallengeProgress(SaveData save, int conditionType)
        {
            if (save == null || save.ChallengeProgressTypes == null) return 0f;
            for (int i = 0; i < save.ChallengeProgressTypes.Length; i++)
                if (save.ChallengeProgressTypes[i] == conditionType)
                    return save.ChallengeProgressValues != null && i < save.ChallengeProgressValues.Length
                        ? save.ChallengeProgressValues[i] : 0f;
            return 0f;
        }

        public static void SetChallengeProgress(SaveData save, int conditionType, float value)
        {
            if (save == null)
                return;

            var types = new System.Collections.Generic.List<int>(save.ChallengeProgressTypes ?? System.Array.Empty<int>());
            var values = new System.Collections.Generic.List<float>(save.ChallengeProgressValues ?? System.Array.Empty<float>());

            while (values.Count < types.Count)
                values.Add(0f);

            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] != conditionType) continue;
                if (value > values[i]) values[i] = value;
                save.ChallengeProgressTypes = types.ToArray();
                save.ChallengeProgressValues = values.ToArray();
                return;
            }

            types.Add(conditionType);
            values.Add(value);
            save.ChallengeProgressTypes = types.ToArray();
            save.ChallengeProgressValues = values.ToArray();
        }

        public static bool IsClaimed(SaveData save, string challengeId)
        {
            if (save == null || save.ClaimedChallengeIds == null) return false;
            return System.Array.IndexOf(save.ClaimedChallengeIds, challengeId) >= 0;
        }

        public static void MarkClaimed(SaveData save, string challengeId)
        {
            if (save == null || string.IsNullOrWhiteSpace(challengeId))
                return;

            var list = new System.Collections.Generic.List<string>(
                save.ClaimedChallengeIds ?? System.Array.Empty<string>());
            if (!list.Contains(challengeId)) list.Add(challengeId);
            save.ClaimedChallengeIds = list.ToArray();
        }
    }
}
