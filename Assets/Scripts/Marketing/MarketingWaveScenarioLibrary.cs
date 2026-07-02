// ============================================================================
// ETD.Marketing - MarketingWaveScenarioLibrary.cs
// Put in: Assets/Scripts/Marketing/MarketingWaveScenarioLibrary.cs
// ============================================================================

using UnityEngine;

namespace ETD.Marketing
{
    [CreateAssetMenu(menuName = "ETD/Marketing/Wave Scenario Library", fileName = "MarketingWaveScenarioLibrary")]
    public class MarketingWaveScenarioLibrary : ScriptableObject
    {
        public MarketingWaveScenarioData[] Scenarios;

        public int Count => Scenarios == null ? 0 : Scenarios.Length;

        public MarketingWaveScenarioData GetScenario(int index)
        {
            if (Scenarios == null || Scenarios.Length == 0)
                return null;

            index = Mathf.Clamp(index, 0, Scenarios.Length - 1);
            return Scenarios[index];
        }
    }
}
