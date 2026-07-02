#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class DemoModeEditorMenu
{
    private const string Key = "ETD_DEMO_EDITOR_TEST";

    [MenuItem("ETD/Demo/Enable Demo Mode In Editor")]
    private static void EnableDemoModeInEditor()
    {
        PlayerPrefs.SetInt(Key, 1);
        PlayerPrefs.Save();
        Debug.Log("[ETD Demo] Demo Mode enabled in Editor. Enter Play Mode again to test the wave 25 cap.");
    }

    [MenuItem("ETD/Demo/Disable Demo Mode In Editor")]
    private static void DisableDemoModeInEditor()
    {
        PlayerPrefs.SetInt(Key, 0);
        PlayerPrefs.Save();
        Debug.Log("[ETD Demo] Demo Mode disabled in Editor. Enter Play Mode again for full-game testing.");
    }

    [MenuItem("ETD/Demo/Print Demo Mode Status")]
    private static void PrintStatus()
    {
        bool enabled = PlayerPrefs.GetInt(Key, 0) == 1;
        Debug.Log($"[ETD Demo] Editor Demo Mode is {(enabled ? "ENABLED" : "DISABLED")}.");
    }
}
#endif
