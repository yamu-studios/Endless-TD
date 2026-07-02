// ============================================================================
// ETD.UI - KeybindingSettingsUI.cs
// Settings-panel helper for player-adjustable keybinds.
// v12: robust owner-based rebind capture and stale-lock cleanup.
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ETD.Core;

namespace ETD.UI
{
    [DefaultExecutionOrder(-10000)]
    public sealed class KeybindingSettingsUI : MonoBehaviour
    {
        [System.Serializable]
        public class BindingRow
        {
            public KeybindAction Action;
            public TMP_Text Label;
            [Tooltip("Usually the TMP text inside the rebind button. This text shows the current key, Listening, Saved, or duplicate feedback.")]
            public TMP_Text KeyText;
            public Button RebindButton;
        }

        private static readonly List<KeybindingSettingsUI> ActiveInstances = new();
        private static readonly KeyCode[] AllKeyCodes = (KeyCode[])System.Enum.GetValues(typeof(KeyCode));

        [SerializeField] private BindingRow[] _rows;
        [SerializeField] private Button _resetButton;
        [SerializeField] private float _inlineMessageSeconds = 0.8f;
        [SerializeField] private bool _allowMouseButtons = false;
        [SerializeField] private bool _allowJoystickKeys = false;

        private int _listeningIndex = -1;
        private int _listeningStartFrame = -1;
        private int _lastHandledFrame = -1;
        private Coroutine _restoreRoutine;
        private int _ownerId;

        /// <summary>
        /// Emergency cleanup used by PauseMenuUI/Settings toggles. This is the most
        /// important part of the v12 fix: closing settings by button must also clear
        /// the static input lock even if this component is not disabled by Unity.
        /// </summary>
        public static void ForceCancelAllActiveRebinds()
        {
            for (int i = ActiveInstances.Count - 1; i >= 0; i--)
            {
                var ui = ActiveInstances[i];
                if (ui == null)
                {
                    ActiveInstances.RemoveAt(i);
                    continue;
                }

                ui.CancelRebind(silent: true, forceGlobal: true);
            }

            KeybindingManager.ForceClearListeningState();
        }

        private void Awake()
        {
            _ownerId = GetInstanceID();
            WireButtons();
            Refresh();
        }

        private void OnEnable()
        {
            _ownerId = GetInstanceID();
            RegisterInstance();

            // Clear any stale lock left by a hidden/previous settings panel. This is
            // safe because opening a settings keybind UI should never preserve an old
            // half-listening state.
            KeybindingManager.ForceClearListeningState();
            _listeningIndex = -1;
            _listeningStartFrame = -1;
            _lastHandledFrame = -1;

            WireButtons();
            Refresh();
            EventBus.Subscribe<SettingToggleEvent>(OnSettingToggleEvent);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SettingToggleEvent>(OnSettingToggleEvent);
            CancelRebind(silent: true, forceGlobal: true);
            ActiveInstances.Remove(this);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<SettingToggleEvent>(OnSettingToggleEvent);
            CancelRebind(silent: true, forceGlobal: true);
            ActiveInstances.Remove(this);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                CancelRebind(silent: true, forceGlobal: true);
        }

        private void OnSettingToggleEvent(SettingToggleEvent evt)
        {
            if (!evt.IsActive)
                CancelRebind(silent: true, forceGlobal: true);
        }

        private void RegisterInstance()
        {
            if (!ActiveInstances.Contains(this))
                ActiveInstances.Add(this);
        }

        private void OnGUI()
        {
            if (!IsActivelyListening())
                return;

            Event evt = Event.current;
            if (evt == null || evt.type != EventType.KeyDown)
                return;

            if (Time.frameCount <= _listeningStartFrame || Time.frameCount == _lastHandledFrame)
                return;

            KeyCode key = evt.keyCode;
            if (key == KeyCode.None || !IsAllowedKey(key))
                return;

            evt.Use();
            HandlePressedKey(key);
        }

        private void Update()
        {
            TryCaptureKeyFromInput();
        }

        private void LateUpdate()
        {
            // Extra fallback. Some UI focus paths miss Update's first readable frame,
            // but LateUpdate still sees Input.GetKeyDown in the same frame.
            TryCaptureKeyFromInput();
        }

        private bool IsActivelyListening()
        {
            return _listeningIndex >= 0 && KeybindingManager.ListeningOwnerId == _ownerId;
        }

        private void TryCaptureKeyFromInput()
        {
            if (!IsActivelyListening())
                return;

            if (Time.frameCount <= _listeningStartFrame || Time.frameCount == _lastHandledFrame)
                return;

            KeyCode key = ReadPressedKey();
            if (key == KeyCode.None)
                return;

            HandlePressedKey(key);
        }

        private void HandlePressedKey(KeyCode key)
        {
            if (!IsActivelyListening())
                return;

            if (!IsAllowedKey(key))
                return;

            _lastHandledFrame = Time.frameCount;

            BindingRow row = _rows[_listeningIndex];
            if (KeybindingManager.TrySet(row.Action, key, out string error))
            {
                int changedIndex = _listeningIndex;
                _listeningIndex = -1;
                _listeningStartFrame = -1;
                KeybindingManager.EndListeningForRebind(_ownerId);
                KeybindingManager.SuppressGameplayShortcutsForNextFrame();

                SetRowTemporaryMessage(changedIndex, LocalizationManager.Get("keybind_saved_short", "Saved"));
                RefreshExcept(changedIndex);
            }
            else
            {
                // Keep listening so the player can press another valid key immediately.
                SetRowTemporaryMessage(_listeningIndex, string.IsNullOrWhiteSpace(error)
                    ? LocalizationManager.Get("keybind_duplicate_short", "Already Used")
                    : LocalizationManager.Get("keybind_duplicate_short", "Already Used"));
                KeybindingManager.SuppressGameplayShortcutsForNextFrame();
            }
        }

