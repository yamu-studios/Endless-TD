using UnityEngine;

namespace ETD.Core
{
    /// <summary>
    /// Facade over SaveSystem. SaveSystem is the single authoritative save path.
    /// This MonoBehaviour exists for bootstrap/lifecycle hooks and old scene references.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        public MetaProgressSaveData MetaProgress { get; private set; } = new MetaProgressSaveData();
        public UnlocksSaveData Unlocks { get; private set; } = new UnlocksSaveData();
        public ChallengesSaveData Challenges { get; private set; } = new ChallengesSaveData();
        public ProfileSaveData Profile { get; private set; } = new ProfileSaveData();
        public GameplaySettingsSaveData GameplaySettings { get; private set; } = new GameplaySettingsSaveData();
        public GraphicsSettingsSaveData GraphicsSettings { get; private set; } = new GraphicsSettingsSaveData();

        [Header("Debug")]
        [SerializeField] private bool _logSavePathsOnAwake = true;
        [SerializeField] private bool _saveOnApplicationPause = true;
        [SerializeField] private bool _saveOnApplicationQuit = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadAll();

            if (_logSavePathsOnAwake)
                LogSavePaths();
        }

        public void LoadAll()
        {
            SyncViewModelsFromUnifiedSave(SaveSystem.Load());

            // Device-specific settings remain local-only.
            if (!JsonSaveSystem.TryLoad(SavePaths.GraphicsSettingsPath, out GraphicsSettingsSaveData graphicsSettings))
                graphicsSettings = new GraphicsSettingsSaveData();

            GraphicsSettings = graphicsSettings;
        }

        public void SaveAll()
        {
            SaveCloudData();
            SaveLocalOnlyData();
        }

        public void SaveCloudData()
        {
            // SaveSystem writes the authoritative cloud save. The split models are only compatibility views.
            SaveSystem.Save(SaveSystem.Load());
            SyncViewModelsFromUnifiedSave(SaveSystem.Load());
        }

        public void SaveLocalOnlyData()
        {
            JsonSaveSystem.Save(SavePaths.GraphicsSettingsPath, GraphicsSettings);
        }

        private void SyncViewModelsFromUnifiedSave(SaveData save)
        {
            MetaProgress = new MetaProgressSaveData
            {
                crystals = save.MetaCurrency,
                highestWave = save.HighestWave,
                totalRuns = save.TotalRuns,
                totalKills = save.TotalKills,
                traitSlots = save.TraitSlotCount,
                rerollTokens = save.RerollTokens,
                coinMagnetLevel = save.ShopMetaBonusLevel,
                wisdomCrystalLevel = save.ShopXPMultiplierLevel,
                fortuneVaultLevel = save.ShopGoldMultiplierLevel,
                fortifiedCoreLevel = save.ShopMaxHPLevel
            };

            Unlocks = new UnlocksSaveData();
            Unlocks.unlockedTraitIds.AddRange(save.UnlockedTraitIds ?? System.Array.Empty<string>());
            Unlocks.unlockedTurretIds.AddRange(save.UnlockedTurretIds ?? System.Array.Empty<string>());

            Challenges = new ChallengesSaveData();
            Challenges.completedChallengeIds.AddRange(save.CompletedChallengeIds ?? System.Array.Empty<string>());

            Profile = new ProfileSaveData
            {
                saveVersion = save.SaveVersion.ToString(),
                tutorialCompleted = save.TutorialCompleted,
                selectedLanguage = save.SelectedLanguage,
                alwaysSkipEnabled = save.AlwaysSkipPrep
            };

            GameplaySettings = new GameplaySettingsSaveData
            {
                language = save.SelectedLanguage,
                alwaysSkipEnabled = save.AlwaysSkipPrep,
                masterVolume = save.MasterVolume,
                musicVolume = save.MusicVolume,
                sfxVolume = save.GameSoundsVolume
            };
        }

        public void MarkTutorialCompleted()
        {
            var save = SaveSystem.Load();
            save.TutorialCompleted = true;
            SaveSystem.Save(save);
            SyncViewModelsFromUnifiedSave(save);
        }

        public void SetLanguage(string languageCode)
        {
            var save = SaveSystem.Load();
            save.SelectedLanguage = string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode;
            SaveSystem.Save(save);
            SyncViewModelsFromUnifiedSave(save);
        }

        public void SetAlwaysSkip(bool enabled)
        {
            var save = SaveSystem.Load();
            if (SaveSystem.IsFirstTimePlayer(save))
                enabled = false;

            save.AlwaysSkipPrep = enabled;
            SaveSystem.Save(save);
            SyncViewModelsFromUnifiedSave(save);
        }

        public void AddCrystals(int amount)
        {
            SaveSystem.AddMetaCurrency(amount);
            SyncViewModelsFromUnifiedSave(SaveSystem.Load());
        }

        public void RecordRunEnded(int waveReached, int kills)
        {
            SaveSystem.RecordRunEnded(waveReached, kills);
            SyncViewModelsFromUnifiedSave(SaveSystem.Load());
        }

        public void CompleteChallenge(string challengeId)
        {
            if (string.IsNullOrWhiteSpace(challengeId))
                return;

            var save = SaveSystem.Load();
            var completed = new System.Collections.Generic.List<string>(save.CompletedChallengeIds ?? System.Array.Empty<string>());
            if (!completed.Contains(challengeId))
                completed.Add(challengeId);

            save.CompletedChallengeIds = completed.ToArray();
            SaveSystem.Save(save);
            SyncViewModelsFromUnifiedSave(save);
        }

        public void UnlockTrait(string traitId)
        {
            if (string.IsNullOrWhiteSpace(traitId))
                return;

            var save = SaveSystem.Load();
            var ids = new System.Collections.Generic.List<string>(save.UnlockedTraitIds ?? System.Array.Empty<string>());
            if (!ids.Contains(traitId)) ids.Add(traitId);
            save.UnlockedTraitIds = ids.ToArray();
            SaveSystem.Save(save);
            SyncViewModelsFromUnifiedSave(save);
        }

        public void UnlockTurret(string turretId)
        {
            if (string.IsNullOrWhiteSpace(turretId))
                return;

            var save = SaveSystem.Load();
            var ids = new System.Collections.Generic.List<string>(save.UnlockedTurretIds ?? System.Array.Empty<string>());
            if (!ids.Contains(turretId)) ids.Add(turretId);
            save.UnlockedTurretIds = ids.ToArray();
            SaveSystem.Save(save);
            SyncViewModelsFromUnifiedSave(save);
        }

        public void LogSavePaths()
        {
            Debug.Log($"[SaveManager] Application.persistentDataPath: {Application.persistentDataPath}");
            Debug.Log($"[SaveManager] Unified cloud save: {SavePaths.UnifiedSavePath}");
            Debug.Log($"[SaveManager] Steam Auto-Cloud folder: {SavePaths.CloudSaveDirectory}");
            Debug.Log($"[SaveManager] Local-only folder: {SavePaths.LocalOnlyDirectory}");
            Debug.Log("[SaveManager] Steamworks Auto-Cloud should sync only the SteamCloudSave folder.");
        }

        private void OnApplicationPause(bool pause)
        {
            if (_saveOnApplicationPause && pause)
                SaveAll();
        }

        private void OnApplicationQuit()
        {
            if (_saveOnApplicationQuit)
                SaveAll();
        }
    }
}
