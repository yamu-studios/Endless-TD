// ============================================================================
// ETD.Debugging - NaturalSoakTestController.cs
// Developer-only fast natural run tool.
//
// Purpose:
//   Reproduce long-run accumulation issues without manually playing for hours.
//   Unlike direct wave hotkeys, this still executes normal wave lifecycle events:
//   prep -> wave start -> spawning/combat -> wave complete -> rewards/modals -> next wave.
//
// Setup:
//   Add this component to an always-active Game scene object, for example GameManagers.
//   This tool is inert in non-development builds unless explicitly enabled in the Inspector.
// ============================================================================
using UnityEngine;
using ETD.Core;
using ETD.Enemies;
using ETD.Gameplay;
using ETD.Turrets;
using ETD.Waves;

namespace ETD.Debugging
{
    [DefaultExecutionOrder(5000)]
    public sealed class NaturalSoakTestController : MonoBehaviour
    {
        public enum SoakMode
        {
            NaturalFastForward = 0,
            WaveLifecycleGrinder = 1
        }

        [Header("Runtime")]
        [SerializeField] private bool _autoStartOnPlay;
        [SerializeField] private bool _enableHotkeys = true;
        [SerializeField] private KeyCode _startKey = KeyCode.F8;
        [SerializeField] private KeyCode _stopKey = KeyCode.F9;
        [SerializeField] private KeyCode _toggleModeKey = KeyCode.F10;
        [SerializeField] private KeyCode _logSnapshotKey = KeyCode.F6;

        [Header("Soak Test")]
        [SerializeField] private SoakMode _mode = SoakMode.NaturalFastForward;
        [SerializeField, Range(1f, 25f)] private float _timeScale = 10f;
        [SerializeField, Min(1)] private int _targetWave = 80;
        [SerializeField] private bool _pauseAtTargetWave = true;
        [SerializeField] private bool _skipPrepAutomatically = true;
        [SerializeField] private bool _grantHugeLivesOnStart = true;
        [SerializeField, Min(1)] private int _soakLives = 999999;
        [SerializeField] private bool _grantGoldOnStart;
        [SerializeField, Min(0)] private int _extraGoldOnStart = 250000;

        [Header("Modal Automation")]
        [SerializeField] private bool _autoPickSpecCards = true;
        [SerializeField, Range(0, 2)] private int _specCardIndex = 0;
        [SerializeField] private bool _autoEvolveTurrets = true;
        [SerializeField, Range(0, 1)] private int _evolutionPath = 0;
        [SerializeField] private float _modalAutoDelayUnscaled = 0.05f;

        [Header("Lifecycle Grinder")]
        [Tooltip("Only used in WaveLifecycleGrinder mode. It lets every wave run briefly, then stops spawning and clears active enemies so the next normal wave lifecycle can start.")]
        [SerializeField, Min(0.1f)] private float _grinderWaveActiveSeconds = 3f;

        [Header("Accumulation Logs")]
        [SerializeField] private bool _logOnStartStop = true;
        [SerializeField] private bool _logEveryNWaveStarts = true;
        [SerializeField, Min(1)] private int _logEveryNWaves = 5;
        [SerializeField] private bool _includeObjectCounts = true;
        [SerializeField] private bool _includeEventBusCounts = true;

        private RunManager _runManager;
        private WaveManager _waveManager;
        private EnemyManager _enemyManager;
        private TurretManager _turretManager;

        private bool _running;
        private int _pendingEvolveTurretId = -1;
        private int _lastSpecPickFrame = -1;
        private int _lastEvolveFrame = -1;
        private float _nextModalActionRealtime;
        private float _currentWaveStartRealtime;
        private float _previousTimeScale = 1f;

        public bool IsRunning => _running;
        public SoakMode CurrentMode => _mode;

        private void Awake()
        {
            ResolveServices();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
            EventBus.Subscribe<PrepPhaseStartedEvent>(OnPrepStarted);
            EventBus.Subscribe<ShowEvolveChoiceEvent>(OnShowEvolveChoice);
            EventBus.Subscribe<GameOverEvent>(OnGameOver);
        }

        private void Start()
        {
            ResolveServices();

            if (_autoStartOnPlay)
                StartSoakTest();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
            EventBus.Unsubscribe<PrepPhaseStartedEvent>(OnPrepStarted);
            EventBus.Unsubscribe<ShowEvolveChoiceEvent>(OnShowEvolveChoice);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);