        private void WireButtons()
        {
            if (_resetButton != null)
            {
                _resetButton.onClick.RemoveListener(OnResetClicked);
                _resetButton.onClick.AddListener(OnResetClicked);
            }

            if (_rows == null)
                return;

            for (int i = 0; i < _rows.Length; i++)
            {
                int captured = i;
                BindingRow row = _rows[i];
                if (row?.RebindButton == null)
                    continue;

                row.RebindButton.onClick.RemoveAllListeners();
                row.RebindButton.onClick.AddListener(() => BeginRebind(captured));
            }
        }

        private void BeginRebind(int index)
        {
            if (_rows == null || index < 0 || index >= _rows.Length || _rows[index] == null)
                return;

            if (_restoreRoutine != null)
            {
                StopCoroutine(_restoreRoutine);
                _restoreRoutine = null;
            }

            // Same button cancels listening. Esc is bindable, so it is not used as cancel.
            if (_listeningIndex == index && KeybindingManager.ListeningOwnerId == _ownerId)
            {
                CancelRebind(silent: false, forceGlobal: true);
                return;
            }

            // Stop other hidden/old instances first. This prevents a stale row from
            // owning the global lock and making the visible row unable to capture keys.
            ForceCancelAllActiveRebinds();
            RegisterInstance();

            _listeningIndex = index;
            _listeningStartFrame = Time.frameCount;
            _lastHandledFrame = -1;
            KeybindingManager.BeginListeningForRebind(_ownerId);

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            SetRowText(index, LocalizationManager.Get("keybind_listening", "Listening..."));
        }

        private void CancelRebind(bool silent, bool forceGlobal = false)
        {
            int index = _listeningIndex;
            _listeningIndex = -1;
            _listeningStartFrame = -1;
            _lastHandledFrame = -1;

            if (_restoreRoutine != null)
            {
                StopCoroutine(_restoreRoutine);
                _restoreRoutine = null;
            }

            if (forceGlobal)
                KeybindingManager.ForceClearListeningState();
            else
                KeybindingManager.EndListeningForRebind(_ownerId);

            KeybindingManager.SuppressGameplayShortcutsForNextFrame();

            if (index >= 0)
            {
                if (silent)
                    RefreshRow(index);
                else
                    SetRowTemporaryMessage(index, LocalizationManager.Get("keybind_cancelled_short", "Cancelled"));
            }
        }

        private void OnResetClicked()
        {
            CancelRebind(silent: true, forceGlobal: true);
            KeybindingManager.ResetToDefaults();
            Refresh();
        }

        private void Refresh()
        {
            if (_rows == null)
                return;

            for (int i = 0; i < _rows.Length; i++)
                RefreshRow(i);
        }

        private void RefreshExcept(int skipIndex)
        {
            if (_rows == null)
                return;

            for (int i = 0; i < _rows.Length; i++)
            {
                if (i == skipIndex)
                    continue;
                RefreshRow(i);
            }
        }

        private void RefreshRow(int index)
        {
            if (_rows == null || index < 0 || index >= _rows.Length)
                return;

            BindingRow row = _rows[index];
            if (row == null)
                return;

            if (row.Label != null)
                row.Label.text = KeybindingManager.GetLocalizedActionName(row.Action);

            if (row.KeyText != null)
                row.KeyText.text = KeybindingManager.FormatKey(KeybindingManager.Get(row.Action));
        }

        private void SetRowText(int index, string text)
        {
            if (_rows == null || index < 0 || index >= _rows.Length)
                return;

            BindingRow row = _rows[index];
            if (row?.KeyText != null)
                row.KeyText.text = text ?? string.Empty;
        }

        private void SetRowTemporaryMessage(int index, string message)
        {
            SetRowText(index, message);

            if (_restoreRoutine != null)
                StopCoroutine(_restoreRoutine);
            _restoreRoutine = StartCoroutine(RestoreRowAfterDelay(index));
        }

        private IEnumerator RestoreRowAfterDelay(int index)
        {
            float end = Time.unscaledTime + Mathf.Max(0.05f, _inlineMessageSeconds);
            while (Time.unscaledTime < end)
                yield return null;

            if (_listeningIndex == index && KeybindingManager.ListeningOwnerId == _ownerId)
                SetRowText(index, LocalizationManager.Get("keybind_listening", "Listening..."));
            else
                RefreshRow(index);

            _restoreRoutine = null;
        }

        private KeyCode ReadPressedKey()
        {
            for (int i = 0; i < AllKeyCodes.Length; i++)
            {
                KeyCode key = AllKeyCodes[i];
                if (!IsAllowedKey(key))
                    continue;

                if (UnityEngine.Input.GetKeyDown(key))
                    return key;
            }

            return KeyCode.None;
        }

        private bool IsAllowedKey(KeyCode key)
        {
            if (key == KeyCode.None)
                return false;

            if (!_allowMouseButtons && key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)
                return false;

            string keyName = key.ToString();
            if (!_allowJoystickKeys && keyName.StartsWith("Joystick", System.StringComparison.Ordinal))
                return false;

            if (!_allowMouseButtons && keyName.StartsWith("Mouse", System.StringComparison.Ordinal))
                return false;

            return true;
        }
    }
}
