// ============================================================================
// ETD.UI - SettingsWindowUI.cs  [FULL REWRITE - 3 tabs]
// Tab 1: Video  — VSync, Quality, Resolution, Window Mode
// Tab 2: Sound  — Master, Game Sounds, UI, Music
// Tab 3: Gameplay — Camera Pan Speed, Camera Zoom Speed
// Works in both Hub scene and in-game pause menu.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using System.Linq;

namespace ETD.UI
{
    public class SettingsWindowUI : MonoBehaviour
    {
        // =================================================================
        // TAB BUTTONS
        // =================================================================
        [Header("Tab Buttons")]
        [SerializeField] private Button    _videoTabButton;
        [SerializeField] private Button    _soundTabButton;
        [SerializeField] private Button    _gameplayTabButton;
        [SerializeField] private Color     _activeTabColor   = Color.white;
        [SerializeField] private Color     _inactiveTabColor = new Color(0.55f, 0.55f, 0.55f);

        [Header("Tab Content")]
        [SerializeField] private GameObject _videoTabContent;
        [SerializeField] private GameObject _soundTabContent;
        [SerializeField] private GameObject _gameplayTabContent;

        // =================================================================
        // VIDEO TAB
        // =================================================================
        [Header("Video")]
        [SerializeField] private Toggle         _vsyncToggle;
        [SerializeField] private TMP_Dropdown   _qualityDropdown;
        [SerializeField] private TMP_Dropdown   _resolutionDropdown;
        [SerializeField] private TMP_Dropdown   _windowModeDropdown;

        // =================================================================
        // SOUND TAB
        // =================================================================
        [Header("Sound")]
        [SerializeField] private Slider   _masterSlider;
        [SerializeField] private Slider   _gameSoundsSlider;
        [SerializeField] private Slider   _uiSlider;
        [SerializeField] private Slider   _musicSlider;
        [SerializeField] private TMP_Text _masterValueText;
        [SerializeField] private TMP_Text _gameSoundsValueText;
        [SerializeField] private TMP_Text _uiValueText;
        [SerializeField] private TMP_Text _musicValueText;

        // =================================================================
        // GAMEPLAY TAB
        // =================================================================
        [Header("Gameplay")]
        [SerializeField] private Slider   _panSpeedSlider;
        [SerializeField] private Slider   _zoomSpeedSlider;
        [SerializeField] private TMP_Text _panSpeedValueText;
        [SerializeField] private TMP_Text _zoomSpeedValueText;

        [Header("Pan Speed Range")]
        [SerializeField] private float _panSpeedMin = 15f;
        [SerializeField] private float _panSpeedMax = 100f;

        [Header("Zoom Speed Range")]
        [SerializeField] private float _zoomSpeedMin = 1f;
        [SerializeField] private float _zoomSpeedMax = 15f;

        // =================================================================
        // CLOSE
        // =================================================================
        [Header("Close")]
        [SerializeField] private Button _closeButton;

        [SerializeField] private Toggle _alwaysSkipPrepToggle;
        [SerializeField] private TMP_Text _alwaysSkipPrepLabel;
        [SerializeField] private Toggle _floatingDamageNumbersToggle;
        [SerializeField] private TMP_Text _floatingDamageNumbersLabel;

        // Runtime
        private Resolution[] _availableResolutions;
        private bool _initializing;
        private bool _soundSlidersBound;

        private float _lastMasterVolume = -1f;
        private float _lastGameSoundsVolume = -1f;
        private float _lastUIVolume = -1f;
        private float _lastMusicVolume = -1f;


        HashSet<(int width, int height)> commonResolutions = new()
{
    (1280, 720),   // HD
    (1366, 768),   // Laptop standard
    (1600, 900),
    (1920, 1080),  // Full HD
    (2560, 1440),  // 1440p
    (3840, 2160),  // 4K

    // Optional ultrawide support
    (2560, 1080),
    (3440, 1440)
};


