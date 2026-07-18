// ============================================================================
// ETD.Gameplay - InGameObjectives.cs  [REWRITTEN v1.0 tutorial redesign]
// Corner objective checklist driving the sandboxed practice tutorial (see
// [[etd-v1-full-release]]). Gated on GameManager.IsTutorialMode instead of the
// old "!save.TutorialCompleted" auto-trigger — this component only ever runs
// during a dedicated tutorial session now, launched from the Hub.
//
// Turret leveling toward evolution thresholds is scripted (repeated calls to
// the same public TurretController.Upgrade() a real Upgrade-button click would
// use — it doesn't check gold itself, the caller does, so calling it directly
// here is a legitimate "free upgrade" in this sandboxed, infinite-gold run,
// not a special-cased method). Evolution CHOICES are still made by the player
// for real (Path A/B click, Tier2 confirm click) — only the level-grinding is
// skipped.
// ============================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Waves;
using ETD.Gameplay;
using ETD.Turrets;

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
        [SerializeField] private float _dynamicTileShowDuration = 4f;
        [SerializeField] private float _returnToHubDelay = 2.5f;

        [Header("References")]
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private float _skipGracePeriod = 2f;

        private int _currentObjective = 0;
        private bool _active = false;
        private bool _skipGraceTriggered = false;

        private int _turretsPlaced;
        private int _firstTurretId = -1;
        private TurretManager _turretManager;

        private static readonly string[] ObjectiveKeys =
        {
            "tutorial_objective_build_turret",
            "tutorial_objective_build_maze",
            "tutorial_objective_speed_control",
            "tutorial_objective_skip_prep",
            "tutorial_objective_upgrade_turret",
            "tutorial_objective_evolve_choice",
            "tutorial_objective_evolve_tier2",
            "tutorial_objective_level_up_spec_card",
            "tutorial_objective_dynamic_tiles",
            "tutorial_objective_cast_spell",
            "tutorial_objective_tab_stats",
        };

        private static readonly string[] ObjectiveFallbacks =
        {
            "Build a turret",
            "Place 2 more turrets to build a maze",
            "Try the speed control to fast-forward",
            "Skip preparation time to earn gold",
            "Select your turret and press Q to upgrade it",
            "Your turret can evolve! Choose a path",
            "It can evolve again! Confirm the upgrade",
            "Defeat enemies, level up, and choose a Spec Card",
            "Glowing tiles give turrets special bonuses",
            "Press the spell button to cast your chosen spell",
            "Press Tab to see your Spec Card stats",
        };

        private const int StepBuildTurret = 0;
        private const int StepBuildMaze = 1;
        private const int StepSpeedControl = 2;
        private const int StepSkipPrep = 3;
        private const int StepUpgradeTurret = 4;
        private const int StepEvolveChoice = 5;
        private const int StepEvolveTier2 = 6;
        private const int StepLevelUpSpecCard = 7;
        private const int StepDynamicTiles = 8;
        private const int StepCastSpell = 9;
        private const int StepTabStats = 10;

        private void Awake()
        {
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void Start()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsTutorialMode)
            {
                gameObject.SetActive(false);
                return;
            }

            ServiceLocator.TryGet(out _turretManager);

            if (_completeIndicator != null) _completeIndicator.SetActive(false);
            EnsureExitButton();

            SubscribeEvents();
            _active = true;
            ShowObjective(0);
        }

        private void Update()
        {
            if (!_active) return;

            if (_currentObjective == StepTabStats && UnityEngine.Input.GetKeyDown(KeyCode.Tab))
                CompleteCurrentObjective();

            if (_currentObjective == StepSpeedControl && Time.timeScale > 1f)
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

            bool isTabStep = index == StepTabStats;
            if (_objectiveIcon != null)
            {
                _objectiveIcon.gameObject.SetActive(isTabStep && _tabKeyIcon != null);
                if (isTabStep && _tabKeyIcon != null) _objectiveIcon.sprite = _tabKeyIcon;
            }

            if (_objectiveText != null)
                _objectiveText.text = LocalizationManager.Get(ObjectiveKeys[index], ObjectiveFallbacks[index]);

            if (index == StepSkipPrep)
                EventBus.Subscribe<PrepPhaseStartedEvent>(OnPrepPhaseStarted);

            if (index == StepDynamicTiles)
                StartCoroutine(AutoAdvanceAfter(_dynamicTileShowDuration));

            // Count-based steps can already be satisfied by the time we enter them —
            // e.g. a fast player placing 3 turrets within the ~1.7s completion-animation
            // window of step 0 would otherwise leave step 1 waiting forever for a 4th
            // TurretPlacedEvent that never comes. Re-check on entry, not just on event.
            if (index == StepBuildTurret && _turretsPlaced >= 1)
                CompleteCurrentObjective();
            else if (index == StepBuildMaze && _turretsPlaced >= 3)
                CompleteCurrentObjective();
        }

        private IEnumerator AutoAdvanceAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (_active && _currentObjective == StepDynamicTiles)
                CompleteCurrentObjective();
        }

        // =================================================================
        // COMPLETE — guard against inactive GO before StartCoroutine
        // =================================================================

        private void CompleteCurrentObjective()
        {
            if (!_active) return;
            if (!gameObject.activeInHierarchy) return;
            _active = false;
            StartCoroutine(ShowCompleteAndAdvance());
        }

        private IEnumerator ShowCompleteAndAdvance()
        {
            if (_completeIndicator != null) _completeIndicator.SetActive(true);

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
            _turretsPlaced++;

            if (_firstTurretId < 0)
                _firstTurretId = evt.TurretId;

            if (!_active) return;

            if (_currentObjective == StepBuildTurret && _turretsPlaced >= 1)
                CompleteCurrentObjective();
            else if (_currentObjective == StepBuildMaze && _turretsPlaced >= 3)
                CompleteCurrentObjective();
        }

        private void OnTurretUpgraded(TurretUpgradedEvent evt)
        {
            if (!_active || _currentObjective != StepUpgradeTurret || evt.TurretId != _firstTurretId) return;

            // Player pressed Q for real once — script-jump the rest of the way to
            // this turret's Tier1 evolve threshold using the same public Upgrade()
            // a real click would call (it doesn't check gold; the UI caller does,
            // and this session has infinite gold anyway).
            var turret = ResolveFirstTurret();
            if (turret != null && turret.Data != null)
            {
                while (turret.Level < turret.Data.EvolveLevel && !turret.IsEvolved)
                    turret.Upgrade();
            }

            CompleteCurrentObjective();
        }

        private void OnTurretEvolved(TurretEvolvedEvent evt)
        {
            if (!_active || evt.TurretId != _firstTurretId) return;

            var turret = ResolveFirstTurret();
            if (turret == null) return;

            if (_currentObjective == StepEvolveChoice)
            {
                // Tier1 just resolved (this event fires from inside TurretController.Evolve(),
                // which runs BEFORE the player's click handler calls GameManager.PopModalState()).
                // Jumping to the Tier2 threshold synchronously here would push a second
                // EvolveChoice modal before the first one is popped, corrupting
                // GameManager's _stateBeforeModal (it would capture "EvolveChoice" itself
                // as the state to restore to, freezing the game at Time.timeScale=0
                // forever). Defer the jump until the modal actually clears.
                StartCoroutine(JumpToTier2AfterModalClears());
                CompleteCurrentObjective();
            }
            else if (_currentObjective == StepEvolveTier2 && turret.IsEvolvedTier2)
            {
                CompleteCurrentObjective();
            }
        }

        private IEnumerator JumpToTier2AfterModalClears()
        {
            yield return new WaitUntil(() =>
                GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.EvolveChoice);
            yield return null; // one extra frame of safety margin

            var turret = ResolveFirstTurret();
            if (turret != null && turret.Data != null)
            {
                while (turret.Level < turret.Data.EvolveLevel2 && !turret.IsEvolvedTier2)
                    turret.Upgrade();
            }
        }

        private TurretController ResolveFirstTurret()
        {
            if (_firstTurretId < 0) return null;
            if (_turretManager == null) ServiceLocator.TryGet(out _turretManager);
            return _turretManager != null ? _turretManager.GetTurret(_firstTurretId) : null;
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            if (!_active || _currentObjective != StepLevelUpSpecCard) return;
            // Wait for the actual spec-card pick, not just the level-up itself —
            // OnSpecCardChosen below completes this step.
        }

        private void OnSpecCardChosen(SpecCardChosenEvent evt)
        {
            if (!_active || _currentObjective != StepLevelUpSpecCard) return;
            CompleteCurrentObjective();
        }

        private void OnSpellCast(SpellCastEvent evt)
        {
            if (!_active || _currentObjective != StepCastSpell) return;
            CompleteCurrentObjective();
        }

        private void OnPrepPhaseStarted(PrepPhaseStartedEvent evt)
        {
            _skipGraceTriggered = false;
            StartCoroutine(MonitorSkipPrep());
        }

        private IEnumerator MonitorSkipPrep()
        {
            while (_active && _currentObjective == StepSkipPrep && _waveManager != null)
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
            if (!_active || _currentObjective != StepSkipPrep || _skipGraceTriggered) return;
            _skipGraceTriggered = true;
            EventBus.Unsubscribe<PrepPhaseStartedEvent>(OnPrepPhaseStarted);
            CompleteCurrentObjective();
        }

        // =================================================================
        // COMPLETE TUTORIAL / EXIT
        // =================================================================

        private void CompleteTutorial()
        {
            _active = false;
            UnsubscribeEvents();

            if (_objectiveText != null)
                _objectiveText.text = LocalizationManager.Get("tutorial_complete_message", "Great work! Returning to Hub...");
            if (_completeIndicator != null) _completeIndicator.SetActive(true);

            StartCoroutine(ReturnToHubAfterDelay());
        }

        public void ExitTutorial()
        {
            _active = false;
            UnsubscribeEvents();
            GameManager.Instance?.LoadHub();
        }

        /// <summary>
        /// Small runtime "X" close button in the panel's corner, so the player can
        /// bail out of the practice run early. Built inline rather than via
        /// ETD.UI.PanelCloseButton — that class lives in the ETD.UI assembly, which
        /// already references ETD.Gameplay, so referencing it back here would be a
        /// circular assembly dependency.
        /// </summary>
        private void EnsureExitButton()
        {
            if (_panel == null) return;

            const string buttonName = "[ExitTutorialButton]";
            if (_panel.transform.Find(buttonName) != null) return;

            var buttonGO = new GameObject(buttonName, typeof(RectTransform));
            buttonGO.transform.SetParent(_panel.transform, false);
            buttonGO.layer = _panel.layer;

            var rect = (RectTransform)buttonGO.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(26f, 26f);
            rect.anchoredPosition = new Vector2(-4f, -4f);

            var background = buttonGO.AddComponent<Image>();
            background.color = new Color(0.16f, 0.16f, 0.2f, 0.9f);

            var button = buttonGO.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.75f, 0.22f, 0.22f, 1f);
            colors.pressedColor = new Color(0.55f, 0.12f, 0.12f, 1f);
            button.colors = colors;
            button.onClick.AddListener(ExitTutorial);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(buttonGO.transform, false);
            labelGO.layer = _panel.layer;

            var labelRect = (RectTransform)labelGO.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = "X";
            label.fontSize = 15f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.92f, 0.92f, 0.95f, 1f);
            label.raycastTarget = false;

            buttonGO.transform.SetAsLastSibling();
        }

        private IEnumerator ReturnToHubAfterDelay()
        {
            yield return new WaitForSecondsRealtime(_returnToHubDelay);
            GameManager.Instance?.LoadHub();
        }

        private void SubscribeEvents()
        {
            EventBus.Subscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Subscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Subscribe<TurretEvolvedEvent>(OnTurretEvolved);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<SpecCardChosenEvent>(OnSpecCardChosen);
            EventBus.Subscribe<SpellCastEvent>(OnSpellCast);
        }

        private void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Unsubscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Unsubscribe<TurretEvolvedEvent>(OnTurretEvolved);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<SpecCardChosenEvent>(OnSpecCardChosen);
            EventBus.Unsubscribe<SpellCastEvent>(OnSpellCast);
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
