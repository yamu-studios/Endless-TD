// ============================================================================
// ETD.UI - PauseMenuUI.cs  [NEW]
// In-game pause menu with Resume, Settings, Exit to Hub options.
// Listens for GamePausedEvent to show/hide.
// Settings sub-panel reuses SettingsWindowUI logic.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using ETD.Core;
using ETD.Gameplay;

namespace ETD.UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private GameObject _settingsSubPanel;

        [Header("Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _exitToHubButton;
        [SerializeField] private Button _settingsBackButton;

        [Header("Settings (inline)")]
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;

        [Header("Info")]
        [SerializeField] private TMP_Text _waveInfoText;
        [SerializeField] private TMP_Text _timeInfoText;

        private bool _initializingVolumeSliders;

        public static PauseMenuUI Instance;
        private void Awake()
        {
            Instance = this;

            if (_pausePanel != null)
                _pausePanel.SetActive(false);

            if (_settingsSubPanel != null)
                _settingsSubPanel.SetActive(false);

            EventBus.Subscribe<GamePausedEvent>(OnGamePaused);
            EventBus.Subscribe<SettingToggleEvent>(OnSettingToggleEvent);

            _resumeButton?.onClick.AddListener(OnResumeClicked);
            _retryButton?.onClick.AddListener(OnRetryClicked);
            _settingsButton?.onClick.AddListener(OnSettingsClicked);
            _settingsBackButton?.onClick.AddListener(OnSettingsBackClicked);
            _exitToHubButton?.onClick.AddListener(OnExitClicked);

            BindVolumeSliders();
        }
        private void OnRetryClicked()
        {
            // Reset time scale before loading
            Time.timeScale = 1f;
            EventBus.Publish(new RetryEvent { });
            GameManager.Instance.LoadGame();
        }
        private void OnGamePaused(GamePausedEvent evt)
        {
            bool isPaused = GameManager.Instance.CurrentState == GameState.Paused;

            if (_pausePanel != null)
                _pausePanel.SetActive(isPaused);

            if (isPaused)
            {
                ShowMainMenu();
                LoadVolumeSettings();
                UpdateInfoText();
            }
        }

        private void ShowMainMenu()
        {
            KeybindingSettingsUI.ForceCancelAllActiveRebinds();
            EventBus.Publish(new SettingToggleEvent { IsActive = false });
        }

        private void BindVolumeSliders()
        {
            SetupVolumeSlider(_masterVolumeSlider, OnMasterVolumeChanged);
            SetupVolumeSlider(_musicVolumeSlider, OnMusicVolumeChanged);
            SetupVolumeSlider(_sfxVolumeSlider, OnSfxVolumeChanged);
        }

        private void SetupVolumeSlider(Slider slider, UnityAction<float> callback)
        {
            if (slider == null)
                return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.onValueChanged.RemoveListener(callback);
            slider.onValueChanged.AddListener(callback);
        }

        private void LoadVolumeSettings()
        {
            var am = AudioManager.Instance;
            var save = SaveSystem.Load();

            float master = am != null ? am.MasterVolume : save.MasterVolume;
            float music = am != null ? am.MusicVolume : save.MusicVolume;
            float gameSounds = am != null ? am.GameSoundsVolume : save.GameSoundsVolume;

            _initializingVolumeSliders = true;
            if (_masterVolumeSlider != null) _masterVolumeSlider.SetValueWithoutNotify(master);
            if (_musicVolumeSlider != null) _musicVolumeSlider.SetValueWithoutNotify(music);
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.SetValueWithoutNotify(gameSounds);
            _initializingVolumeSliders = false;
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (_initializingVolumeSliders)
                return;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMasterVolume(value);
                return;
            }

            var save = SaveSystem.Load();
            save.MasterVolume = Mathf.Clamp01(value);
            SaveSystem.Save(save);
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (_initializingVolumeSliders)
                return;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMusicVolume(value);
                return;
            }

            var save = SaveSystem.Load();
            save.MusicVolume = Mathf.Clamp01(value);
            SaveSystem.Save(save);
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (_initializingVolumeSliders)
                return;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetGameSoundsVolume(value);
                return;
            }

            var save = SaveSystem.Load();
            save.GameSoundsVolume = Mathf.Clamp01(value);
            SaveSystem.Save(save);
        }

        private void UpdateInfoText()
        {
            if (ServiceLocator.TryGet<RunManager>(out var runMgr))
            {
                if (_waveInfoText != null)
                    _waveInfoText.text = $"{runMgr.RunData.CurrentWave}";
                if (_timeInfoText != null)
                {
                    float secs = runMgr.RunData.TotalTime;
                    int m = Mathf.FloorToInt(secs / 60f);
                    int s = Mathf.FloorToInt(secs % 60f);
                    _timeInfoText.text = $"{m:D2}:{s:D2}";
                }
            }
        }

        private void OnResumeClicked()
        {
            GameManager.Instance.TogglePause();
        }

        private void OnSettingsClicked()
        {
            KeybindingSettingsUI.ForceCancelAllActiveRebinds();
            EventBus.Publish(new SettingToggleEvent { IsActive = true });
        }

        private void OnSettingsBackClicked()
        {
            KeybindingSettingsUI.ForceCancelAllActiveRebinds();
            EventBus.Publish(new SettingToggleEvent { IsActive = false });
        }

        private void OnExitClicked()
        {
            // Save current run stats before exiting
            if (ServiceLocator.TryGet<RunManager>(out var runMgr))
            {
                SaveSystem.UpdateLeaderboard(runMgr.RunData.CurrentWave > 0 ? runMgr.RunData.CurrentWave : runMgr.RunData.Score);
            }

            GameManager.Instance.LoadHub();
        }

        public bool IsSettingsOpen
        {
            get
            {
                return _settingsSubPanel != null && _settingsSubPanel.activeSelf;
            }
        }

        public void CloseSettingsOnly()
        {
            KeybindingSettingsUI.ForceCancelAllActiveRebinds();

            if (_settingsSubPanel != null)
                _settingsSubPanel.SetActive(false);

            EventBus.Publish(new SettingToggleEvent { IsActive = false });
        }
        private void OnSettingToggleEvent(SettingToggleEvent evt)
        {
            if (_settingsSubPanel == null)
                return;

            if (!evt.IsActive)
                KeybindingSettingsUI.ForceCancelAllActiveRebinds();

            _settingsSubPanel.SetActive(evt.IsActive);

            if (evt.IsActive)
                LoadVolumeSettings();
        }

        private void OnDestroy()
        {
            KeybindingSettingsUI.ForceCancelAllActiveRebinds();
            EventBus.Unsubscribe<GamePausedEvent>(OnGamePaused);
            EventBus.Unsubscribe<SettingToggleEvent>(OnSettingToggleEvent);
        }
    }
}
