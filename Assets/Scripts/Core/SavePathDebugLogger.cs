using UnityEngine;

namespace ETD.Core
{
    /// <summary>
    /// Optional temporary helper.
    /// Add it to a GameObject, run the Steam build, then copy the logged path into Steamworks Auto-Cloud config.
    /// </summary>
    public class SavePathDebugLogger : MonoBehaviour
    {
        [SerializeField] private bool _createTestCloudFile;

        private void Start()
        {
            Debug.Log($"[SavePathDebug] persistentDataPath: {Application.persistentDataPath}");
            Debug.Log($"[SavePathDebug] cloud save directory: {SavePaths.CloudSaveDirectory}");
            Debug.Log($"[SavePathDebug] local-only directory: {SavePaths.LocalOnlyDirectory}");

            if (_createTestCloudFile)
            {
                JsonSaveSystem.Save(SavePaths.ProfilePath, new ProfileSaveData
                {
                    saveVersion = "1.0.0",
                    tutorialCompleted = false,
                    selectedLanguage = "en",
                    alwaysSkipEnabled = false
                });

                Debug.Log($"[SavePathDebug] Created test file: {SavePaths.ProfilePath}");
            }
        }
    }
}