        private void OnEnable()
        {
            _initializing = true;
            SetupVideoTab();
            SetupSoundTab();
            SetupGameplayTab();
            _initializing = false;

            _videoTabButton?.onClick.RemoveAllListeners();
            _soundTabButton?.onClick.RemoveAllListeners();
            _gameplayTabButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.RemoveAllListeners();

            _videoTabButton?.onClick.AddListener(() => SwitchTab(0));
            _soundTabButton?.onClick.AddListener(() => SwitchTab(1));
            _gameplayTabButton?.onClick.AddListener(() => SwitchTab(2));
            _closeButton?.onClick.AddListener(() => gameObject.SetActive(false));

            SwitchTab(0);
        }

        private void Awake()
        {
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            if (gameObject.activeSelf) OnEnable(); // regenerate list with new language
            RefreshGameplayStaticTexts();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);

            if (_soundSlidersBound)
            {
                _masterSlider?.onValueChanged.RemoveListener(OnMasterVolumeChanged);
                _gameSoundsSlider?.onValueChanged.RemoveListener(OnGameSoundsVolumeChanged);
                _uiSlider?.onValueChanged.RemoveListener(OnUIVolumeChanged);
                _musicSlider?.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            }
        }


        private void RefreshGameplayStaticTexts()
        {
            if (_alwaysSkipPrepLabel == null && _alwaysSkipPrepToggle != null)
                _alwaysSkipPrepLabel = _alwaysSkipPrepToggle.GetComponentInChildren<TMP_Text>(true);

            if (_alwaysSkipPrepLabel != null)
                _alwaysSkipPrepLabel.text = LocalizationManager.Get("settings_always_skip_prep", "Always Skip Prep");

            if (_floatingDamageNumbersLabel == null && _floatingDamageNumbersToggle != null)
                _floatingDamageNumbersLabel = _floatingDamageNumbersToggle.GetComponentInChildren<TMP_Text>(true);

            if (_floatingDamageNumbersLabel != null)
                _floatingDamageNumbersLabel.text = LocalizationManager.Get("settings_floating_damage_numbers", "Floating Damage Numbers");
        }

        // =================================================================
        // TAB SWITCHING
        // =================================================================

        private void SwitchTab(int tab)
        {
            if (_videoTabContent    != null) _videoTabContent.SetActive(tab == 0);
            if (_soundTabContent    != null) _soundTabContent.SetActive(tab == 1);
            if (_gameplayTabContent != null) _gameplayTabContent.SetActive(tab == 2);

            SetTabColor(_videoTabButton,    tab == 0);
            SetTabColor(_soundTabButton,    tab == 1);
            SetTabColor(_gameplayTabButton, tab == 2);
        }

        private void SetTabColor(Button btn, bool active)
        {
            if (btn == null) return;
            var c = btn.colors;
            c.normalColor = active ? _activeTabColor : _inactiveTabColor;
            btn.colors = c;
        }

        // =================================================================
        // VIDEO TAB SETUP
        // =================================================================