            if (_running)
                StopSoakTest(false);
        }

        private void Update()
        {
            if (_enableHotkeys)
                HandleHotkeys();

            if (!_running)
                return;

            ResolveServices();
            DriveTimeScale();
            DrivePrepSkip();
            DriveModalAutomation();
            DriveLifecycleGrinder();
            CheckTargetWave();
        }

        private void HandleHotkeys()
        {
            if (UnityEngine.Input.GetKeyDown(_startKey))
                StartSoakTest();

            if (UnityEngine.Input.GetKeyDown(_stopKey))
                StopSoakTest(true);

            if (UnityEngine.Input.GetKeyDown(_toggleModeKey))
            {
                _mode = _mode == SoakMode.NaturalFastForward
                    ? SoakMode.WaveLifecycleGrinder
                    : SoakMode.NaturalFastForward;

                Debug.Log($"[SOAK] Mode changed to {_mode}.");
            }

            if (UnityEngine.Input.GetKeyDown(_logSnapshotKey))
                LogAccumulationSnapshot("manual");
        }

        [ContextMenu("Start Soak Test")]
        public void StartSoakTest()
        {
            ResolveServices();

            if (_running)
                return;

            _running = true;
            _pendingEvolveTurretId = -1;
            _lastSpecPickFrame = -1;
            _lastEvolveFrame = -1;
            _nextModalActionRealtime = Time.realtimeSinceStartup + _modalAutoDelayUnscaled;
            _currentWaveStartRealtime = Time.realtimeSinceStartup;
            _previousTimeScale = Time.timeScale;

            ApplyStartBonuses();
            DriveTimeScale();

            Debug.Log($"[SOAK] Started. mode={_mode}, targetWave={_targetWave}, speed={_timeScale:0.##}x, skipPrep={_skipPrepAutomatically}, autoSpec={_autoPickSpecCards}, autoEvolve={_autoEvolveTurrets}");

            if (_logOnStartStop)
                LogAccumulationSnapshot("start");
        }

        [ContextMenu("Stop Soak Test")]
        public void StopSoakTest()
        {
            StopSoakTest(true);
        }

        private void StopSoakTest(bool log)
        {
            if (!_running)
                return;

            _running = false;
            _pendingEvolveTurretId = -1;

            var state = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Preparation;
            if (state == GameState.Preparation || state == GameState.WaveActive || state == GameState.Hub)
                Time.timeScale = Mathf.Approximately(_previousTimeScale, 0f) ? 1f : _previousTimeScale;

            Debug.Log("[SOAK] Stopped.");

            if (log && _logOnStartStop)
                LogAccumulationSnapshot("stop");
        }

        private void ResolveServices()
        {
            if (_runManager == null)
                ServiceLocator.TryGet(out _runManager);
            if (_waveManager == null)
                ServiceLocator.TryGet(out _waveManager);
            if (_enemyManager == null)
                ServiceLocator.TryGet(out _enemyManager);
            if (_turretManager == null)
                ServiceLocator.TryGet(out _turretManager);
        }

        private void ApplyStartBonuses()
        {
            if (_runManager == null || _runManager.RunData == null)
                return;

            if (_grantHugeLivesOnStart)
            {
                _runManager.RunData.MaxLives = Mathf.Max(_runManager.RunData.MaxLives, _soakLives);
                _runManager.RunData.Lives = Mathf.Max(_runManager.RunData.Lives, _soakLives);
                EventBus.Publish(new LivesChangedEvent
                {
                    Current = _runManager.RunData.Lives,
                    Max = _runManager.RunData.MaxLives
                });
            }

            if (_grantGoldOnStart && _extraGoldOnStart > 0)
                _runManager.AddGold(_extraGoldOnStart);
        }

        private void DriveTimeScale()
        {
            var state = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Preparation;
            if (state == GameState.Preparation || state == GameState.WaveActive)
            {
                if (!Mathf.Approximately(Time.timeScale, _timeScale))
                    Time.timeScale = _timeScale;
            }
        }

        private void DrivePrepSkip()
        {
            if (!_skipPrepAutomatically || _runManager == null || GameManager.Instance == null)
                return;

            if (GameManager.Instance.CurrentState != GameState.Preparation)
                return;

            if (_waveManager != null && _waveManager.PrepTimeRemaining <= 0.02f)
                return;

            _runManager.SkipPrepTime();
        }

