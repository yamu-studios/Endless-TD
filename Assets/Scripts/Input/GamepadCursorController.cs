// ============================================================================
// ETD.Inputs - GamepadCursorController.cs
// Drives a VirtualMouseInput (stock Input System component) from the gamepad
// right stick + South/East buttons, and toggles it on/off based on whichever
// device produced input most recently, so the real mouse keeps working
// normally whenever the player isn't actively using a gamepad.
// ============================================================================
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ETD.Inputs
{
    [RequireComponent(typeof(VirtualMouseInput))]
    public class GamepadCursorController : MonoBehaviour
    {
        [SerializeField] private RectTransform _cursorTransform;
        [SerializeField] private Graphic _cursorGraphic;

        [Tooltip("Cursor travel in pixels per second at 1080p, at full stick deflection. " +
                 "Scaled by actual screen height so it feels identical at 1440p/4K. " +
                 "~2600 crosses a 1080p screen in about 0.75s.")]
        [SerializeField] private float _cursorSpeed = 2600f;

        [Tooltip("Screen height the speed above is authored against.")]
        [SerializeField] private float _cursorSpeedReferenceHeight = 1080f;

        [Tooltip("Hide the hardware mouse pointer while the gamepad is driving. Without this " +
                 "both cursors are visible at once and the OS pointer visibly lags behind, " +
                 "then jumps when control returns to the mouse.")]
        [SerializeField] private bool _hideHardwareCursorOnGamepad = true;

        [SerializeField] private float _stickDeadzone = 0.15f;
        [SerializeField] private float _mouseMoveThresholdSqr = 9f; // ~3px, avoids flickering back on real-mouse sensor jitter

        [Tooltip("This game drives UI with the virtual cursor, not with selection-based " +
                 "navigation. Unity's DefaultInputActions binds Submit to */{Submit} (South) " +
                 "and Cancel to */{Cancel} (East), which makes those buttons activate whichever " +
                 "widget happens to be SELECTED instead of whatever the cursor is over — e.g. " +
                 "firing a turret panel's Sell button from across the screen. Leave enabled.")]
        [SerializeField] private bool _disableUINavigation = true;

        private VirtualMouseInput _virtualMouseInput;
        private bool _gamepadActive;
        private InputAction _stick;
        private InputAction _leftButton;
        private InputAction _rightButton;

        private void Awake()
        {
            _virtualMouseInput = GetComponent<VirtualMouseInput>();

            // Start disabled so its virtual Mouse device is never created until a
            // gamepad is actually used — otherwise it would immediately become
            // Mouse.current and mask the real mouse from GetRealMouse() below.
            _virtualMouseInput.enabled = false;

            _virtualMouseInput.cursorMode = VirtualMouseInput.CursorMode.SoftwareCursor;
            _virtualMouseInput.cursorSpeed = GetScaledCursorSpeed();
            if (_cursorTransform != null) _virtualMouseInput.cursorTransform = _cursorTransform;
            if (_cursorGraphic != null) _virtualMouseInput.cursorGraphic = _cursorGraphic;

            _stick = new InputAction("GamepadCursorStick", InputActionType.Value, "<Gamepad>/rightStick");
            _leftButton = new InputAction("GamepadCursorClick", InputActionType.Button, "<Gamepad>/buttonSouth");
            _rightButton = new InputAction("GamepadCursorCancel", InputActionType.Button, "<Gamepad>/buttonEast");

            _virtualMouseInput.stickAction = new InputActionProperty(_stick);
            _virtualMouseInput.leftButtonAction = new InputActionProperty(_leftButton);
            _virtualMouseInput.rightButtonAction = new InputActionProperty(_rightButton);

            DisableNavigationEvents();
            SetGamepadActive(false);
        }

        /// <summary>
        /// Turns off selection-based UI navigation. Without this, South/East are claimed
        /// by the Submit/Cancel actions in DefaultInputActions and fire at the SELECTED
        /// widget rather than at the cursor, so the face buttons appear not to click and
        /// can trigger whatever button was last selected.
        /// </summary>
        private void DisableNavigationEvents()
        {
            if (!_disableUINavigation) return;

            // EventSystem.current is assigned in EventSystem.OnEnable, which may not have
            // run yet during our Awake — fall back to a scene lookup, and Start() calls
            // this again so ordering can't leave navigation switched on.
            var eventSystem = EventSystem.current;
            if (eventSystem == null) eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem == null) return;

            eventSystem.sendNavigationEvents = false;
            // Nothing should stay selected, or a stale selection can still receive events.
            eventSystem.SetSelectedGameObject(null);
        }

        private void Start()
        {
            DisableNavigationEvents();
        }

        private void Update()
        {
            var gamepad = Gamepad.current;
            if (gamepad != null && IsGamepadActivelyUsed(gamepad))
            {
                if (!_gamepadActive) SetGamepadActive(true);
                return;
            }

            var realMouse = GetRealMouse();
            if (realMouse != null && IsRealMouseActivelyUsed(realMouse) && _gamepadActive)
            {
                SetGamepadActive(false);
            }
        }

        private bool IsGamepadActivelyUsed(Gamepad gamepad)
        {
            float deadzoneSqr = _stickDeadzone * _stickDeadzone;
            if (gamepad.rightStick.ReadValue().sqrMagnitude > deadzoneSqr) return true;
            if (gamepad.leftStick.ReadValue().sqrMagnitude > deadzoneSqr) return true;

            return gamepad.buttonSouth.wasPressedThisFrame
                || gamepad.buttonEast.wasPressedThisFrame
                || gamepad.buttonNorth.wasPressedThisFrame
                || gamepad.buttonWest.wasPressedThisFrame
                || gamepad.startButton.wasPressedThisFrame
                || gamepad.leftShoulder.wasPressedThisFrame
                || gamepad.rightShoulder.wasPressedThisFrame;
        }

        private bool IsRealMouseActivelyUsed(Mouse mouse)
        {
            return mouse.delta.ReadValue().sqrMagnitude > _mouseMoveThresholdSqr
                || mouse.leftButton.wasPressedThisFrame
                || mouse.rightButton.wasPressedThisFrame;
        }

        /// <summary>
        /// Mouse.current can be the virtual device created by VirtualMouseInput
        /// while it's active, so find the real hardware mouse explicitly rather
        /// than trusting .current (which would make gamepad cursor movement look
        /// like "real mouse activity" and immediately switch itself back off).
        /// </summary>
        private Mouse GetRealMouse()
        {
            Mouse virtualMouse = _virtualMouseInput.virtualMouse;
            foreach (var device in InputSystem.devices)
            {
                if (device is Mouse mouse && mouse != virtualMouse)
                    return mouse;
            }
            return null;
        }

        private void SetGamepadActive(bool active)
        {
            // Hand the pointer position over before the outgoing device goes away,
            // otherwise the incoming cursor appears wherever it was last left instead
            // of where the player is actually looking — and clicks land there too.
            if (!active) HandOffToRealMouse();

            _gamepadActive = active;
            _virtualMouseInput.enabled = active;
            if (_cursorGraphic != null) _cursorGraphic.enabled = active;

            // Only one pointer should ever be visible, otherwise the OS cursor sits
            // where it was last physically moved and reads as a second, wrong cursor.
            SetHardwareCursorVisible(!active);

            // Enabling the component is what creates the virtual device, so the
            // gamepad-side handoff has to happen after that.
            if (active)
            {
                // Re-read in case resolution changed since Awake.
                _virtualMouseInput.cursorSpeed = GetScaledCursorSpeed();
                HandOffToVirtualMouse();
            }

            // Code-created InputActions are not enabled by default. Do it explicitly
            // rather than relying on VirtualMouseInput to adopt them, so the click
            // buttons can never end up dead while the stick still moves the cursor.
            if (active)
            {
                _stick?.Enable();
                _leftButton?.Enable();
                _rightButton?.Enable();
                // A selection made before the gamepad took over would otherwise linger.
                if (_disableUINavigation && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            }
            else
            {
                _stick?.Disable();
                _leftButton?.Disable();
                _rightButton?.Disable();
            }
        }

        /// <summary>
        /// Gamepad is taking over: move the virtual cursor to wherever the hardware
        /// pointer currently is, so it appears under the player's eye rather than at
        /// the position it held the last time the gamepad was used.
        /// </summary>
        private void HandOffToVirtualMouse()
        {
            var virtualMouse = _virtualMouseInput.virtualMouse;
            var realMouse = GetRealMouse();
            if (virtualMouse == null || realMouse == null) return;

            Vector2 position = realMouse.position.ReadValue();
            if (!IsOnScreen(position)) return;

            // The software cursor RectTransform follows the device position on the
            // component's next update, so only the device needs changing here.
            InputState.Change(virtualMouse.position, position);
            InputState.Change(virtualMouse.delta, Vector2.zero);
        }

        /// <summary>
        /// Real mouse is taking over: warp the OS pointer to where the virtual cursor
        /// was, so the player continues from the same spot instead of the pointer
        /// jumping back to wherever the hardware mouse was physically left.
        /// </summary>
        private void HandOffToRealMouse()
        {
            var virtualMouse = _virtualMouseInput.virtualMouse;
            var realMouse = GetRealMouse();
            if (virtualMouse == null || realMouse == null) return;

            Vector2 position = virtualMouse.position.ReadValue();
            if (!IsOnScreen(position)) return;

            realMouse.WarpCursorPosition(position);
            // WarpCursorPosition only moves the OS pointer; the device's own state is
            // updated separately so this frame's reads already agree with it.
            InputState.Change(realMouse.position, position);
        }

        private static bool IsOnScreen(Vector2 position)
        {
            return position.x >= 0f && position.y >= 0f
                && position.x <= Screen.width && position.y <= Screen.height;
        }

        /// <summary>
        /// Cursor speed is authored at a reference height and scaled to the real screen,
        /// so a given stick deflection covers the same fraction of the screen at any
        /// resolution instead of feeling sluggish on larger displays.
        /// </summary>
        private float GetScaledCursorSpeed()
        {
            if (_cursorSpeedReferenceHeight <= 0f) return _cursorSpeed;
            return _cursorSpeed * (Screen.height / _cursorSpeedReferenceHeight);
        }

        private void SetHardwareCursorVisible(bool visible)
        {
            if (!_hideHardwareCursorOnGamepad) return;
            Cursor.visible = visible;
        }

        private void OnDisable()
        {
            // Never leave the player without a pointer if this object is torn down
            // or the scene changes while the gamepad was driving.
            if (_hideHardwareCursorOnGamepad) Cursor.visible = true;
        }

        /// <summary>
        /// Places the cursor graphic at the virtual mouse's screen position.
        ///
        /// Done here rather than left to VirtualMouseInput's own software cursor because
        /// that writes anchoredPosition, which only lines up when the cursor is anchored
        /// bottom-left (screen-space origin). CursorImage is anchored top-left, so the
        /// drawn cursor ended up vertically offset from where clicks actually landed.
        /// Converting through the parent rect also handles Hub's ScreenSpaceCamera canvas
        /// and Game's ScreenSpaceOverlay canvas without special-casing either.
        /// </summary>
        private void LateUpdate()
        {
            if (!_gamepadActive || _cursorTransform == null) return;

            var virtualMouse = _virtualMouseInput.virtualMouse;
            if (virtualMouse == null) return;

            var parentRect = _cursorTransform.parent as RectTransform;
            if (parentRect == null) return;

            var rootCanvas = _cursorTransform.GetComponentInParent<Canvas>();
            if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;

            // Overlay canvases take a null camera; anything else needs the canvas camera.
            Camera camera = null;
            if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                camera = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;

            Vector2 screenPosition = virtualMouse.position.ReadValue();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, screenPosition, camera, out Vector2 localPoint))
            {
                _cursorTransform.localPosition = localPoint;
            }
        }

        private void OnDestroy()
        {
            _stick?.Dispose();
            _leftButton?.Dispose();
            _rightButton?.Dispose();
        }
    }
}
