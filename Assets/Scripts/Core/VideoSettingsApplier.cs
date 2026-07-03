// ============================================================================
// ETD.Core - VideoSettingsApplier.cs
// Applies saved video settings (quality level, vsync, window mode, resolution).
// Called at boot so a fresh launch matches the player's saved settings instead
// of running at the platform-default quality tier until the settings window is
// opened for the first time.
// ============================================================================
using UnityEngine;

namespace ETD.Core
{
    public static class VideoSettingsApplier
    {
        public static void Apply(SaveData save)
        {
            if (save == null)
                return;

            QualitySettings.SetQualityLevel(save.QualityLevel, true);
            QualitySettings.vSyncCount = save.VSyncEnabled ? 1 : 0;

            FullScreenMode mode = IndexToFullScreenMode(save.WindowModeIndex);
            Screen.fullScreenMode = mode;

            if (save.ResolutionWidth > 0 && save.ResolutionHeight > 0)
                Screen.SetResolution(save.ResolutionWidth, save.ResolutionHeight, mode);
        }

        public static FullScreenMode IndexToFullScreenMode(int index) => index switch
        {
            1 => FullScreenMode.Windowed,
            2 => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.ExclusiveFullScreen
        };
    }
}
