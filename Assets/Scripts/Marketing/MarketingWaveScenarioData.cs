// ============================================================================
// ETD.Marketing - MarketingWaveScenarioData.cs
// Put in: Assets/Scripts/Marketing/MarketingWaveScenarioData.cs
// ============================================================================

using UnityEngine;

namespace ETD.Marketing
{
    [CreateAssetMenu(menuName = "ETD/Marketing/Wave Scenario", fileName = "MarketingWaveScenario_")]
    public class MarketingWaveScenarioData : ScriptableObject
    {
        [Header("Identity")]
        public string Id = "hero_wave_25";
        public string DisplayName = "Hero Wave 25";

        [TextArea(2, 5)]
        public string Notes;

        [Header("Wave")]
        public int WaveNumber = 25;
        public bool ClearExistingEnemies = true;
        public bool PublishWaveStartedEvent = true;
        public bool ForceWaveActiveState = true;

        [Header("Camera")]
        public bool PlayCameraFromStart = true;
        public float DelayBeforeCamera = 0f;

        [Header("Capture Helper")]
        public bool HideGameplayUI = false;
        public bool HideMarketingOverlayAfterStart = true;

        [Header("Editing")]
        public float RecommendedCutTime = 4f;
    }
}
