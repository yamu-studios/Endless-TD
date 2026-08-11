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
using UnityEngine.InputSystem;
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
                bool upgrade = KeybindingManager.GetKeyDown(KeybindAction.UpgradeSelectedTurret);
                bool sell    = KeybindingManager.GetKeyDown(KeybindAction.SellSelectedTurret);
                bool cast    = KeybindingManager.GetKeyDown(KeybindAction.CastSpell);

                // Fixed gamepad shortcuts, mirroring the keyboard bindings above.
                // These are deliberately NOT rebindable: KeybindingManager stores
                // KeyCodes for the keyboard, and the pad is read through the new Input
                // System, so the two cannot share one binding table.
                var pad = Gamepad.current;
                if (pad != null && !KeybindingManager.IsListeningForRebind)
                {
                    // L1 doubles as "keep placing" while a turret is being placed
                    // (GameInputHandler), so quick-sell must not fire in that mode or a
                    // single press would both place and sell.
                    if (!IsPlacingTurret())
                        sell |= pad.leftShoulder.wasPressedThisFrame;

                    upgrade |= pad.rightShoulder.wasPressedThisFrame;
                    cast    |= pad.buttonNorth.wasPressedThisFrame;   // Triangle / Y
                }

                if (upgrade) _turretInfoPanel?.UpgradeShortcut();
                if (sell)    _turretInfoPanel?.SellShortcut();
                if (cast)    TryCastSpell();
            }
        }

        // Cached once: GameInputHandler is not in the ServiceLocator, and a
        // FindObject call every frame would be wasteful.
        private ETD.Inputs.GameInputHandler _inputHandler;
        private bool _inputHandlerResolved;

        private bool IsPlacingTurret()
        {
            if (!_inputHandlerResolved)
            {
                _inputHandler = FindFirstObjectByType<ETD.Inputs.GameInputHandler>();
                _inputHandlerResolved = true;
            }
            return _inputHandler != null && _inputHandler.IsPlacingTurret;
        }

        private static void TryCastSpell()
        {
            if (ServiceLocator.TryGet<ETD.Gameplay.SpellManager>(out var spells))
                spells.TryCast();
        }
    }
}
