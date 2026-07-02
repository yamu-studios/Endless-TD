// ============================================================================
// ETD.Core - KeybindingManager.cs
// Runtime-rebindable shortcuts with duplicate-key protection.
// v12: owner-based listening lock so stale settings UI can never block input.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ETD.Core
{
    public enum KeybindAction
    {
        SelectSpecCard1,
        SelectSpecCard2,
        SelectSpecCard3,
        RerollSpecCards,
        EvolvePathA,
        EvolvePathB,
        UpgradeSelectedTurret,
        SellSelectedTurret,
        ToggleSpecCardStats,
        PauseOrCancel
    }

    public static class KeybindingManager
    {
        private static readonly Dictionary<KeybindAction, KeyCode> _bindings = new();
        private static bool _loaded;

        // Owner-based rebind lock. This prevents an old/hidden settings panel from
        // leaving the whole keyboard blocked forever.
        private static int _listeningOwnerId;
        private static float _listeningStartedRealtime;
        private const float ListeningSafetyTimeoutSeconds = 60f;

        private static int _suppressGameplayShortcutsUntilFrame = -1;

        public static bool IsListeningForRebind
        {
            get
            {
                // Last-resort safety. If something goes badly wrong, keyboard input
                // is released automatically instead of staying dead until restart.
                if (_listeningOwnerId != 0 &&
                    Time.realtimeSinceStartup - _listeningStartedRealtime > ListeningSafetyTimeoutSeconds)
                {
                    ForceClearListeningState();
                }

                return _listeningOwnerId != 0;
            }
        }

        public static int ListeningOwnerId => _listeningOwnerId;

        /// <summary>
        /// True only for the frame where a key was consumed by the rebind UI.
        /// Input handlers can use this to avoid acting on the same key press after
        /// a successful rebind, especially Escape.
        /// </summary>
        public static bool ShouldSuppressGameplayShortcuts => Time.frameCount <= _suppressGameplayShortcutsUntilFrame;

        public static event Action<KeybindAction, KeyCode> BindingChanged;

        public static IReadOnlyDictionary<KeybindAction, KeyCode> Bindings
        {
            get { EnsureLoaded(); return _bindings; }
        }

        public static KeyCode Get(KeybindAction action)
        {
            EnsureLoaded();
            return _bindings.TryGetValue(action, out KeyCode key) ? key : GetDefault(action);
        }

        public static bool GetKeyDown(KeybindAction action)
        {
            if (IsListeningForRebind || ShouldSuppressGameplayShortcuts)
                return false;

            KeyCode key = Get(action);
            return key != KeyCode.None && UnityEngine.Input.GetKeyDown(key);
        }

        /// <summary>
        /// Starts a key-capture session owned by a specific UI instance.
        /// Any previous stale owner is replaced.
        /// </summary>
        public static void BeginListeningForRebind(int ownerId)
        {
            _listeningOwnerId = ownerId;
            _listeningStartedRealtime = Time.realtimeSinceStartup;
            SuppressGameplayShortcutsForCurrentFrame();
        }

        /// <summary>
        /// Ends a key-capture session. Passing ownerId=0 force-clears any owner.
        /// </summary>
        public static void EndListeningForRebind(int ownerId = 0)
        {
            if (ownerId == 0 || _listeningOwnerId == ownerId)
                _listeningOwnerId = 0;

            SuppressGameplayShortcutsForCurrentFrame();
        }

        /// <summary>
        /// Compatibility wrapper for older callers.
        /// </summary>
        public static void SetListeningForRebind(bool listening)
        {
            if (listening)
                BeginListeningForRebind(-1);
            else
                EndListeningForRebind(0);
        }

        public static void ForceClearListeningState()
        {
            _listeningOwnerId = 0;
            SuppressGameplayShortcutsForCurrentFrame();
        }

        public static void SuppressGameplayShortcutsForCurrentFrame()
        {
            _suppressGameplayShortcutsUntilFrame = Mathf.Max(_suppressGameplayShortcutsUntilFrame, Time.frameCount);
        }

        public static void SuppressGameplayShortcutsForNextFrame()
        {
            _suppressGameplayShortcutsUntilFrame = Mathf.Max(_suppressGameplayShortcutsUntilFrame, Time.frameCount + 1);
        }

        public static bool TrySet(KeybindAction action, KeyCode key, out string error)
        {
            EnsureLoaded();
            error = null;

            if (key == KeyCode.None)
            {
                _bindings[action] = KeyCode.None;
                Save();
                BindingChanged?.Invoke(action, key);
                return true;
            }

            foreach (var kvp in _bindings)
            {
                if (kvp.Key.Equals(action))
                    continue;

                if (kvp.Value == key)
                {
                    error = LocalizationManager.GetFormat(
                        "keybind_duplicate_format",
                        "{0} is already assigned to {1}.",
                        FormatKey(key),
                        GetLocalizedActionName(kvp.Key));
                    return false;
                }
            }

            _bindings[action] = key;
            Save();
            BindingChanged?.Invoke(action, key);
            return true;
        }

        public static void ResetToDefaults()
        {
            _bindings.Clear();
            foreach (KeybindAction action in Enum.GetValues(typeof(KeybindAction)))
                _bindings[action] = GetDefault(action);
            _loaded = true;
            Save();
        }

        public static string GetLocalizedActionName(KeybindAction action)
        {
            string key = "keybind_action_" + action.ToString().ToLowerInvariant();
            return LocalizationManager.Get(key, GetFallbackActionName(action));
        }

        public static string FormatKey(KeyCode key)
        {
            if (key == KeyCode.None)
                return LocalizationManager.Get("keybind_unassigned", "Unassigned");

            return key switch
            {
                KeyCode.Alpha0 => "0",
                KeyCode.Alpha1 => "1",
                KeyCode.Alpha2 => "2",
                KeyCode.Alpha3 => "3",
                KeyCode.Alpha4 => "4",
                KeyCode.Alpha5 => "5",
                KeyCode.Alpha6 => "6",
                KeyCode.Alpha7 => "7",
                KeyCode.Alpha8 => "8",
                KeyCode.Alpha9 => "9",
                KeyCode.Keypad0 => "Num 0",
                KeyCode.Keypad1 => "Num 1",
                KeyCode.Keypad2 => "Num 2",
                KeyCode.Keypad3 => "Num 3",
                KeyCode.Keypad4 => "Num 4",
                KeyCode.Keypad5 => "Num 5",
                KeyCode.Keypad6 => "Num 6",
                KeyCode.Keypad7 => "Num 7",
                KeyCode.Keypad8 => "Num 8",
                KeyCode.Keypad9 => "Num 9",
                KeyCode.Escape => "Esc",
                KeyCode.LeftShift => "Left Shift",
                KeyCode.RightShift => "Right Shift",
                KeyCode.LeftControl => "Left Ctrl",
                KeyCode.RightControl => "Right Ctrl",
                KeyCode.LeftAlt => "Left Alt",
                KeyCode.RightAlt => "Right Alt",
                _ => key.ToString()
            };
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _bindings.Clear();
            foreach (KeybindAction action in Enum.GetValues(typeof(KeybindAction)))
                _bindings[action] = GetDefault(action);

            SaveData save = SaveSystem.Load();
            string[] actionNames = save.KeybindActionNames ?? Array.Empty<string>();
            string[] keyNames = save.KeybindKeyNames ?? Array.Empty<string>();
            int count = Mathf.Min(actionNames.Length, keyNames.Length);

            for (int i = 0; i < count; i++)
            {
                if (!Enum.TryParse(actionNames[i], out KeybindAction action))
                    continue;
                if (!Enum.TryParse(keyNames[i], out KeyCode key))
                    continue;

                _bindings[action] = key;
            }

            _loaded = true;
        }

        private static void Save()
        {
            SaveData save = SaveSystem.Load();
            var actionNames = new List<string>(_bindings.Count);
            var keyNames = new List<string>(_bindings.Count);

            foreach (KeybindAction action in Enum.GetValues(typeof(KeybindAction)))
            {
                actionNames.Add(action.ToString());
                keyNames.Add(Get(action).ToString());
            }

            save.KeybindActionNames = actionNames.ToArray();
            save.KeybindKeyNames = keyNames.ToArray();
            SaveSystem.Save(save);
        }

        private static KeyCode GetDefault(KeybindAction action)
        {
            return action switch
            {
                KeybindAction.SelectSpecCard1 => KeyCode.Alpha1,
                KeybindAction.SelectSpecCard2 => KeyCode.Alpha2,
                KeybindAction.SelectSpecCard3 => KeyCode.Alpha3,
                KeybindAction.RerollSpecCards => KeyCode.R,
                KeybindAction.EvolvePathA => KeyCode.Z,
                KeybindAction.EvolvePathB => KeyCode.X,
                KeybindAction.UpgradeSelectedTurret => KeyCode.Q,
                KeybindAction.SellSelectedTurret => KeyCode.E,
                KeybindAction.ToggleSpecCardStats => KeyCode.Tab,
                KeybindAction.PauseOrCancel => KeyCode.Escape,
                _ => KeyCode.None
            };
        }

        private static string GetFallbackActionName(KeybindAction action)
        {
            return action switch
            {
                KeybindAction.SelectSpecCard1 => "Select Spec Card 1",
                KeybindAction.SelectSpecCard2 => "Select Spec Card 2",
                KeybindAction.SelectSpecCard3 => "Select Spec Card 3",
                KeybindAction.RerollSpecCards => "Reroll Spec Cards",
                KeybindAction.EvolvePathA => "Evolve Path A",
                KeybindAction.EvolvePathB => "Evolve Path B",
                KeybindAction.UpgradeSelectedTurret => "Upgrade Selected Turret",
                KeybindAction.SellSelectedTurret => "Sell Selected Turret",
                KeybindAction.ToggleSpecCardStats => "Toggle Spec Stats",
                KeybindAction.PauseOrCancel => "Pause / Cancel",
                _ => action.ToString()
            };
        }
    }
}
