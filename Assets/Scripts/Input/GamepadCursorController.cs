// ============================================================================
// ETD.Inputs - GamepadCursorController.cs
// Drives a VirtualMouseInput (stock Input System component) from the gamepad
// right stick + South/East buttons, and toggles it on/off based on whichever
// device produced input most recently, so the real mouse keeps working
// normally whenever the player isn't actively using a gamepad.
// ============================================================================
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ETD.Inputs
{
    [RequireComponent(typeof(VirtualMouseInput))]
    public class GamepadCursorController : MonoBehaviour
    {
        [SerializeField] private RectTransform _cursorTransform;
        [SerializeField] private Graphic _cursorGraphic;
        [SerializeField] private float _cursorSpeed = 1200f;
        [SerializeField] private float _stickDeadzone = 0.15f;
        [SerializeField] private float _mouseMoveThresholdSqr = 9f; // ~3px, avoids flickering back on real-mouse sensor jitter

        private VirtualMouseInput _virtualMouseInput;
        private bool _gamepadActive;

        private void Awake()
        {
            _virtualMouseInput = GetComponent<VirtualMouseInput>();

            // Start disabled so its virtual Mouse device is never created until a
            // gamepad is actually used — otherwise it would immediately become
            // Mouse.current and mask the real mouse from GetRealMouse() below.
            _virtualMouseInput.enabled = false;

            _virtualMouseInput.cursorMode = VirtualMouseInput.CursorMode.SoftwareCursor;
            _virtualMouseInput.cursorSpeed = _cursorSpeed;
            if (_cursorTransform != null) _virtualMouseInput.cursorTransform = _cursorTransform;
            if (_cursorGraphic != null) _virtualMouseInput.cursorGraphic = _cursorGraphic;

            _virtualMouseInput.stickAction = new InputActionProperty(
                new InputAction("GamepadCursorStick", InputActionType.Value, "<Gamepad>/rightStick"));
            _virtualMouseInput.leftButtonAction = new InputActionProperty(
                new InputAction("GamepadCursorClick", InputActionType.Button, "<Gamepad>/buttonSouth"));
            _virtualMouseInput.rightButtonAction = new InputActionProperty(
                new InputAction("GamepadCursorCancel", InputActionType.Button, "<Gamepad>/buttonEast"));

            SetGamepadActive(false);
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
            _gamepadActive = active;
            _virtualMouseInput.enabled = active;
            if (_cursorGraphic != null) _cursorGraphic.enabled = active;
        }
    }
}
