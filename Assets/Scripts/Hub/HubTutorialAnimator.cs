// ============================================================================
// ETD.Hub - HubTutorialAnimator.cs  [v1.0 tutorial redesign]
// Hub "Tutorial" button handler. Launches the sandboxed tutorial run directly
// (GameManager.LoadTutorial()) — no longer guides the player through Planning
// and Spells selection first, since that just got in the way of a quick replay.
// Keeps the method name StartGuidedFlow to match the existing Button.onClick
// wiring in Hub.unity.
// ============================================================================
using UnityEngine;
using ETD.Core;

namespace ETD.Hub
{
    public class HubTutorialAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HubBadge _newBadge;

        private void Start()
        {
            RefreshBadge();
        }

        private void RefreshBadge()
        {
            var save = SaveSystem.Load();
            _newBadge?.SetVisible(!save.HubTutorialSeen);
        }

        /// <summary>Called by the Hub "Tutorial" button's onClick.</summary>
        public void StartGuidedFlow()
        {
            var save = SaveSystem.Load();
            save.HubTutorialSeen = true;
            SaveSystem.Save(save);
            RefreshBadge();

            GameManager.Instance?.LoadTutorial();
        }
    }
}
