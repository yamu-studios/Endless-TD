// ============================================================================
// ETD.UI - GameOverUI.cs
// Shown on game over. Displays final score, waves survived, meta earned.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Gameplay;

namespace ETD.UI
{
    public class GameOverUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _wavesText;
        [SerializeField] private TMP_Text _metaEarnedText;
        [SerializeField] private TMP_Text _timeText;
        [Header("Damage Stats")]
        [SerializeField] private EndGameDamageStatsUI _damageStatsPanel;
        [SerializeField] private TMP_Text _damageStatsText; // Legacy fallback text output. Prefer EndGameDamageStatsUI for polished rows.
        [SerializeField] private Button _returnToHubButton;
        [SerializeField] private Button _retryButton;

        private bool _suppressForDemoComplete;
        private int _suppressDemoGameOverSequence = -1;
        private bool _subscribed;
        private int _shownGameOverSequence = -1;

        private void Awake()
        {
            if (_panel != null)
                _panel.SetActive(false);

            _returnToHubButton?.onClick.RemoveListener(OnReturnToHub);
            _returnToHubButton?.onClick.AddListener(OnReturnToHub);
            _retryButton?.onClick.RemoveListener(OnRetry);
            _retryButton?.onClick.AddListener(OnRetry);
        }

        private void OnEnable()
        {
            SubscribeEvents();
            TryRecoverMissedGameOverEvent();
        }

        private void LateUpdate()
        {
            // Safety net for retry/scene reload edge cases: if GameManager already
            // reached GameOver but this UI missed the event for any reason, show it.
            TryRecoverMissedGameOverEvent();
        }

        private void SubscribeEvents()
        {
            if (_subscribed)
                return;

            EventBus.Subscribe<GameOverEvent>(OnGameOver);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Subscribe<DemoCompletedEvent>(OnDemoCompleted);
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed)
                return;

            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Unsubscribe<DemoCompletedEvent>(OnDemoCompleted);
            _subscribed = false;
        }

        private void TryRecoverMissedGameOverEvent()
        {
            var manager = GameManager.Instance;
            if (manager == null)
                return;

            if (manager.CurrentState != GameState.GameOver)
                return;

            if (!manager.HasLastGameOverResult)
                return;

            if (_shownGameOverSequence == manager.LastGameOverSequence)
                return;

            OnGameOver(new GameOverEvent
            {
                Score = manager.LastGameOverScore,
                WavesCompleted = manager.LastGameOverWavesCompleted
            });
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if ((GameState)evt.NewState == GameState.GameOver)
                TryRecoverMissedGameOverEvent();
        }

        private void OnGameOver(GameOverEvent evt)
        {
            var manager = GameManager.Instance;
            if (manager != null)
                _shownGameOverSequence = manager.LastGameOverSequence;

            if (ShouldSuppressForDemoComplete())
            {
                HideForDemoComplete();
                return;
            }

            if (_panel != null)
                _panel.SetActive(true);
            else
                gameObject.SetActive(true);

            if (_scoreText != null) _scoreText.text = $"{evt.Score}";
            if (_wavesText != null) _wavesText.text = $"{evt.WavesCompleted}";

            var runMgr = ServiceLocator.Get<Gameplay.RunManager>();
            if (runMgr != null)
            {
                if (_metaEarnedText != null)
                    _metaEarnedText.text = $"{runMgr.RunData.MetaCurrencyEarned}";
                if (_timeText != null)
                    _timeText.text = $"{Utilities.GameUtilities.FormatTime(runMgr.RunData.TotalTime)}";
            }

            if (_damageStatsPanel == null)
                _damageStatsPanel = GetComponentInChildren<EndGameDamageStatsUI>(true);

            RunDamageStatsTracker damageStats = RunDamageStatsTracker.Active;
            if (damageStats == null)
                ServiceLocator.TryGet(out damageStats);

            if (damageStats != null)
            {
                if (_damageStatsPanel != null)
                    _damageStatsPanel.Show(damageStats);

                if (_damageStatsText != null)
                    _damageStatsText.text = damageStats.BuildLocalizedSummary();
            }
            else
            {
                if (_damageStatsPanel != null)
                    _damageStatsPanel.Clear();

                if (_damageStatsText != null)
                    _damageStatsText.text = string.Empty;
            }
        }

        private void OnDemoCompleted(DemoCompletedEvent _)
        {
            HideForDemoComplete();
        }

        public void HideForDemoComplete()
        {
            _suppressForDemoComplete = true;

            var manager = GameManager.Instance;
            _suppressDemoGameOverSequence = manager != null ? manager.LastGameOverSequence : -1;

            if (_panel != null)
                _panel.SetActive(false);
        }

        private bool ShouldSuppressForDemoComplete()
        {
            var manager = GameManager.Instance;
            if (manager == null)
                return false;

            // Local suppression is only valid for the exact sequence where the
            // DemoCompletedEvent was received. Do not keep suppressing after Retry.
            if (_suppressForDemoComplete
                && _suppressDemoGameOverSequence >= 0
                && manager.LastGameOverSequence == _suppressDemoGameOverSequence)
            {
                return true;
            }

            // Static demo state is also sequence-scoped now. DemoCompletedThisSession
            // alone must not hide normal GameOverUI on a later retry run.
            return DemoMode.ShouldSuppressGameOverUIForCurrentSequence(manager);
        }

        private void OnReturnToHub()
        {
            _shownGameOverSequence = -1;
            _suppressForDemoComplete = false;
            _suppressDemoGameOverSequence = -1;

            if (_panel != null)
                _panel.SetActive(false);

            UnsubscribeEvents();
            GameManager.Instance.LoadHub();
        }

        private void OnRetry()
        {
            _shownGameOverSequence = -1;
            _suppressForDemoComplete = false;
            _suppressDemoGameOverSequence = -1;

            if (_panel != null)
                _panel.SetActive(false);

            // Match PauseMenuUI retry behavior so run-scoped systems can reset.
            EventBus.Publish(new RetryEvent { });

            // The old scene is about to unload. Unsubscribe now so retry cannot leave
            // a stale GameOverUI handler in EventBus before the new scene subscribes.
            UnsubscribeEvents();
            GameManager.Instance.LoadGame();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }
    }
}
