// ============================================================================
// ETD.Input - KeyboardShortcuts.cs  [NEW]
// Handles all in-game keyboard shortcuts.
// Add to [GameManagers] GO in Game scene.
//
// Shortcuts:
//   1 / 2 / 3      — select spec card (during level-up) OR evolve path A/B
//   R              — re-roll spec cards (during level-up)
//   Q              — upgrade selected turret
//   E              — sell selected turret
// ============================================================================
using UnityEngine;
using ETD.Core;
using ETD.Turrets;
using ETD.UI;

namespace ETD.Input
{
    public class KeyboardShortcuts : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpecCardSelectionUI _specCardUI;
        [SerializeField] private TurretInfoPanel     _turretInfoPanel;

        private void Update()
        {
            var state = GameManager.Instance?.CurrentState ?? GameState.Preparation;

            // ---------------------------------------------------------------
            // Spec card selection — gated on the panel actually being open, not on
            // game state. v1.0: leveling up no longer pauses the run (see
            // [[etd-v1-full-release]] Phase 4), so this panel can be showing at the
            // same time as normal Preparation/WaveActive play — no early return, the
            // shortcuts below still need to work underneath it.
            // ---------------------------------------------------------------
            if (_specCardUI != null && _specCardUI.IsShowing)
            {
                if (KeybindingManager.GetKeyDown(KeybindAction.SelectSpecCard1)) _specCardUI.SelectCard(0);
                if (KeybindingManager.GetKeyDown(KeybindAction.SelectSpecCard2)) _specCardUI.SelectCard(1);
                if (KeybindingManager.GetKeyDown(KeybindAction.SelectSpecCard3)) _specCardUI.SelectCard(2);
                if (KeybindingManager.GetKeyDown(KeybindAction.RerollSpecCards)) _specCardUI.TryReroll();
            }

            // ---------------------------------------------------------------
            // EVOLVE CHOICE state — path selection
            // ---------------------------------------------------------------
            if (state == GameState.EvolveChoice)
            {
                if (KeybindingManager.GetKeyDown(KeybindAction.EvolvePathA))
                {
                    _turretInfoPanel?.EvolvePathByKey(0);
                    _turretInfoPanel?.ConfirmTier2ByKey();
                }
                if (KeybindingManager.GetKeyDown(KeybindAction.EvolvePathB)) _turretInfoPanel?.EvolvePathByKey(1);
                return;
            }

            // ---------------------------------------------------------------
            // PREPARATION or WAVE — turret shortcuts
            // ---------------------------------------------------------------
            if (state == GameState.Preparation || state == GameState.WaveActive)
            {
                if (KeybindingManager.GetKeyDown(KeybindAction.UpgradeSelectedTurret)) _turretInfoPanel?.UpgradeShortcut();
                if (KeybindingManager.GetKeyDown(KeybindAction.SellSelectedTurret)) _turretInfoPanel?.SellShortcut();
            }
        }
    }
}
