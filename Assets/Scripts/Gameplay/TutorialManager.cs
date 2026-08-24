// ============================================================================
// ETD.Gameplay - TutorialManager.cs  [NEW]
// Step-by-step tutorial. Tracks progress via SaveData.TutorialCompleted.
// Steps auto-advance on game events OR manually via "Got It" button.
// Can be skipped entirely at any time.
// Add to [GameManagers] in Game scene. Hub tutorial is separate (HubTutorial.cs).
// ============================================================================
using System.Collections;
using UnityEngine;
using ETD.Core;

namespace ETD.Gameplay
{
    public enum TutorialStepId
    {
        Welcome = 0,
        BuildTurret,
        BuildMaze,
        PathChanges,
        SpeedUp,
        PrepSkip,
        WaveActive,
        DynamicTile,
        Upgrade,
        LevelUp,
        SpecCard,
        EvolveAvailable,
        TabStats,
        Complete
    }

    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private TutorialUI _ui;

        [Header("References")]
        [SerializeField] private PathIndicator _pathIndicator;

        [Header("Settings")]
        [Tooltip("Delay between auto-advance steps (seconds)")]
        [SerializeField] private float _autoAdvanceDelay = 3f;

        private TutorialStepId _currentStep = TutorialStepId.Welcome;
        private bool _isActive;
        private bool _tabPressedSinceTutorial;
        private int  _turretsPlaced;
        private bool _specCardChosen;
        private bool _turretUpgraded;
        private bool _turretEvolved;
        private bool _speedChanged;
        private bool _prepSkipped;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            var save = SaveSystem.Load();
            if (save.TutorialCompleted)
            {
                gameObject.SetActive(false);
                return;
            }