        private void SetupVideoTab()
        {
            var save = SaveSystem.Load();

            // VSync
            if (_vsyncToggle != null)
            {
                _vsyncToggle.isOn = save.VSyncEnabled;
                _vsyncToggle.onValueChanged.RemoveAllListeners();
                _vsyncToggle.onValueChanged.AddListener(OnVSyncChanged);
            }

            // Quality
            if (_qualityDropdown != null)
            {
                _qualityDropdown.ClearOptions();
                var names = new List<string>(QualitySettings.names);
                var localizedNames = new List<string>();
                foreach (var name in names)
                {
                    localizedNames.Add(LocalizationManager.Get("quality_" + name.ToLower(), name));
                }
                _qualityDropdown.AddOptions(localizedNames);
                _qualityDropdown.value = Mathf.Clamp(save.QualityLevel, 0, names.Count - 1);
                _qualityDropdown.onValueChanged.RemoveAllListeners();
                _qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            }

            // Resolution
            if (_resolutionDropdown != null)
            {
                //_availableResolutions = Screen.resolutions;
                //_resolutionDropdown.ClearOptions();

                //var opts        = new List<string>();
                //int currentIdx  = 0;
                //int savedIdx    = save.ResolutionIndex;

                //for (int i = 0; i < _availableResolutions.Length; i++)
                //{
                //    var r = _availableResolutions[i];
                //    opts.Add($"{r.width} x {r.height} @ {r.refreshRateRatio.numerator}Hz");

                //    if (savedIdx < 0)
                //    {
                //        if (r.width == Screen.currentResolution.width
                //            && r.height == Screen.currentResolution.height)
                //            currentIdx = i;
                //    }
                //    else if (i == savedIdx)
                //    {
                //        currentIdx = i;
                //    }
                //}

                //_resolutionDropdown.AddOptions(opts);
                //_resolutionDropdown.value = currentIdx;
                //_resolutionDropdown.onValueChanged.RemoveAllListeners();
                //_resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

                _availableResolutions = BuildResolutionList();
                _resolutionDropdown.ClearOptions();

                var opts = new List<string>();
                for (int i = 0; i < _availableResolutions.Length; i++)
                {
                    var r = _availableResolutions[i];
                    opts.Add($"{r.width} x {r.height}");
                }

                int currentIdx = FindResolutionIndex(save);
                _resolutionDropdown.AddOptions(opts);
                _resolutionDropdown.value = currentIdx;
                _resolutionDropdown.RefreshShownValue();

                _resolutionDropdown.onValueChanged.RemoveAllListeners();
                _resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            }

            // Window Mode
            if (_windowModeDropdown != null)
            {

                _windowModeDropdown.ClearOptions();
                _windowModeDropdown.AddOptions(new List<string>
                {
                    LocalizationManager.Get("settings_window_fullscreen"),
                     LocalizationManager.Get("settings_window_windowed"),
                      //LocalizationManager.Get("settings_window_borderless_windowed"),

                });
                _windowModeDropdown.value = Mathf.Clamp(save.WindowModeIndex, 0, 1);
                _windowModeDropdown.onValueChanged.RemoveAllListeners();
                _windowModeDropdown.onValueChanged.AddListener(OnWindowModeChanged);
            }

            // Apply saved video settings immediately
            ApplyVideoSettings(save);
        }

        private void ApplyVideoSettings(SaveData save)
        {
            QualitySettings.SetQualityLevel(save.QualityLevel, true);
            QualitySettings.vSyncCount = save.VSyncEnabled ? 1 : 0;

            FullScreenMode mode = IndexToFullScreenMode(save.WindowModeIndex);
            Screen.fullScreenMode = mode;

            if (save.ResolutionWidth > 0 && save.ResolutionHeight > 0)
                Screen.SetResolution(save.ResolutionWidth, save.ResolutionHeight, mode);
        }

        private Resolution[] BuildResolutionList()
        {
            var bestBySize = new Dictionary<(int width, int height), Resolution>();
            Resolution[] raw = Screen.resolutions;

            if (raw != null)
            {
                for (int i = 0; i < raw.Length; i++)
                {
                    var r = raw[i];
                    var key = (r.width, r.height);

                    // Keep the list readable and consistent, but always add the current resolution below.
                    if (!commonResolutions.Contains(key))
                        continue;

                    if (!bestBySize.TryGetValue(key, out var existing)
                        || r.refreshRateRatio.value > existing.refreshRateRatio.value)
                    {
                        bestBySize[key] = r;
                    }
                }
            }

            Resolution current = Screen.currentResolution;
            var currentKey = (current.width, current.height);
            if (current.width > 0 && current.height > 0 && !bestBySize.ContainsKey(currentKey))
                bestBySize[currentKey] = current;

            if (bestBySize.Count == 0 && current.width > 0 && current.height > 0)
                bestBySize[currentKey] = current;

            return bestBySize.Values
                .OrderBy(r => r.width)
                .ThenBy(r => r.height)
                .ToArray();
        }

