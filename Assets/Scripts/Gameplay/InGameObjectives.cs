using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Waves;
using ETD.Gameplay;

namespace ETD.Gameplay
{
    public class InGameObjectives : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _objectiveText;
        [SerializeField] private Image _objectiveIcon;
        [SerializeField] private GameObject _completeIndicator;
        [SerializeField] private Sprite _tabKeyIcon;

        [Header("Timing")]
        [SerializeField] private float _completeShowDuration = 1.2f;
        [SerializeField] private float _nextObjectiveDelay = 0.5f;

        [Header("References")]
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private float _skipGracePeriod = 2f;

        private int _currentObjective = 0;
        private bool _active = false;
        private bool _skipGraceTriggered = false;

        private static readonly string[] ObjectiveKeys =
        {
            "tutorial_objective_build_turret",
            "tutorial_objective_level_up",
            "tutorial_objective_choose_spec_card",
            "tutorial_objective_tab_stats",
            "tutorial_objective_skip_prep"
        };

        private static readonly string[] ObjectiveFallbacks =
        {
            "Build a turret",
            "Defeat enemies and level up",
            "Choose a Spec Card",
            "Press Tab to see your Spec Card stats",
            "Skip preparation time to earn gold"
        };

        private void Awake()
        {
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void Start()
        {
            var save = SaveSystem.Load();
            if (save.TutorialCompleted) { gameObject.SetActive(false); return; }

            if (_completeIndicator != null) _completeIndicator.SetActive(false);

            SubscribeEvents();
            _active = true;
            ShowObjective(0);
        }

        private void Update()
        {
            if (!_active) return;
            if (_currentObjective == 3 && UnityEngine.Input.GetKeyDown(KeyCode.Tab))
                CompleteCurrentObjective();
        }

        // =================================================================
        // SHOW OBJECTIVE
        // =================================================================

        private void ShowObjective(int index)
        {
            _currentObjective = index;

            if (index >= ObjectiveKeys.Length) { CompleteTutorial(); return; }

            if (_panel != null) _panel.SetActive(true);
            if (_completeIndicator != null) _completeIndicator.SetActive(false);

            bool isTabStep = index == 3;
            if (_objectiveIcon != null)
            {
                _objectiveIcon.gameObject.SetActive(isTabStep && _tabKeyIcon != null);
                if (isTabStep && _tabKeyIcon != null) _objectiveIcon.sprite = _tabKeyIcon;
            }

            if (_objectiveText != null)
                _objectiveText.text = LocalizationManager.Get(ObjectiveKeys[index], ObjectiveFallbacks[index]);

            if (index == 4)
                EventBus.Subscribe<PrepPhaseStartedEvent>(OnPrepPhaseStarted);
        }

        // =================================================================
        // COMPLETE — FIX: guard against inactive GO before StartCoroutine
        // =================================================================

        private void CompleteCurrentObjective()
        {
            // Guard: do nothing if inactive or already completing Debug.Log("ShowCompleteAndAdvance : ");
            if (!_active) return;
            if (!gameObject.activeInHierarchy) return;
            _active = false;
            StartCoroutine(ShowCompleteAndAdvance());
        }

        private IEnumerator ShowCompleteAndAdvance()
        {
            if (_completeIndicator != null) _completeIndicator.SetActive(true);
            if (_objectiveText != null)
            {
                string current = _objectiveText.text;
                //if (!current.EndsWith("  ✓")) _objectiveText.text = current + "  ✓";
            }

            yield return new WaitForSecondsRealtime(_completeShowDuration);
            yield return new WaitForSecondsRealtime(_nextObjectiveDelay);

            _active = true;
            ShowObjective(_currentObjective + 1);
        }

        // =================================================================
        // EVENT HANDLERS — all guard _active first
        // =================================================================

        private void OnTurretPlaced(TurretPlacedEvent evt)
        {
            if (!_active || _currentObjective != 0) return;
            CompleteCurrentObjective();
        }

        private void OnLevelUp(LevelUpEvent evt)
        {

            if (!_active || _currentObjective != 1) return;
            CompleteCurrentObjective();
        }

        private void OnSpecCardChosen(SpecCardChosenEvent evt)
        {
            if (!_active || _currentObjective != 2) return;
            CompleteCurrentObjective();
        }

        private void OnPrepPhaseStarted(PrepPhaseStartedEvent evt)
        {
            _skipGraceTriggered = false;
            StartCoroutine(MonitorSkipPrep());
        }

        private IEnumerator MonitorSkipPrep()
        {
            while (_active && _currentObjective == 4 && _waveManager != null)
            {
                float remaining = _waveManager.PrepTimeRemaining;

                if (remaining <= _skipGracePeriod && remaining > 0f && !_skipGraceTriggered)
                {
                    _skipGraceTriggered = true;
                    float fullReward = GameConstants.BASE_PREP_TIME
                                     * GameConstants.SKIP_REWARD_GOLD_PER_SECOND;
                    if (ServiceLocator.TryGet<RunManager>(out var rm))
                        rm.AddGold(Mathf.RoundToInt(fullReward));

                    EventBus.Unsubscribe<PrepPhaseStartedEvent>(OnPrepPhaseStarted);
                    CompleteCurrentObjective();
                    yield break;
                }

                yield return null;
            }
        }

        public void OnSkipButtonPressed()
        {
            if (!_active || _currentObjective != 4 || _skipGraceTriggered) return;
            _skipGraceTriggered = true;
            EventBus.Unsubscribe<PrepPhaseStartedEvent>(OnPrepPhaseStarted);
            CompleteCurrentObjective();
        }

        // =================================================================
        // COMPLETE TUTORIAL
        // =================================================================

        private void CompleteTutorial()
        {
            _active = false;
            UnsubscribeEvents();

            if (_panel != null) _panel.SetActive(false);

            var save = SaveSystem.Load();
            save.TutorialCompleted = true;
            SaveSystem.Save(save);

            gameObject.SetActive(false);
        }

        private void SubscribeEvents()
        {
            EventBus.Subscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<SpecCardChosenEvent>(OnSpecCardChosen);
        }

        private void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<SpecCardChosenEvent>(OnSpecCardChosen);
            EventBus.Unsubscribe<PrepPhaseStartedEvent>(OnPrepPhaseStarted);
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            if (_active && _currentObjective >= 0 && _currentObjective < ObjectiveKeys.Length && _objectiveText != null)
                _objectiveText.text = LocalizationManager.Get(ObjectiveKeys[_currentObjective], ObjectiveFallbacks[_currentObjective]);
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }
    }
}
