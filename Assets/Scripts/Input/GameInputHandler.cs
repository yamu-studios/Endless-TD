// ============================================================================
// ETD.Input - GameInputHandler.cs
// Handles mouse clicks for turret placement/selection, Tab for relics,
// Escape for pause
// ============================================================================
using UnityEngine;
using UnityEngine.InputSystem;
using ETD.Core;
using ETD.Data;
using ETD.Grid;
using ETD.Turrets;
using ETD.Gameplay;
using ETD.Input;
using UnityEngine.EventSystems;

namespace ETD.Inputs
{
    public class GameInputHandler : MonoBehaviour
    {
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private LayerMask _turretLayer;
        [SerializeField] private TurretPlacementPreview _placementPreview;

        private GridSystem _grid;
        private TurretManager _turretManager;
        private bool _isPlacingTurret;
        private TurretData _turretToPlace;
        private int _selectedTurretId = -1;
        private bool isSettingsEnabled;
        private void Start()
        {
            _grid = ServiceLocator.Get<GridSystem>();
            _turretManager = ServiceLocator.Get<TurretManager>();
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            EventBus.Subscribe<SettingToggleEvent>(OnSettingsToggled);
        }

        private void Update()
        {
            var state = GameManager.Instance.CurrentState;
            if (state == GameState.GameOver || state == GameState.Hub) return;

            // A keybinding row is listening for the next key, or it consumed a key
            // this frame. Do not let Esc/settings/gameplay shortcuts act on the same press.
            if (KeybindingManager.IsListeningForRebind || KeybindingManager.ShouldSuppressGameplayShortcuts)
                return;

            // Pause — keyboard Escape (via KeybindingManager) or a gamepad Start
            // press. Gamepad pause is a fixed binding, not part of the rebindable
            // keyboard shortcut system (see [[etd-v1-full-release]] Phase 5).
            bool gamepadPausePressed = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
            if (KeybindingManager.GetKeyDown(KeybindAction.PauseOrCancel) || gamepadPausePressed)
            {
                if (GameManager.Instance.CurrentState == GameState.EvolveChoice)
                {
                    GameManager.Instance.PopModalState();
                    // Also hide the evolve panel via event or direct reference
                    _selectedTurretId = -1;
                    EventBus.Publish(new TurretDeselectedEvent { });
                    return;
                }
                if (_isPlacingTurret)
                    CancelPlacement();
                else
                {
                    if(!isSettingsEnabled)
                        GameManager.Instance.TogglePause();
                    else
                        EventBus.Publish(new SettingToggleEvent { IsActive = false });
                }
                   
                return;
            }
          

            if (state == GameState.Paused || state == GameState.LevelUp
                || state == GameState.EvolveChoice)
                return;


            // ADD this inside Update() when _isPlacingTurret == true:
            if (_isPlacingTurret && _turretToPlace != null)
            {
                var mousPos = GetMouseWorldPosition();
                if (_grid == null || !mousPos.HasValue) return;
                Vector2Int gridPos = _grid.WorldToNearestCell(mousPos.Value);
                Vector3 worldPos = _grid.GridToWorld(gridPos);
                worldPos.y = .1f;
                TurretRangeIndicator.ShowPlacementRange(worldPos, _turretToPlace.Range);
            }

            HandleMouseInput();
        }

        private void HandleMouseInput()
        {
            // Don't process 3D clicks when clicking on UI
            if (IsPointerOverUI()) return;
            if (Mouse.current == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (_isPlacingTurret)
                    TryPlaceTurret();
                else
                    TrySelectTurret();
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (_isPlacingTurret)
                    CancelPlacement();
                else
                    DeselectTurret();
            }
        }
        public void StartPlacement(TurretData data)
        {
            // If evolve panel is open, close it gracefully
            if (GameManager.Instance.CurrentState == GameState.EvolveChoice)
                GameManager.Instance.PopModalState();

            _isPlacingTurret = true;
            _turretToPlace = data;
            DeselectTurret();
            _placementPreview?.Show(data);
        }

        //
        // In DeselectTurret()  called when clicking empty space:
        //
        private void DeselectTurret()
        {
            if (_selectedTurretId >= 0)
            {
                // If evolve was pending for this turret, dismiss it
                if (GameManager.Instance.CurrentState == GameState.EvolveChoice)
                    GameManager.Instance.PopModalState();

                _selectedTurretId = -1;
                EventBus.Publish(new TurretDeselectedEvent());
            }
        }
        //private void DeselectTurret()
        //{
        //    if (_selectedTurretId >= 0)
        //    {
        //        _selectedTurretId = -1;
        //        EventBus.Publish(new TurretDeselectedEvent());
        //    }
        //}
        //public void StartPlacement(TurretData data)
        //{
        //    _isPlacingTurret = true;
        //    _turretToPlace = data;
        //    DeselectTurret();

        //    // Show blueprint
        //    if (_placementPreview != null)
        //        _placementPreview.Show(data);
        //}
        private void TryPlaceTurret()
        {
            if (_turretToPlace == null) return;

            var worldPos = GetMouseWorldPosition();
            if (!worldPos.HasValue) return;

            var gridPos = _grid.WorldToGrid(worldPos.Value);

            if (!_turretManager.CanPlaceTurret(gridPos)) return;

            var runMgr = ServiceLocator.Get<RunManager>();
            if (runMgr == null || !runMgr.SpendGold(_turretToPlace.Cost)) return;

            _turretManager.PlaceTurret(_turretToPlace, gridPos);

            // Keep placing if holding shift (keyboard) or the gamepad's left
            // shoulder button (fixed binding, mirrors the shift-to-keep-placing
            // convenience for gamepad players).
            bool keepPlacing = (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
                || (Gamepad.current != null && Gamepad.current.leftShoulder.isPressed);
            if (!keepPlacing)
                CancelPlacement();
        }

        private void TrySelectTurret()
        {
            if (_mainCamera == null) return;
            var ray = _mainCamera.ScreenPointToRay(GetPointerScreenPosition());

            if (Physics.Raycast(ray, out var hit, 1000f, _turretLayer))
            {
                var turret = hit.collider.GetComponent<TurretController>();
                if (turret != null)
                {
                    _selectedTurretId = turret.InstanceId;
                    EventBus.Publish(new TurretSelectedEvent { TurretId = turret.InstanceId });
                    return;
                }
            }

            // Do not close the turret panel when the player clicks an enemy or empty world space.
            // This keeps turret and enemy info panels independent; right-click still deselects turrets.
        }



        public void CancelPlacement()
        {
            _isPlacingTurret = false;
            _turretToPlace = null;
            TurretRangeIndicator.HidePlacementRange();
            // Hide blueprint
            if (_placementPreview != null)
                _placementPreview.Hide();
        }
        private Vector3? GetMouseWorldPosition()
        {
            if (_mainCamera == null) return null;
            var ray = _mainCamera.ScreenPointToRay(GetPointerScreenPosition());
            if (Physics.Raycast(ray, out var hit, 200f, _groundLayer))
                return hit.point;
            return null;
        }

        /// <summary>
        /// Mouse.current is the real hardware mouse, or the gamepad-driven
        /// virtual cursor while GamepadCursorController has it active — either
        /// way this is the correct pointer position to raycast from.
        /// </summary>
        private Vector2 GetPointerScreenPosition()
        {
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        }

        private bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void OnSettingsToggled(SettingToggleEvent evt)
        {
            isSettingsEnabled = evt.IsActive;
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<SettingToggleEvent>(OnSettingsToggled);
        }
    }
}