        private int FindResolutionIndex(SaveData save)
        {
            if (_availableResolutions == null || _availableResolutions.Length == 0)
                return 0;

            int desiredWidth = save.ResolutionWidth > 0
                ? save.ResolutionWidth
                : Screen.currentResolution.width;
            int desiredHeight = save.ResolutionHeight > 0
                ? save.ResolutionHeight
                : Screen.currentResolution.height;

            for (int i = 0; i < _availableResolutions.Length; i++)
            {
                if (_availableResolutions[i].width == desiredWidth
                    && _availableResolutions[i].height == desiredHeight)
                    return i;
            }

            // Fallback for old saves that only stored a dropdown index.
            if (save.ResolutionWidth <= 0 && save.ResolutionHeight <= 0
                && save.ResolutionIndex >= 0 && save.ResolutionIndex < _availableResolutions.Length)
                return save.ResolutionIndex;

            return 0;
        }

        // =================================================================
        // SOUND TAB SETUP
        // =================================================================

        private void SetupSoundTab()
        {
            BindSoundSliders();
            RefreshSoundSlidersFromRuntime();
        }

        private void BindSoundSliders()
        {
            // SettingsWindowUI is used both in the Hub and in the in-run pause/settings window.
            // Bind every OnEnable because pooled/animated windows can be recreated or disabled between scenes.
            SetupSlider(_masterSlider,     OnMasterVolumeChanged);
            SetupSlider(_gameSoundsSlider, OnGameSoundsVolumeChanged);
            SetupSlider(_uiSlider,         OnUIVolumeChanged);
            SetupSlider(_musicSlider,      OnMusicVolumeChanged);
            _soundSlidersBound = true;
        }