        private void DriveModalAutomation()
        {
            if (Time.realtimeSinceStartup < _nextModalActionRealtime)
                return;

            var state = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Preparation;

            // v1.0: leveling up no longer pauses the run (offers queue instead — see
            // [[etd-v1-full-release]] Phase 4), so drive this off the pending-offer
            // queue rather than GameState.LevelUp, which the soak test would
            // otherwise never observe.
            if (_runManager != null && _runManager.PendingOfferCount > 0 && _autoPickSpecCards)
            {
                TryAutoPickSpecCard();
                return;
            }

            if (state == GameState.EvolveChoice && _autoEvolveTurrets)
            {
                TryAutoEvolvePendingTurret();
            }
        }

        private void TryAutoPickSpecCard()
        {
            if (_lastSpecPickFrame == Time.frameCount)
                return;

            _lastSpecPickFrame = Time.frameCount;
            _nextModalActionRealtime = Time.realtimeSinceStartup + _modalAutoDelayUnscaled;

            int index = Mathf.Clamp(_specCardIndex, 0, 2);

            var options = _runManager != null ? _runManager.CurrentSpecOptions : null;
            if (options != null && options.Length > 0)
            {
                if (index >= options.Length || options[index] == null)
                {
                    int fallback = -1;
                    for (int i = 0; i < options.Length; i++)
                    {
                        if (options[i] != null)
                        {
                            fallback = i;
                            break;
                        }
                    }

                    if (fallback >= 0)
                        index = fallback;
                }
            }

            Debug.Log($"[SOAK] Auto-pick spec card index={index}.");
            EventBus.Publish(new SpecCardChosenEvent { CardIndex = index });
        }

        private void OnShowEvolveChoice(ShowEvolveChoiceEvent evt)
        {
            _pendingEvolveTurretId = evt.TurretId;
            _nextModalActionRealtime = Time.realtimeSinceStartup + _modalAutoDelayUnscaled;
        }

        private void TryAutoEvolvePendingTurret()
        {
            if (_lastEvolveFrame == Time.frameCount)
                return;

            _lastEvolveFrame = Time.frameCount;
            _nextModalActionRealtime = Time.realtimeSinceStartup + _modalAutoDelayUnscaled;

            if (_pendingEvolveTurretId < 0)
                return;

            ResolveServices();

            var evolved = _turretManager != null
                ? _turretManager.EvolveTurret(_pendingEvolveTurretId, Mathf.Clamp(_evolutionPath, 0, 1))
                : null;

            Debug.Log($"[SOAK] Auto-evolve turret={_pendingEvolveTurretId}, path={_evolutionPath}, success={evolved != null}.");

            _pendingEvolveTurretId = -1;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.EvolveChoice)
                GameManager.Instance.PopModalState();
        }

        private void DriveLifecycleGrinder()
        {
            if (_mode != SoakMode.WaveLifecycleGrinder)
                return;

            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.WaveActive)
                return;

            if (Time.realtimeSinceStartup - _currentWaveStartRealtime < _grinderWaveActiveSeconds)
                return;

            ResolveServices();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _waveManager?.DebugStopMarketingWaveSpawn();
