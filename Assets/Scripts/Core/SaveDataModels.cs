using System;
using System.Collections.Generic;

namespace ETD.Core
{
    /// <summary>
    /// Example save models for Endless Defense.
    /// You can rename fields or connect these to your current progression/challenge/settings systems.
    /// </summary>
    [Serializable]
    public class MetaProgressSaveData
    {
        public int crystals;
        public int highestWave;
        public int totalRuns;
        public int totalKills;
        public int traitSlots;
        public int rerollTokens;

        // Example permanent shop upgrades.
        public int coinMagnetLevel;
        public int wisdomCrystalLevel;
        public int fortuneVaultLevel;
        public int fortifiedCoreLevel;
    }

    [Serializable]
    public class UnlocksSaveData
    {
        public List<string> unlockedTurretIds = new();
        public List<string> unlockedTraitIds = new();
        public List<string> unlockedSpecCardIds = new();
    }

    [Serializable]
    public class ChallengesSaveData
    {
        public List<string> completedChallengeIds = new();
        public List<ChallengeProgressEntry> progress = new();
    }

    [Serializable]
    public class ChallengeProgressEntry
    {
        public string challengeId;
        public int currentValue;
    }

    [Serializable]
    public class ProfileSaveData
    {
        public string saveVersion = "1.0.0";
        public bool tutorialCompleted;
        public string selectedLanguage = "en";
        public bool alwaysSkipEnabled;
    }

    /// <summary>
    /// Good to sync through Steam Cloud because it affects player preference/progression feel.
    /// Move volume to local-only if you prefer per-device audio settings.
    /// </summary>
    [Serializable]
    public class GameplaySettingsSaveData
    {
        public string language = "en";
        public bool alwaysSkipEnabled;
        public float masterVolume = 1f;
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
    }

    /// <summary>
    /// Do NOT sync this with Steam Cloud. Resolution/quality/render scale are device-specific.
    /// </summary>
    [Serializable]
    public class GraphicsSettingsSaveData
    {
        public int width = 1920;
        public int height = 1080;
        public bool fullscreen = true;
        public int qualityLevel = 1;
        public float renderScale = 1f;
    }
}