        private void SetupSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback)
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.onValueChanged.RemoveListener(callback);
            slider.onValueChanged.AddListener(callback);
        }

        private void RefreshSoundSlidersFromRuntime()
        {
            var am   = AudioManager.Instance;
            var save = SaveSystem.Load();

            float master = Mathf.Clamp01(am != null ? am.MasterVolume : save.MasterVolume);
            float game   = Mathf.Clamp01(am != null ? am.GameSoundsVolume : save.GameSoundsVolume);
            float ui     = Mathf.Clamp01(am != null ? am.UIVolume : save.UIVolume);
            float music  = Mathf.Clamp01(am != null ? am.MusicVolume : save.MusicVolume);

            SetSliderValueWithoutNotify(_masterSlider, master);
            SetSliderValueWithoutNotify(_gameSoundsSlider, game);
            SetSliderValueWithoutNotify(_uiSlider, ui);
            SetSliderValueWithoutNotify(_musicSlider, music);

            CacheSoundValues(master, game, ui, music);
            UpdateVolumeText(_masterValueText, master);
            UpdateVolumeText(_gameSoundsValueText, game);
            UpdateVolumeText(_uiValueText, ui);
            UpdateVolumeText(_musicValueText, music);
        }

        private static void SetSliderValueWithoutNotify(Slider slider, float value)
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
        }

        private void CacheSoundValues(float master, float game, float ui, float music)
        {
            _lastMasterVolume = master;
            _lastGameSoundsVolume = game;
            _lastUIVolume = ui;
            _lastMusicVolume = music;
        }

        // =================================================================
        // GAMEPLAY TAB SETUP
        // =================================================================

        private void OnAlwaysSkipPrepChanged(bool value)
        {
            if (_initializing) return;

            var save = SaveSystem.Load();
            if (SaveSystem.IsFirstTimePlayer(save))
            {
                save.AlwaysSkipPrep = false;
                SaveSystem.Save(save);
                _alwaysSkipPrepToggle?.SetIsOnWithoutNotify(false);
                return;
            }

            save.AlwaysSkipPrep = value;
            SaveSystem.Save(save);
        }

        private void SetupGameplayTab()
        {
            var save = SaveSystem.Load();
            RefreshGameplayStaticTexts();

            if (_panSpeedSlider != null)
            {
                _panSpeedSlider.minValue = _panSpeedMin;
                _panSpeedSlider.maxValue = _panSpeedMax;
                _panSpeedSlider.value    = save.CameraPanSpeed;
                _panSpeedSlider.onValueChanged.RemoveAllListeners();
                _panSpeedSlider.onValueChanged.AddListener(OnPanSpeedChanged);
                UpdateSliderText(_panSpeedValueText, save.CameraPanSpeed, "");
            }

            if (_zoomSpeedSlider != null)
            {
                _zoomSpeedSlider.minValue = _zoomSpeedMin;
                _zoomSpeedSlider.maxValue = _zoomSpeedMax;
                _zoomSpeedSlider.value    = save.CameraZoomSpeed;
                _zoomSpeedSlider.onValueChanged.RemoveAllListeners();
                _zoomSpeedSlider.onValueChanged.AddListener(OnZoomSpeedChanged);
                UpdateSliderText(_zoomSpeedValueText, save.CameraZoomSpeed, "");
            }

            if (_alwaysSkipPrepToggle != null)
            {
                bool isFirstTimePlayer = SaveSystem.IsFirstTimePlayer(save);
                bool alwaysSkipValue = !isFirstTimePlayer && save.AlwaysSkipPrep;

                if (isFirstTimePlayer && save.AlwaysSkipPrep)
                {
                    save.AlwaysSkipPrep = false;
                    SaveSystem.Save(save);
                }

                _alwaysSkipPrepToggle.SetIsOnWithoutNotify(alwaysSkipValue);
                _alwaysSkipPrepToggle.interactable = !isFirstTimePlayer;
                _alwaysSkipPrepToggle.onValueChanged.RemoveAllListeners();
                _alwaysSkipPrepToggle.onValueChanged.AddListener(OnAlwaysSkipPrepChanged);
            }

            if (_floatingDamageNumbersToggle != null)
            {
                _floatingDamageNumbersToggle.isOn = save.FloatingDamageNumbersEnabled;
                _floatingDamageNumbersToggle.onValueChanged.RemoveAllListeners();
                _floatingDamageNumbersToggle.onValueChanged.AddListener(v =>
                {
                    if (_initializing) return;
                    save.FloatingDamageNumbersEnabled = v;
                    save.FloatingDamageNumbersPreferenceInitialized = true;
                    SaveSystem.Save(save);
                });
            }
        }

        // =================================================================
        // CALLBACKS — VIDEO
        // =================================================================

        private void OnVSyncChanged(bool enabled)
        {
            if (_initializing) return;
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            var save = SaveSystem.Load();
            save.VSyncEnabled = enabled;
            SaveSystem.Save(save);
        }

        private void OnQualityChanged(int index)
        {
            if (_initializing) return;
            QualitySettings.SetQualityLevel(index, true);
            var save = SaveSystem.Load();
            save.QualityLevel = index;
            SaveSystem.Save(save);
        }

        private void OnResolutionChanged(int index)
        {
            if (_initializing || _availableResolutions == null
                || index >= _availableResolutions.Length) return;

            var r = _availableResolutions[index];
            Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);
            var save = SaveSystem.Load();
            save.ResolutionIndex = index; // legacy fallback
            save.ResolutionWidth = r.width;
            save.ResolutionHeight = r.height;
            SaveSystem.Save(save);
        }

        private void OnWindowModeChanged(int index)
        {
            if (_initializing) return;
            Screen.fullScreenMode = IndexToFullScreenMode(index);
            var save = SaveSystem.Load();
            save.WindowModeIndex = index;
            SaveSystem.Save(save);
        }

        private static FullScreenMode IndexToFullScreenMode(int index) => index switch
        {
            1 => FullScreenMode.Windowed,
            2 => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.ExclusiveFullScreen
        };

        // =================================================================
        // CALLBACKS — SOUND
        // =================================================================

        private void OnMasterVolumeChanged(float v)
        {
            if (_initializing) return;
            v = Mathf.Clamp01(v);
            if (Mathf.Approximately(v, _lastMasterVolume)) return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMasterVolume(v);
            else
                SaveVolumeFallback(master: v);

            _lastMasterVolume = v;
            UpdateVolumeText(_masterValueText, v);
        }

        private void OnGameSoundsVolumeChanged(float v)
        {
            if (_initializing) return;
            v = Mathf.Clamp01(v);
            if (Mathf.Approximately(v, _lastGameSoundsVolume)) return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.SetGameSoundsVolume(v);
            else
                SaveVolumeFallback(gameSounds: v);

            _lastGameSoundsVolume = v;
            UpdateVolumeText(_gameSoundsValueText, v);
        }

        private void OnUIVolumeChanged(float v)
        {
            if (_initializing) return;
            v = Mathf.Clamp01(v);
            if (Mathf.Approximately(v, _lastUIVolume)) return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.SetUIVolume(v);
            else
                SaveVolumeFallback(ui: v);

            _lastUIVolume = v;
            UpdateVolumeText(_uiValueText, v);
        }

        private void OnMusicVolumeChanged(float v)
        {
            if (_initializing) return;
            v = Mathf.Clamp01(v);
            if (Mathf.Approximately(v, _lastMusicVolume)) return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMusicVolume(v);
            else
                SaveVolumeFallback(music: v);

            _lastMusicVolume = v;
            UpdateVolumeText(_musicValueText, v);
        }

        private void SaveVolumeFallback(float? master = null, float? gameSounds = null, float? ui = null, float? music = null)
        {
            var save = SaveSystem.Load();
            if (master.HasValue) save.MasterVolume = Mathf.Clamp01(master.Value);
            if (gameSounds.HasValue) save.GameSoundsVolume = Mathf.Clamp01(gameSounds.Value);
            if (ui.HasValue) save.UIVolume = Mathf.Clamp01(ui.Value);
            if (music.HasValue) save.MusicVolume = Mathf.Clamp01(music.Value);
            SaveSystem.Save(save);
        }

        // =================================================================
        // CALLBACKS — GAMEPLAY
        // =================================================================

        private void OnPanSpeedChanged(float v)
        {
            if (_initializing) return;
            UpdateSliderText(_panSpeedValueText, v, "");
            ApplyCameraSettings();
        }

        private void OnZoomSpeedChanged(float v)
        {
            if (_initializing) return;
            UpdateSliderText(_zoomSpeedValueText, v, "");
            ApplyCameraSettings();
        }

        private void ApplyCameraSettings()
        {
            float pan  = _panSpeedSlider  != null ? _panSpeedSlider.value  : 20f;
            float zoom = _zoomSpeedSlider != null ? _zoomSpeedSlider.value : 5f;

            // Find camera controller in scene
            var cam = FindFirstObjectByType<Input.CameraController>();
            cam?.SetSpeeds(pan, zoom);

            var save = SaveSystem.Load();
            save.CameraPanSpeed  = pan;
            save.CameraZoomSpeed = zoom;
            SaveSystem.Save(save);
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private void UpdateVolumeText(TMP_Text txt, float v)
        {
            if (txt != null) txt.text = $"{Mathf.RoundToInt(v * 100)}%";
        }

        private void UpdateSliderText(TMP_Text txt, float v, string suffix)
        {
            if (txt != null) txt.text = $"{v:F1}{suffix}";
        }
    }
}
