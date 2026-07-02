// ============================================================================
// ETD.Hub - ResumePopupUI.cs  [NEW]
// Shown when player clicks Play and a saved run exists.
// "Continue your last run?" → Yes / No
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Gameplay;

namespace ETD.Hub
{
    public class ResumePopupUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text   _titleText;
        [SerializeField] private TMP_Text   _waveText;
        [SerializeField] private TMP_Text   _scoreText;
        [SerializeField] private TMP_Text   _timeText;
        [SerializeField] private TMP_Text   _continueButtonText;
        [SerializeField] private TMP_Text   _newRunButtonText;

       
        [SerializeField] private Button     _continueButton;
        [SerializeField] private Button     _newRunButton;
        [SerializeField] private Button     _backdrop;

        private System.Action _onContinue;
        private System.Action _onNewRun;

        private void Awake()
        {
            _continueButton?.onClick.AddListener(OnContinue);
            _newRunButton?.onClick.AddListener(OnNewRun);
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
            LocalizeStaticLabels();
            // Remove: _backdrop?.onClick.AddListener(OnNewRun);
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            LocalizeStaticLabels();
        }

        private void LocalizeStaticLabels()
        {
            if (_titleText != null)
                _titleText.text = LocalizationManager.Get("resume_title", "Continue your last run?");
            if (_continueButtonText != null)
                _continueButtonText.text = LocalizationManager.Get("general_continue", "Continue");
            if (_newRunButtonText != null)
                _newRunButtonText.text = LocalizationManager.Get("general_new_game", "New Game");
        }

        // =================================================================
        // SHOW
        // =================================================================

        public void Show(RunSnapshot snapshot, System.Action onContinue, System.Action onNewRun)
        {
            _onContinue = onContinue;
            _onNewRun   = onNewRun;

            LocalizeStaticLabels();
            if (_waveText  != null) _waveText.text  = LocalizationManager.GetFormat("resume_wave_format", "Wave {0}", snapshot.Wave);
            if (_scoreText != null) _scoreText.text = LocalizationManager.GetFormat("resume_score_format", "Score: {0:N0}", snapshot.Score);
            if (_timeText != null && snapshot.TotalTime > 0)
            {
                int mins = Mathf.FloorToInt(snapshot.TotalTime / 60f);
                int secs = Mathf.FloorToInt(snapshot.TotalTime % 60f);
                _timeText.text = $"{mins:D2}:{secs:D2}";
            }

            if (_panel != null) _panel.SetActive(true);


        }

        private void OnContinue()
        {
            if (_panel != null) _panel.SetActive(false);
            _onContinue?.Invoke();
        }

        private void OnNewRun()
        {
            RunSnapshotManager.ClearSnapshot();
            if (_panel != null) _panel.SetActive(false);
            _onNewRun?.Invoke();
        }
    }
}