            _isActive = true;
            SubscribeEvents();
            StartStep(TutorialStepId.Welcome);
        }

        private void Update()
        {
            if (!_isActive) return;

            // Tab check for stats step
            if (_currentStep == TutorialStepId.TabStats
                && UnityEngine.Input.GetKeyDown(KeyCode.Tab))
            {
                _tabPressedSinceTutorial = true;
                AdvanceStep();
            }
        }

        // =================================================================
        // STEP LOGIC
        // =================================================================

        private void StartStep(TutorialStepId step)
        {
            _currentStep = step;

            switch (step)
            {
                case TutorialStepId.Welcome:
                    _ui.Show(
                        title:   "Welcome, Commander!",
                        body:    "Enemies follow the glowing path. Build turrets beside it to stop them! The path changes dynamically based on where you build.",
                        target:  null,
                        canSkip: true,
                        onNext:  () => { });
                    // Show path for 4 seconds
                    _pathIndicator?.SetAlwaysVisible(true);
                    StartCoroutine(AutoAdvance(4f, TutorialStepId.BuildTurret));
                    break;

                case TutorialStepId.BuildTurret:
                    _pathIndicator?.SetAlwaysVisible(false);
                    _ui.Show(
                        title: "Build Your First Turret",
                        body:  "Select a turret from the bottom bar, then click a tile next to the path to place it. Try building the Basic Turret!",
                        target: _ui.TurretBarTarget,
                        canSkip: true,
                        onNext: null); // Advances on event
                    break;

                case TutorialStepId.BuildMaze:
                    _ui.Show(
                        title: "Shape the Path!",
                        body:  "Place 2 more turrets to force enemies on a longer route. The longer the path, the more damage your turrets deal!",
                        target: null,
                        canSkip: true,
                        onNext: null);
                    break;

                case TutorialStepId.PathChanges:
                    _ui.Show(
                        title: "The Path Changed!",
                        body:  "See how enemies rerouted? This is maze building — the core strategy of the game. Experiment with different layouts!",
                        target: null,
                        canSkip: true,
                        onNext: () => { });
                    StartCoroutine(AutoAdvance(_autoAdvanceDelay, TutorialStepId.SpeedUp));
                    break;

                case TutorialStepId.SpeedUp:
                    _ui.Show(
                        title: "Speed Control",
                        body:  "Use the speed button (1x → 2x → 3x) to fast-forward through waves. Click it now to try!",
                        target: _ui.SpeedButtonTarget,
                        canSkip: true,
                        onNext: null);
                    break;

                case TutorialStepId.PrepSkip:
                    _ui.Show(
                        title: "Preparation Controls",
                        body:  "Between waves, pause the prep countdown to build without time pressure, resume when ready, or click 'Skip' to start immediately.",
                        target: _ui.SkipButtonTarget,
                        canSkip: true,
                        onNext: null);
                    break;

                case TutorialStepId.WaveActive:
                    _ui.Show(
                        title: "Enemies Incoming!",
                        body:  "Your turrets attack automatically. Select a turret to see its stats, upgrade it with Q, or sell it with E.",
                        target: null,
                        canSkip: true,
                        onNext: () => { });
                    StartCoroutine(AutoAdvance(_autoAdvanceDelay, TutorialStepId.Upgrade));
                    break;

                case TutorialStepId.Upgrade:
                    _ui.Show(
                        title: "Upgrade Your Turrets",
                        body:  "Click a turret to select it, then press Q to upgrade it. Upgrading increases damage, range, and attack speed!",
                        target: _ui.TurretInfoTarget,
                        canSkip: true,
                        onNext: null);
                    break;

                case TutorialStepId.DynamicTile:
                    _ui.Show(
                        title: "Special Tiles!",
                        body:  "Glowing tiles give unique bonuses to turrets placed on them — damage boosts, cost reductions, gold multipliers and more. Hover to see their effects!",
                        target: null,
                        canSkip: true,
                        onNext: () => { });
                    StartCoroutine(AutoAdvance(_autoAdvanceDelay, TutorialStepId.LevelUp));
                    break;

                case TutorialStepId.LevelUp:
                    _ui.Show(
                        title: "You Leveled Up!",
                        body:  "Choose a Spec Card to permanently boost all your turrets this run. Each level-up gives you 3 options — choose wisely!",
                        target: _ui.SpecCardTarget,
                        canSkip: true,
                        onNext: null);
                    break;

                case TutorialStepId.SpecCard:
                    // Auto-advances after card is picked (event-driven)
                    break;

                case TutorialStepId.EvolveAvailable:
                    _ui.Show(
                        title: "Evolution Available!",
                        body:  "This turret can EVOLVE! Choose Path A or Path B — each transforms it into a completely different weapon type. Press 1 or 2, or click the path button.",
                        target: _ui.EvolveTarget,
                        canSkip: true,
                        onNext: null);
                    break;

                case TutorialStepId.TabStats:
                    _ui.Show(
                        title: "Track Your Power",
                        body:  "Press TAB at any time to see all your accumulated Spec Card bonuses. Watch your stats grow as you level up!",
                        target: null,
                        canSkip: true,
                        onNext: null);
                    break;

                case TutorialStepId.Complete:
                    _ui.Show(
                        title: "You're Ready, Commander!",
                        body:  "That covers the basics. Keep building, upgrading, and evolving to survive endless waves. Good luck!",
                        target: null,
                        canSkip: false,
                        onNext: CompleteTutorial);
                    StartCoroutine(AutoAdvance(5f, (TutorialStepId)999)); // just calls complete
                    break;
            }
        }

        private void AdvanceStep()
        {
            TutorialStepId next = _currentStep + 1;
            if ((int)next > (int)TutorialStepId.Complete)
            {
                CompleteTutorial();
                return;
            }
            StartStep(next);
        }

        private IEnumerator AutoAdvance(float delay, TutorialStepId nextStep)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (_isActive && _currentStep != TutorialStepId.Complete)
                StartStep(nextStep);
        }

        // =================================================================
        // EVENT HANDLERS
        // =================================================================

        private void SubscribeEvents()
        {
            EventBus.Subscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Subscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Subscribe<TurretEvolvedEvent>(OnTurretEvolved);
            EventBus.Subscribe<SpecCardChosenEvent>(OnSpecCardChosen);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<ShowEvolveChoiceEvent>(OnEvolveChoice);
            EventBus.Subscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Subscribe<PrepPhaseStartedEvent>(OnPrepPhase);
            EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void OnTurretPlaced(TurretPlacedEvent evt)
        {
            _turretsPlaced++;

            if (_currentStep == TutorialStepId.BuildTurret && _turretsPlaced >= 1)
                StartStep(TutorialStepId.BuildMaze);
            else if (_currentStep == TutorialStepId.BuildMaze && _turretsPlaced >= 3)
                StartStep(TutorialStepId.PathChanges);
        }

        private void OnTurretUpgraded(TurretUpgradedEvent evt)
        {
            if (_currentStep == TutorialStepId.Upgrade && !_turretUpgraded)
            {
                _turretUpgraded = true;
                StartStep(TutorialStepId.DynamicTile);
            }
        }

        private void OnTurretEvolved(TurretEvolvedEvent evt)
        {
            if (_currentStep == TutorialStepId.EvolveAvailable && !_turretEvolved)
            {
                _turretEvolved = true;
                StartStep(TutorialStepId.TabStats);
            }
        }

        private void OnSpecCardChosen(SpecCardChosenEvent evt)
        {
            if (_currentStep == TutorialStepId.LevelUp)
            {
                _specCardChosen = true;
                StartStep(TutorialStepId.EvolveAvailable);
            }
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            // If past BuildMaze step, show level up tutorial
            if ((int)_currentStep >= (int)TutorialStepId.Upgrade
                && _currentStep != TutorialStepId.LevelUp
                && _currentStep != TutorialStepId.SpecCard
                && !_specCardChosen)
            {
                StartStep(TutorialStepId.LevelUp);
            }
        }

        private void OnEvolveChoice(ShowEvolveChoiceEvent evt)
        {
            if ((int)_currentStep >= (int)TutorialStepId.LevelUp && !_turretEvolved)
                StartStep(TutorialStepId.EvolveAvailable);
        }

        private void OnWaveStarted(WaveStartedEvent evt)
        {
            if (_currentStep == TutorialStepId.PrepSkip)
                StartStep(TutorialStepId.WaveActive);
        }

        private void OnPrepPhase(PrepPhaseStartedEvent evt)
        {
            if (_currentStep == TutorialStepId.SpeedUp && _speedChanged)
                StartStep(TutorialStepId.PrepSkip);
        }

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            // Detect speed change (timeScale != 1)
            if (_currentStep == TutorialStepId.SpeedUp && Time.timeScale > 1f)
            {
                _speedChanged = true;
                StartStep(TutorialStepId.PrepSkip);
            }
        }

        // =================================================================
        // COMPLETE / SKIP
        // =================================================================

        public void CompleteTutorial()
        {
            _isActive = false;
            _ui?.Hide();

            var save = SaveSystem.Load();
            save.TutorialCompleted = true;
            SaveSystem.Save(save);

            UnsubscribeEvents();
            gameObject.SetActive(false);
        }

        public void SkipTutorial() => CompleteTutorial();

        private void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Unsubscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Unsubscribe<TurretEvolvedEvent>(OnTurretEvolved);
            EventBus.Unsubscribe<SpecCardChosenEvent>(OnSpecCardChosen);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<ShowEvolveChoiceEvent>(OnEvolveChoice);
            EventBus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Unsubscribe<PrepPhaseStartedEvent>(OnPrepPhase);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void OnDestroy() => UnsubscribeEvents();
    }
}
