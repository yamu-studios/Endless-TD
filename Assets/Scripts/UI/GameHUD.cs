// ============================================================================
// ETD.UI - GameHUD.cs
// Main in-game HUD. Displays gold, lives, score, wave, time, level/XP bar,
// meta-currency, prep timer, active traits.
// Wire TMP fields in Inspector.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Data;
using ETD.Gameplay;
using ETD.Waves;
using ETD.Traits;

namespace ETD.UI
{
    public class GameHUD : MonoBehaviour
    {
        [Header("Top Bar")]
        [SerializeField] private TMP_Text _goldText;
        [SerializeField] private TMP_Text _livesText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _totalTimeText;
        [SerializeField] private TMP_Text _metaCurrencyText;

        [Header("XP Bar")]
        [SerializeField] private Image _xpBarFill;
        [SerializeField] private TMP_Text _levelText;

        [Header("Wave Timer")]
        [SerializeField] private TMP_Text _prepTimerText;
        [SerializeField] private Button _skipPrepButton;
        [SerializeField] private GameObject _prepTimerPanel;



        [Header("Traits")]
        [SerializeField] private Transform _traitIconContainer;
        [SerializeField] private GameObject _traitIconPrefab;


        private RunManager _runManager;
        private WaveManager _waveManager;
        private int _lastDisplayedTotalSeconds = -1;
        private int _lastDisplayedPrepSeconds = -999;
        private bool _prepPanelVisible;


        private void Start()
        {
            _runManager = ServiceLocator.Get<RunManager>();
            _waveManager = ServiceLocator.Get<WaveManager>();


            _skipPrepButton?.onClick.AddListener(OnSkipPrepClicked);

            // Subscribe to events
            EventBus.Subscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Subscribe<LivesChangedEvent>(OnLivesChanged);
            EventBus.Subscribe<ScoreChangedEvent>(OnScoreChanged);
            EventBus.Subscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Subscribe<XPGainedEvent>(OnXPChanged);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<MetaCurrencyChangedEvent>(OnMetaCurrencyChanged);

            // Initial state
            RefreshAll();
        }

        private void Update()
        {
            if (_runManager == null) return;

            // Total time changes visibly only once per second; avoid TMP string work every frame.
            if (_totalTimeText != null)
            {
                int totalSeconds = Mathf.FloorToInt(_runManager.RunData.TotalTime);
                if (totalSeconds != _lastDisplayedTotalSeconds)
                {
                    _lastDisplayedTotalSeconds = totalSeconds;
                    _totalTimeText.text = Utilities.GameUtilities.FormatTime(totalSeconds);
                }
            }

            bool inPrep = _waveManager != null && GameManager.Instance.CurrentState == GameState.Preparation;
            if (_prepTimerPanel != null && _prepPanelVisible != inPrep)
            {
                _prepPanelVisible = inPrep;
                _prepTimerPanel.SetActive(inPrep);
            }

            int prepSeconds = inPrep ? Mathf.CeilToInt(_waveManager.PrepTimeRemaining) : -1;
            if (_prepTimerText != null && prepSeconds != _lastDisplayedPrepSeconds)
            {
                _lastDisplayedPrepSeconds = prepSeconds;
                _prepTimerText.text = inPrep ? prepSeconds.ToString() : "-";
            }
        }

        private void RefreshAll()
        {
            if (_runManager == null) return;
            var data = _runManager.RunData;

            SetText(_goldText, ETD.Core.NumberFormat.Compact(data.Gold));
            SetText(_livesText, $"{data.Lives}");
            SetText(_scoreText, ETD.Core.NumberFormat.Compact(data.Score));
            SetText(_waveText, $"{_waveManager?.CurrentWave ?? 0}");
            SetText(_levelText, $"Lv {data.Level}");
            UpdateXPBar(data.CurrentXP, data.XPToNextLevel);

          if(_traitIconContainer != null)
            {
                for (int i = 0; i < TraitManager.Instance.ActiveTraits.Count; i++)
                {
                    TraitData _traitData = TraitManager.Instance.ActiveTraits[i];
                    GameObject go = Instantiate(_traitIconPrefab, _traitIconContainer);
                    go.GetComponent<Image>().sprite = _traitData.Icon;
                    go.GetComponent<TooltipTrigger>().SetData(_traitData);
                }
            }
        }

      

        private void UpdateXPBar(float current, float required)
        {
            if (_xpBarFill != null)
                _xpBarFill.fillAmount = required > 0 ? current / required : 0f;
        }

        // === EVENT HANDLERS ===

        private void OnGoldChanged(GoldChangedEvent evt) => SetText(_goldText, ETD.Core.NumberFormat.Compact(evt.Current));
        private void OnLivesChanged(LivesChangedEvent evt) => SetText(_livesText, $"{evt.Current}");
        private void OnScoreChanged(ScoreChangedEvent evt) => SetText(_scoreText, ETD.Core.NumberFormat.Compact(evt.Current));
        private void OnWaveStarted(WaveStartedEvent evt) => SetText(_waveText, $"{evt.WaveNumber}");
        private void OnLevelUp(LevelUpEvent evt) => SetText(_levelText, $"Lv {evt.NewLevel}");
        private void OnMetaCurrencyChanged(MetaCurrencyChangedEvent evt) => SetText(_metaCurrencyText, ETD.Core.NumberFormat.Compact(evt.Current));

        private void OnXPChanged(XPGainedEvent evt)
        {
            UpdateXPBar(evt.CurrentXP, evt.RequiredXP);
        }

        private void OnPrepStarted(PrepPhaseStartedEvent evt)
        {
            if (_prepTimerPanel != null) _prepTimerPanel.SetActive(true);
        }

        private void OnSkipPrepClicked()
        {
            _runManager?.SkipPrepTime();
        }

        private void SetText(TMP_Text text, string value)
        {
            if (text != null) text.text = value;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Unsubscribe<LivesChangedEvent>(OnLivesChanged);
            EventBus.Unsubscribe<ScoreChangedEvent>(OnScoreChanged);
            EventBus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Unsubscribe<XPGainedEvent>(OnXPChanged);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<MetaCurrencyChangedEvent>(OnMetaCurrencyChanged);
            EventBus.Unsubscribe<PrepPhaseStartedEvent>(OnPrepStarted);
        }

      
    }
}
