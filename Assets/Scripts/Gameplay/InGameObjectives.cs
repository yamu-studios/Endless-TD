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
// not a special-cased method), but it always stops ONE LEVEL SHORT of each
// evolution threshold — ETD.Core.TutorialGates.TurretLevelCap enforces that even
// against extra upgrade presses. Both evolutions are then player actions taken
// against a frozen board: Tier1 is the Path A/B click, and Tier2 is the player's
// own upgrade press crossing the threshold, so they see it happen.
// ============================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
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

        private int _currentObjective = 0;
        private bool _active = false;

        private int _turretsPlaced;
        private int _firstTurretId = -1;
        private TurretManager _turretManager;

        private static readonly string[] ObjectiveKeys =
        {
            "tutorial_objective_build_turret",
            "tutorial_objective_build_maze",
            "tutorial_objective_upgrade_turret",
            "tutorial_objective_evolve_choice",
            "tutorial_objective_evolve_tier2",
            "tutorial_objective_level_up_spec_card",
            "tutorial_objective_cast_spell",
            "tutorial_objective_tab_stats",
        };

        private static readonly string[] ObjectiveFallbacks =
        {
            "Build a turret",
            "Place 2 more turrets to build a maze",
            "Select your turret and press Q to upgrade it",
            "Your turret can evolve at level {0}! Choose a path",
            "It will automatically evolve again at level {0}!",
            "Defeat enemies, level up, and choose a Spec Card",
            "Press the spell button to cast your chosen spell",
            "Press Tab to see your Spec Card stats",
        };

        private const int StepBuildTurret = 0;
        private const int StepBuildMaze = 1;
        private const int StepUpgradeTurret = 2;
        private const int StepEvolveChoice = 3;
        private const int StepEvolveTier2 = 4;
        private const int StepLevelUpSpecCard = 5;
        private const int StepCastSpell = 6;
        private const int StepTabStats = 7;

        /// <summary>
        /// Gates real kill-XP level-ups during the tutorial so the player can't level
        /// up before the objective sequence actually reaches the LevelUp step. Always
        /// true outside tutorial mode (see RunManager.AddXP).
        /// </summary>
        public static bool TutorialLevelUpGateOpen { get; private set; } = true;

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

            // No dedicated End Tutorial button any more — Escape opens the menu, which
            // already has Return to Menu. Force it off in case it is left enabled in the
            // scene, so it cannot linger on screen.
            if (_endTutorialButtonObject != null) _endTutorialButtonObject.SetActive(false);

            ApplyTutorialUITexts();

            TutorialLevelUpGateOpen = false;

            // The tutorial teaches exactly one level-up (to level 2) and holds the turret
            // below each evolution threshold until the matching objective appears.
            TutorialGates.PlayerLevelCap = 2;
            TutorialGates.TurretLevelCap = int.MaxValue;   // raised/lowered per step below

            SubscribeEvents();
            _active = true;
            ShowObjective(0);
        }

        private void Update()
        {
            if (!_active) return;

            if (_currentObjective == StepTabStats && UnityEngine.Input.GetKeyDown(KeyCode.Tab))
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
                _objectiveText.text = BuildObjectiveText(index);

            if (index == StepLevelUpSpecCard)
                TutorialLevelUpGateOpen = true;

            // Count-based steps can already be satisfied by the time we enter them —
            // e.g. a fast player placing 3 turrets within the ~1.7s completion-animation
            // window of step 0 would otherwise leave step 1 waiting forever for a 4th
            // TurretPlacedEvent that never comes. Re-check on entry, not just on event.
            if (index == StepBuildTurret && _turretsPlaced >= 1)
                CompleteCurrentObjective();
            else if (index == StepBuildMaze && _turretsPlaced >= 3)
                CompleteCurrentObjective();
            else if (index == StepEvolveChoice)
            {
                var t = ResolveFirstTurret();
                if (t != null && t.IsEvolved)
                    CompleteCurrentObjective();       // safety: already evolved somehow
                else
                    StartCoroutine(RunForcedTier1Evolution());
            }
            else if (index == StepEvolveTier2)
            {
                var t = ResolveFirstTurret();
                if (t != null && t.IsEvolvedTier2)
                    CompleteCurrentObjective();       // safety: already there somehow
                else
                    StartCoroutine(RunForcedTier2Evolution());
            }
        }

        [Header("Tier 2 Evolution Step")]
        [Tooltip("Extra pause after the Tier 1 path choice before the Tier 2 objective "
               + "appears, so the two evolutions do not blur into one another.")]
        [SerializeField, Min(0f)] private float _tier2LeadInSeconds = 3f;

        [Tooltip("Pause after the Tier 2 objective appears before the turret starts "
               + "levelling, giving the player time to read it and look at the turret.")]
        [SerializeField, Min(0f)] private float _tier2ReadSeconds = 1.5f;

        [Tooltip("Delay between each scripted upgrade on the way to Tier 2, so the "
               + "level-ups and the final transformation are watchable rather than instant.")]
        [SerializeField, Min(0f)] private float _tier2UpgradeStepSeconds = 0.25f;

        /// <summary>
        /// Tier 1: raise the cap so the turret can cross its evolve threshold, push it
        /// there, then FREEZE the board while the choice panel is open. The player must
        /// pick a path to continue — OnTurretEvolved unfreezes and advances.
        /// </summary>
        private IEnumerator RunForcedTier1Evolution()
        {
            yield return new WaitForSecondsRealtime(_tier2ReadSeconds);

            var turret = ResolveFirstTurret();
            if (turret == null || turret.Data == null) yield break;

            // Selecting shows the info panel and range ring, so it is obvious which
            // turret the objective is talking about.
            EventBus.Publish(new TurretSelectedEvent { TurretId = turret.InstanceId });

            // Open the gate by exactly one level: this crossing publishes
            // ShowEvolveChoiceEvent, which opens the path-choice panel.
            TutorialGates.TurretLevelCap = turret.Data.EvolveLevel;
            if (turret.Level < turret.Data.EvolveLevel)
                turret.Upgrade();

            // Freeze only after the panel is up, so the player is choosing against a
            // still board rather than a live wave.
            SetTutorialFreeze(true);
        }

        /// <summary>
        /// Tier 2: park the turret one level below its Tier 2 threshold, freeze, and let
        /// the PLAYER press upgrade for the final level so they see the transformation
        /// happen as a result of their own click.
        /// </summary>
        private IEnumerator RunForcedTier2Evolution()
        {
            yield return new WaitForSecondsRealtime(_tier2ReadSeconds);

            var turret = ResolveFirstTurret();
            if (turret == null || turret.Data == null) yield break;

            // Silently close the gap to one level short of Tier 2, so the player's own
            // upgrade press is the one that triggers it.
            TutorialGates.TurretLevelCap = Mathf.Max(1, turret.Data.EvolveLevel2 - 1);
            int guard = 0;
            while (turret != null && turret.Data != null
                   && turret.Level < TutorialGates.TurretLevelCap
                   && !turret.IsEvolvedTier2
                   && ++guard < 500)
            {
                turret.Upgrade();
                // Re-resolve: an evolution swaps in a new prefab under the same InstanceId.
                turret = ResolveFirstTurret();
                if (_tier2UpgradeStepSeconds > 0f)
                    yield return new WaitForSecondsRealtime(_tier2UpgradeStepSeconds);
            }

            turret = ResolveFirstTurret();
            if (turret == null || turret.Data == null) yield break;

            EventBus.Publish(new TurretSelectedEvent { TurretId = turret.InstanceId });

            // Allow exactly the one upgrade that crosses into Tier 2, then freeze and
            // wait for the player to press it themselves.
            TutorialGates.TurretLevelCap = turret.Data.EvolveLevel2;
            SetTutorialFreeze(true);
        }

        /// <summary>
        /// Freezes/unfreezes the board for a forced tutorial step. Uses Time.timeScale
        /// directly rather than GameManager.PushModalState(GameState.Paused), because
        /// SetState publishes GamePausedEvent and PauseMenuUI would open the pause menu
        /// on top of the step.
        /// </summary>
        private void SetTutorialFreeze(bool frozen)
        {
            Time.timeScale = frozen ? 0f : 1f;
        }

        /// <summary>
        /// Evolve objective text includes the turret's actual evolution level
        /// instead of generic wording.
        /// </summary>
        private string BuildObjectiveText(int index)
        {
            if (index == StepEvolveChoice || index == StepEvolveTier2)
            {
                var turret = ResolveFirstTurret();
                int level = index == StepEvolveChoice
                    ? (turret != null && turret.Data != null ? turret.Data.EvolveLevel : 0)
                    : (turret != null && turret.Data != null ? turret.Data.EvolveLevel2 : 0);
                return LocalizationManager.GetFormat(ObjectiveKeys[index], ObjectiveFallbacks[index], level);
            }
            return LocalizationManager.Get(ObjectiveKeys[index], ObjectiveFallbacks[index]);
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

            // Breathing room before the Tier 2 step specifically, so it does not land on
            // top of the Tier 1 path choice the player just made.
            if (_currentObjective + 1 == StepEvolveTier2 && _tier2LeadInSeconds > 0f)
                yield return new WaitForSecondsRealtime(_tier2LeadInSeconds);

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
            if (!_active || _currentObjective != StepUpgradeTurret) return;

            // Accept a Q-press on ANY placed turret, not just the first one built.
            // Whichever turret the player actually upgrades becomes the tracked
            // turret for the rest of the flow (evolve steps below).
            _firstTurretId = evt.TurretId;

            // Player pressed Q for real once — script-jump the rest of the way to
            // this turret's Tier1 evolve threshold using the same public Upgrade()
            // a real click would call (it doesn't check gold; the UI caller does,
            // and this session has infinite gold anyway).
            // Level to ONE BELOW the Tier1 threshold and park there. Grinding all the
            // way to EvolveLevel here used to open the evolve panel during this step, so
            // the player could pick a path before the evolve objective appeared — and
            // then that objective waited forever for an event that had already fired.
            // The cap also blocks any extra upgrade presses from crossing the line.
            var turret = ResolveFirstTurret();
            if (turret != null && turret.Data != null)
            {
                TutorialGates.TurretLevelCap = Mathf.Max(1, turret.Data.EvolveLevel - 1);
                while (turret.Level < TutorialGates.TurretLevelCap && !turret.IsEvolved)
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
                // Player picked a path: unfreeze and move on. The Tier2 grind is NOT done
                // here — it happens in RunForcedTier2Evolution once that objective is up,
                // so the player actually sees the second evolution.
                SetTutorialFreeze(false);
                CompleteCurrentObjective();
            }
            else if (_currentObjective == StepEvolveTier2 && turret.IsEvolvedTier2)
            {
                // Player pressed upgrade themselves and crossed into Tier 2.
                SetTutorialFreeze(false);
                CompleteCurrentObjective();
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

            // Exactly one level-up in the tutorial: the gate opened when this objective
            // appeared, and closes again the moment the card is picked. Later steps
            // (cast spell, tab stats) then run without further level-up interruptions.
            TutorialLevelUpGateOpen = false;

            CompleteCurrentObjective();
        }

        private void OnSpellCast(SpellCastEvent evt)
        {
            if (!_active || _currentObjective != StepCastSpell) return;
            CompleteCurrentObjective();
        }

        // =================================================================
        // COMPLETE TUTORIAL / EXIT
        // =================================================================

        [Header("Completion")]
        [Tooltip("Seconds to keep playing after the last objective before the " +
                 "'Tutorial Complete' panel appears, so the run does not stop dead the " +
                 "instant the final box is ticked.")]
        [SerializeField, Min(0f)] private float _completionPanelDelaySeconds = 5f;

        private void CompleteTutorial()
        {
            _active = false;
            UnsubscribeEvents();
            TutorialLevelUpGateOpen = true;
            // Restore unrestricted play — these gates are static and shared with real runs.
            TutorialGates.ResetToDefaults();

            if (_panel != null) _panel.SetActive(false);
            if (_endTutorialButtonObject != null) _endTutorialButtonObject.SetActive(false);

            // Let the player keep playing for a beat before the panel interrupts them.
            StartCoroutine(ShowCompletionPanelAfterDelay());
        }

        private IEnumerator ShowCompletionPanelAfterDelay()
        {
            if (_completionPanelDelaySeconds > 0f)
            {
                // Realtime: the delay should be wall-clock even if something else
                // pauses or slows the game while it is counting down.
                yield return new WaitForSecondsRealtime(_completionPanelDelaySeconds);
            }

            // Freeze the board behind the panel. Deliberately NOT
            // GameManager.PushModalState(GameState.Paused): SetState publishes
            // GamePausedEvent, which makes PauseMenuUI open the pause menu — that would
            // appear alongside this panel. Stopping time is all this needs; the panel
            // brings its own full-screen dimmer, and its only action is Return to Menu,
            // whose LoadHub() restores timeScale on its way out.
            Time.timeScale = 0f;

            ShowCompletionPanel();
        }

        public void ExitTutorial()
        {
            _active = false;
            UnsubscribeEvents();
            TutorialLevelUpGateOpen = true;
            TutorialGates.ResetToDefaults();
            // A forced evolve step may have frozen the board; never leave it at 0.
            Time.timeScale = 1f;
            GameManager.Instance?.LoadHub();
        }

        [Header("Tutorial UI (authored in the Game scene)")]
        [Tooltip("Legacy 'Tutorial End Button' object. No longer shown — Escape opens "
               + "the menu, which already has Return to Menu. Kept only so the object "
               + "can be force-hidden if it is still enabled in the scene.")]
        [SerializeField] private GameObject _endTutorialButtonObject;

        [Tooltip("The 'Tutorial Complete UI' object under Canvas_GameHUD.")]
        [SerializeField] private GameObject _completePanelObject;
        [SerializeField] private TMP_Text _completeTitleText;
        [SerializeField] private TMP_Text _completeBodyText;
        [SerializeField] private Button _completeReturnButton;
        [SerializeField] private TMP_Text _completeReturnLabel;

        /// <summary>
        /// Wires and shows the authored End Tutorial button. These panels live in the
        /// scene (rather than being built at runtime as they used to be) so they can be
        /// art-directed normally; this only drives visibility, text and the callback.
        /// </summary>

        /// <summary>
        /// Shows the authored completion panel. Called after the post-objective delay,
        /// with the board already frozen by the caller.
        /// </summary>
        private void ShowCompletionPanel()
        {
            if (_completePanelObject == null)
            {
                Debug.LogWarning("[Tutorial] Tutorial Complete panel is not assigned.");
                return;
            }

            if (_completeReturnButton != null)
            {
                _completeReturnButton.onClick.RemoveListener(ReturnToHubFromCompletion);
                _completeReturnButton.onClick.AddListener(ReturnToHubFromCompletion);
            }

            _completePanelObject.SetActive(true);
            _completePanelObject.transform.SetAsLastSibling();   // draw above the rest of the HUD
            ApplyTutorialUITexts();
        }

        private void ReturnToHubFromCompletion()
        {
            // ShowCompletionPanelAfterDelay froze time; LoadHub sets its own timeScale,
            // but restore here too so nothing can leave the game stuck at 0.
            Time.timeScale = 1f;
            GameManager.Instance?.LoadHub();
        }

        /// <summary>
        /// Pushes localized strings into both authored panels. Called on show and again
        /// on LanguageChangedEvent, so switching language updates them live.
        /// </summary>
        private void ApplyTutorialUITexts()
        {
            if (_completeTitleText != null)
                _completeTitleText.text =
                    LocalizationManager.Get("tutorial_complete_title", "Tutorial Complete!");

            if (_completeBodyText != null)
                _completeBodyText.text =
                    LocalizationManager.Get("tutorial_complete_body", "You have learned the basics!");

            if (_completeReturnLabel != null)
                _completeReturnLabel.text =
                    LocalizationManager.Get("tutorial_complete_return", "Return to Menu");
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
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            if (_active && _currentObjective >= 0 && _currentObjective < ObjectiveKeys.Length && _objectiveText != null)
                _objectiveText.text = BuildObjectiveText(_currentObjective);
            // Keeps both authored panels in sync when the player switches language
            // mid-tutorial, including while the completion panel is already open.
            ApplyTutorialUITexts();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }
    }
}