#endif
            _enemyManager?.ClearAll();
            _currentWaveStartRealtime = float.PositiveInfinity;

            Debug.Log($"[SOAK] Grinder cleared active enemies at wave={(_waveManager != null ? _waveManager.CurrentWave : -1)} after {_grinderWaveActiveSeconds:0.##} real seconds.");
        }

        private void CheckTargetWave()
        {
            if (_waveManager == null || _targetWave <= 0)
                return;

            if (_waveManager.CurrentWave < _targetWave)
                return;

            Debug.Log($"[SOAK] Target wave reached. wave={_waveManager.CurrentWave}, target={_targetWave}.");
            StopSoakTest(true);

            if (_pauseAtTargetWave && GameManager.Instance != null
                && GameManager.Instance.CurrentState != GameState.GameOver
                && GameManager.Instance.CurrentState != GameState.Paused)
            {
                GameManager.Instance.SetState(GameState.Paused);
            }
        }

        private void OnPrepStarted(PrepPhaseStartedEvent evt)
        {
            if (!_running || !_skipPrepAutomatically)
                return;

            _nextModalActionRealtime = Time.realtimeSinceStartup + _modalAutoDelayUnscaled;
        }

        private void OnWaveStarted(WaveStartedEvent evt)
        {
            if (!_running)
                return;

            _currentWaveStartRealtime = Time.realtimeSinceStartup;

            if (_logEveryNWaveStarts && _logEveryNWaves > 0 && evt.WaveNumber % _logEveryNWaves == 0)
                LogAccumulationSnapshot($"wave-start-{evt.WaveNumber}");
        }

        private void OnWaveCompleted(WaveCompletedEvent evt)
        {
            if (!_running)
                return;

            if (_logEveryNWaveStarts && _logEveryNWaves > 0 && evt.WaveNumber % _logEveryNWaves == 0)
                LogAccumulationSnapshot($"wave-complete-{evt.WaveNumber}");
        }

        private void OnGameOver(GameOverEvent evt)
        {
            if (!_running)
                return;

            Debug.Log($"[SOAK] GameOver reached during soak. score={evt.Score}, waves={evt.WavesCompleted}.");
            LogAccumulationSnapshot("game-over");
            StopSoakTest(false);
        }

        [ContextMenu("Log Accumulation Snapshot")]
        public void LogAccumulationSnapshot()
        {
            LogAccumulationSnapshot("manual-context");
        }

        private void LogAccumulationSnapshot(string reason)
        {
            ResolveServices();

            int wave = _waveManager != null ? _waveManager.CurrentWave : -1;
            int enemies = _enemyManager != null ? _enemyManager.ActiveCount : -1;
            int turrets = _turretManager != null && _turretManager.AllTurrets != null ? _turretManager.AllTurrets.Count : -1;
            string state = GameManager.Instance != null ? GameManager.Instance.CurrentState.ToString() : "no-gm";
            float fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
            long gcMb = System.GC.GetTotalMemory(false) / (1024L * 1024L);

            int totalSubscribers = -1;
            int eventTypes = -1;
            string eventSummary = "disabled";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_includeEventBusCounts)
            {
                totalSubscribers = EventBus.DebugSubscriberCountTotal();
                eventTypes = EventBus.DebugEventTypeCount;
                eventSummary = EventBus.DebugBuildSubscriberSummary();
            }
#endif

            if (!_includeObjectCounts)
            {
                Debug.Log($"[ACCUM] reason={reason} mode={_mode} wave={wave} state={state} fps={fps:0.0} timeScale={Time.timeScale:0.##} activeEnemies={enemies} turrets={turrets} gcMB={gcMb} eventTypes={eventTypes} subscribers={totalSubscribers} events=[{eventSummary}]");
                return;
            }

            int gameObjectsTotal = 0;
            int gameObjectsActive = 0;
            int gameObjectsInactive = 0;
            int enemyControllers = 0;
            int projectileComponents = 0;
            int damageNumbers = 0;
            int healthBars = 0;
            int particles = 0;
            int lineRenderers = 0;
            int audioSources = 0;
            int canvases = 0;
            int tmpTexts = 0;

            try
            {
                var gameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                gameObjectsTotal = gameObjects.Length;
                for (int i = 0; i < gameObjects.Length; i++)
                {
                    if (gameObjects[i] != null && gameObjects[i].activeInHierarchy)
                        gameObjectsActive++;
                }
                gameObjectsInactive = gameObjectsTotal - gameObjectsActive;

                enemyControllers = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                projectileComponents = CountByNameContains("Projectile");
                damageNumbers = CountByNameContains("DamageNumber");
                healthBars = UnityEngine.Object.FindObjectsByType<EnemyHealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                particles = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                lineRenderers = UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                audioSources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                tmpTexts = UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ACCUM] Object count failed: {ex.Message}");
            }

            Debug.Log($"[ACCUM] reason={reason} mode={_mode} wave={wave} state={state} fps={fps:0.0} timeScale={Time.timeScale:0.##} activeEnemies={enemies} turrets={turrets} GO={gameObjectsTotal} activeGO={gameObjectsActive} inactiveGO={gameObjectsInactive} EnemyController={enemyControllers} ProjectileName={projectileComponents} DamageNumberName={damageNumbers} HealthBar={healthBars} ParticleSystem={particles} LineRenderer={lineRenderers} AudioSource={audioSources} Canvas={canvases} TMP_Text={tmpTexts} gcMB={gcMb} eventTypes={eventTypes} subscribers={totalSubscribers} events=[{eventSummary}]");
        }

        private static int CountByNameContains(string token)
        {
            if (string.IsNullOrEmpty(token))
                return 0;

            int count = 0;
            var gameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < gameObjects.Length; i++)
            {
                if (gameObjects[i] != null && gameObjects[i].name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    count++;
            }
            return count;
        }
    }
}
