// ============================================================================
// ETD.Hub - HubController.cs
// Hub scene: manages all hub windows (Planning, Challenges, Shop, Turrets,
// Settings, Language). Handles button routing and leaderboard display.
// ============================================================================
using UnityEngine;
using UnityEngine.InputSystem;
using ETD.Core;
using ETD.Data;
using ETD.Meta;
using ETD.Gameplay;

namespace ETD.Hub
{
    public class HubController : MonoBehaviour
    {
        [Header("Windows - Assign in Inspector")]
        [SerializeField] private GameObject _planningWindow;
        [SerializeField] private GameObject _challengesWindow;
        [SerializeField] private GameObject _shopWindow;
        [SerializeField] private GameObject _turretWindow;
        [SerializeField] private GameObject _settingsWindow;
        [SerializeField] private GameObject _languageWindow;
        [SerializeField] private GameObject _wikiWindow;
        [SerializeField] private GameObject _leaderboardPanel;

        [Header("Button Badges")]
        [SerializeField] private HubBadge _planningBadge;   // on Planning button
        [SerializeField] private HubBadge _challengesBadge; // on Challenges button
        [SerializeField] private HubBadge _turretsBadge;    // on Turrets button

        [Header("Data")]
        [SerializeField] private GameDatabase _database;
        [SerializeField] private GameSoundConfig _soundData;

        [SerializeField] private ResumePopupUI _resumePopup;

        private MetaProgressionManager _metaManager;
        private GameObject _activeWindow;

        public static HubController Instance;

        void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _metaManager = ServiceLocator.Get<MetaProgressionManager>();
            CloseAllWindows();
            UpdateLeaderboard();
            HubBadgeRegistry.LoadFromSave();
            RefreshButtonBadges();
            if (_soundData != null)
                AudioManager.Instance?.PlayMusicPlaylist(_soundData.HubMusicPlaylist, _soundData.HubMusic);
        }

        // === BUTTON HANDLERS (wire in Inspector) ===

        //public void OnPlayClicked()
        //{
        //    GameManager.Instance.LoadGame();
        //}

        //public void OnPlayClicked()
        //{
        //    var save = SaveSystem.Load();
        //    if (save.HasSavedRun && save.SavedRun != null)
        //    {
        //        _resumePopup?.Show(save.SavedRun,
        //            onContinue: () => StartGame(resume: true),
        //            onNewRun: () => StartGame(resume: false));
        //    }
        //    else
        //    {
        //        StartGame(resume: false);
        //    }
        //}

        public void OnPlayClicked()
        {
            // Check hub tutorial
            var hubTutorial = FindObjectOfType<HubTutorial>();
            if (hubTutorial != null)
            {
                bool canPlay = hubTutorial.CheckAndAdvanceTutorial();
                if (!canPlay) return; // tutorial will call OnPlayClicked again
            }

            var save = SaveSystem.Load();
            if (save.HasSavedRun && save.SavedRun != null)
            {
                _resumePopup?.Show(
                    save.SavedRun,
                    onContinue: () =>
                    {
                        PlayerPrefs.SetInt("ResumeRun", 1);
                        SceneLoader.Instance.LoadScene(GameConstants.GAME_SCENE_NAME);
                    },
                    onNewRun: () =>
                    {
                        RunSnapshotManager.ClearSnapshot();
                        PlayerPrefs.SetInt("ResumeRun", 0);
                        SceneLoader.Instance.LoadScene(GameConstants.GAME_SCENE_NAME);
                    });
            }
            else
            {
                PlayerPrefs.SetInt("ResumeRun", 0);
                SceneLoader.Instance.LoadScene(GameConstants.GAME_SCENE_NAME);
            }
        }


        private void StartGame(bool resume)
        {
            PlayerPrefs.SetInt("ResumeRun", resume ? 1 : 0);
            GameManager.Instance.LoadGame();
        }

        public void OnPlanningClicked()
        {
            ToggleWindow(_planningWindow, HubWindowType.Planning);
        }

        public void OnChallengesClicked()
        {
            ToggleWindow(_challengesWindow, HubWindowType.Challenges);
        }

        public void OnShopClicked()
        {
            ToggleWindow(_shopWindow, HubWindowType.Shop);
        }

        public void OnTurretsClicked()
        {
            ToggleWindow(_turretWindow, HubWindowType.Turrets);
        }

        public void OnSettingsClicked()
        {
            ToggleWindow(_settingsWindow, HubWindowType.Settings);
        }

        public void OnLanguageClicked()
        {
            ToggleWindow(_languageWindow, HubWindowType.Language);
        }

        public void OnWikiClicked()
        {
            ToggleWindow(_wikiWindow, HubWindowType.Wiki);
        }

        public void OnExitClicked()
        {
            if (SaveManager.Instance != null)
                SaveManager.Instance.SaveAll();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public void RefreshButtonBadges()
        {
            _planningBadge?.SetVisible(HubBadgeRegistry.AnyNew(HubBadgeType.Trait));
            _challengesBadge?.SetVisible(HubBadgeRegistry.AnyNew(HubBadgeType.Challenge));
            _turretsBadge?.SetVisible(HubBadgeRegistry.AnyNew(HubBadgeType.Turret));
        }
        // === WINDOW MANAGEMENT ===

        private void ToggleWindow(GameObject window, HubWindowType type)
        {
            if (window == null) return;

            //if (_activeWindow == window)
            //{
            //    CloseAllWindows();
            //    return;
            //}

            CloseAllWindows();
            window.SetActive(true);
            _activeWindow = window;
            EventBus.Publish(new HubWindowOpenedEvent { WindowType = type });
        }

        private void CloseAllWindows()
        {
            if (_planningWindow != null) _planningWindow.SetActive(false);
            if (_challengesWindow != null) _challengesWindow.SetActive(false);
            if (_shopWindow != null) _shopWindow.SetActive(false);
            if (_turretWindow != null) _turretWindow.SetActive(false);
            if (_settingsWindow != null) _settingsWindow.SetActive(false);
            if (_languageWindow != null) _languageWindow.SetActive(false);
            if (_wikiWindow != null) _wikiWindow.SetActive(false);
            _activeWindow = null;
        }

        private void UpdateLeaderboard()
        {
            // Leaderboard panel stays visible; populated from save data
            var save = SaveSystem.Load();
            // UI binding handled by LeaderboardUI component on the panel
        }

        private void Update()
        {
            // Keyboard Escape or a gamepad East-button press (fixed binding, see
            // [[etd-v1-full-release]] Phase 5) both back out of an open window.
            bool keyboardCancel = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool gamepadCancel = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;

            if (keyboardCancel || gamepadCancel)
            {
                if (_activeWindow != null)
                    CloseAllWindows();
            }
        }

        public Color GetColor(SpecCardRarity rarity)
        {
            return rarity switch
            {
                SpecCardRarity.Common => _database.GradeColors[0],
                SpecCardRarity.Uncommon => _database.GradeColors[1],
                SpecCardRarity.Rare => _database.GradeColors[2],
                SpecCardRarity.Unique => _database.GradeColors[3],
                SpecCardRarity.Legendary => _database.GradeColors[4],
                _ => Color.white
            };
        }
    }
}
