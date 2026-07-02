using System.IO;
using UnityEngine;

namespace ETD.Core
{
    /// <summary>
    /// Central place for save file paths.
    /// Steam Auto-Cloud should sync only CloudSaveDirectory.
    ///
    /// Windows example:
    /// C:/Users/<User>/AppData/LocalLow/<CompanyName>/<ProductName>/SteamCloudSave
    ///
    /// Steamworks Auto-Cloud example:
    /// Root: WinAppDataLocalLow
    /// Subdirectory: <CompanyName>/<ProductName>/SteamCloudSave
    /// Pattern: *
    /// Recursive: Yes
    /// OS: Windows
    /// </summary>
    public static class SavePaths
    {
        private const string CloudSaveFolderName = "SteamCloudSave";
        private const string LocalOnlyFolderName = "LocalOnlySettings";

        private static string VariantSuffix => DemoMode.IsDemo ? DemoMode.SaveFolderSuffix : string.Empty;
        private static string CloudSaveFolder => CloudSaveFolderName + VariantSuffix;
        private static string LocalOnlyFolder => LocalOnlyFolderName + VariantSuffix;

        public static string CloudSaveDirectory
        {
            get
            {
                string path = Path.Combine(Application.persistentDataPath, CloudSaveFolder);
                EnsureDirectory(path);
                return path;
            }
        }

        public static string LocalOnlyDirectory
        {
            get
            {
                string path = Path.Combine(Application.persistentDataPath, LocalOnlyFolder);
                EnsureDirectory(path);
                return path;
            }
        }

        public static string MetaProgressPath =>
            Path.Combine(CloudSaveDirectory, "meta_progress.json");

        public static string UnlocksPath =>
            Path.Combine(CloudSaveDirectory, "unlocks.json");

        public static string ChallengesPath =>
            Path.Combine(CloudSaveDirectory, "challenges.json");

        public static string ProfilePath =>
            Path.Combine(CloudSaveDirectory, "profile.json");

        public static string GameplaySettingsPath =>
            Path.Combine(CloudSaveDirectory, "gameplay_settings.json");

        // Main save authority used by SaveSystem. This is the only progression/run-resume file.
        public static string UnifiedSavePath =>
            Path.Combine(CloudSaveDirectory, "save_data.json");

        // Keep device-specific settings outside Steam Cloud.
        public static string GraphicsSettingsPath =>
            Path.Combine(LocalOnlyDirectory, "graphics_settings.json");

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
