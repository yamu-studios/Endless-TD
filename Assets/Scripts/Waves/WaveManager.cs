// ============================================================================
// ETD.Waves - WaveManager.cs  [UPDATED - tier-aware pools]
// Reads EnemyTier from each enemy's prefab EnemyController at generation time.
// Normal pool  → spawned every wave
// Elite pool   → spawned only at multiples of _eliteEveryNWaves (5, 10, 15…)
// Boss pool    → spawned only at multiples of _bossEveryNWaves   (25, 50, 75…)
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;
using ETD.Enemies;

namespace ETD.Waves
{
    public class WaveManager : MonoBehaviour
    {
        [Header("Wave Budget")]
        // Initializers reference the shared constants rather than repeating the numbers,
        // so the in-game wiki (which reads the constants) cannot drift from the tuning
        // a fresh WaveManager actually starts with.
        [SerializeField] private int _baseBudget = BalanceConstants.WaveBaseBudget;
        [SerializeField] private int _budgetPerWave = BalanceConstants.WaveBudgetPerWave;
        [SerializeField] private float _budgetQuadraticScale = BalanceConstants.WaveBudgetQuadraticScale;
        [SerializeField] private int _budgetSoftCapWave = BalanceConstants.WaveBudgetSoftCapWave;
        [SerializeField] private int _budgetPerWaveAfterSoftCap = BalanceConstants.WaveBudgetPerWaveAfterSoftCap;

        [Header("Spawning")]
        [SerializeField] private float _baseSpawnInterval = BalanceConstants.WaveBaseSpawnInterval;
        [SerializeField] private float _spawnIntervalReduction = BalanceConstants.WaveSpawnIntervalReduction;
        [SerializeField] private float _minSpawnInterval = BalanceConstants.WaveMinSpawnInterval;
        [SerializeField] private float _waveStartDelay = 0.5f;

        [Header("Elite / Boss Waves")]
        [SerializeField] private int _eliteEveryNWaves = GameConstants.ELITE_WAVE_INTERVAL;
        [SerializeField] private int _bossEveryNWaves = GameConstants.BOSS_WAVE_INTERVAL;
        [SerializeField] private float _eliteSpawnDelay = 1.5f;
        [SerializeField] private float _bossSpawnDelay = 3.0f;
        [SerializeField] private int _eliteFrequencyDoubleWave = GameConstants.INCREASED_ELITE_FREQUENCY_WAVE;

        private GameDatabase _db;
        private EnemyManager _enemyManager;
        private System.Random _rng;

        private int _currentWave;
        private float _prepTimeRemaining;
        private bool _isSpawning;
        private Coroutine _spawnCoroutine;

        // Tier pools — built once per wave from database
        private readonly List<EnemyData> _normalPool = new();
        private readonly List<EnemyData> _elitePool = new();
        private readonly List<EnemyData> _bossPool = new();

        public int CurrentWave => _currentWave;
        public float PrepTimeRemaining => _prepTimeRemaining;
        public bool IsSpawning => _isSpawning;

        // =================================================================
        // INIT
        // =================================================================

        public void Initialize(GameDatabase db, EnemyManager enemyManager, int seed = -1)
        {
            _db = db;
            _enemyManager = enemyManager;
            _rng = seed >= 0 ? new System.Random(seed) : new System.Random();
            _currentWave = 0;
            ServiceLocator.Register(this);
        }


        public void SetWave(int wave)
        {
            _currentWave = Mathf.Max(0, wave-1); // wave will not be incremented again until StartNextWave
        }

        // =================================================================
        // PREP PHASE
        // =================================================================

        public void StartPrepPhase()
        {
            _prepTimeRemaining = GameConstants.BASE_PREP_TIME;

            //var save = SaveSystem.Load();
            //if (save.AlwaysSkipPrep)
            //{
            //    float reward = SkipPrepTime();
            //    EventBus.Publish(new GoldChangedEvent
            //    {
            //        Delta = Mathf.RoundToInt(reward),
            //        Current = 0 // RunManager will handle gold add
            //    });
            //    GameManager.Instance.SetState(GameState.WaveActive);
            //    StartNextWave();
            //}

            EventBus.Publish(new PrepPhaseStartedEvent { Duration = _prepTimeRemaining });

            // Always Skip Prep setting
            var save = SaveSystem.Load();
            if (save.AlwaysSkipPrep)
            {
                float reward = SkipPrepTime();
                // Give the gold reward via RunManager
                EventBus.Publish(new PrepPhaseSkipedEvent { Reward = Mathf.RoundToInt(reward) });

                GameManager.Instance.SetState(GameState.WaveActive);
                StartNextWave();
                return;
            }
            GameManager.Instance.SetState(GameState.Preparation);
        }

        public float SkipPrepTime()
        {
            
            float reward = _prepTimeRemaining * GameConstants.SKIP_REWARD_GOLD_PER_SECOND*(CurrentWave+1);
            Debug.Log("Reward : " + reward);
            _prepTimeRemaining = 0f;
            return reward;
        }

        private void Update()
        {
            if (GameManager.Instance.CurrentState == GameState.Preparation)
            {
                _prepTimeRemaining -= Time.deltaTime;
                if (_prepTimeRemaining <= 0f)
                {
                    _prepTimeRemaining = 0f;
                    GameManager.Instance.SetState(GameState.WaveActive);
                    StartNextWave();
                }
            }

            if (!_isSpawning
                && GameManager.Instance.CurrentState == GameState.WaveActive
                && _enemyManager.ActiveCount == 0
                && _currentWave > 0)
            {
                EventBus.Publish(new WaveCompletedEvent { WaveNumber = _currentWave });
                StartPrepPhase();
            }
        }

        // =================================================================
        // WAVE START
        // =================================================================

        public void StartNextWave()
        {
            // Demo hard cap. This prevents wave 26 from starting, including when
            // Always Skip Prep immediately calls StartNextWave from StartPrepPhase.
            if (DemoMode.ShouldBlockStartingNextWave(_currentWave))
            {
                _isSpawning = false;
                DemoMode.CompleteDemo(_currentWave);
                return;
            }

            _currentWave++;
            _isSpawning = true;
            _enemyManager.ResetWaveSpawnOffset();

            EventBus.Publish(new WaveStartedEvent { WaveNumber = _currentWave });

            var commands = GenerateWave(_currentWave);
            _spawnCoroutine = StartCoroutine(SpawnWaveCoroutine(commands));
        }

        // =================================================================
        // GENERATE WAVE — tier-aware pools
        // =================================================================

        private List<SpawnCommand> GenerateWave(int wave)
        {
            var commands = new List<SpawnCommand>();
            if (_db?.Enemies == null || _db.Enemies.Length == 0) return commands;

            // Build tier pools for this wave
            BuildPools(wave);

            if (_normalPool.Count == 0 && _elitePool.Count == 0 && _bossPool.Count == 0)
                return commands;

            // --- BOSS WAVE: multiples of _bossEveryNWaves ---
            bool isBossWave = wave % _bossEveryNWaves == 0;

            // --- ELITE WAVE: multiples of _eliteEveryNWaves (but NOT boss waves) ---
            bool isEliteWave = !isBossWave && wave % _eliteEveryNWaves == 0;

            // 1. Fill normal budget with normal-tier enemies
            int budget = CalculateBudget(wave);
            if (_normalPool.Count > 0)
                SpendBudget(budget, _normalPool, wave, EnemyTier.Normal, commands);

            // 2. Add elite(s) — only on elite waves
            if (isEliteWave && _elitePool.Count > 0)
            {
                int eliteCount = GetEliteCount(wave);
                for (int i = 0; i < eliteCount; i++)
                {
                    var enemy = PickRandom(_elitePool);
                    int insertAt = commands.Count > 0 ? _rng.Next(commands.Count) : 0;
                    commands.Insert(insertAt, new SpawnCommand
                    {
                        Data = enemy,
                        Tier = EnemyTier.Elite,
                        Delay = _eliteSpawnDelay
                    });
                }
            }

            // 3. Add boss — only on boss waves
            if (isBossWave && _bossPool.Count > 0)
            {
                // Boss always spawns LAST for dramatic effect
                commands.Add(new SpawnCommand
                {
                    Data = PickRandom(_bossPool),
                    Tier = EnemyTier.Boss,
                    Delay = _bossSpawnDelay
                });
            }

            return commands;
        }

        // =================================================================
        // BUILD TIER POOLS — reads Tier from each enemy's prefab
        // =================================================================

        private void BuildPools(int wave)
        {
            _normalPool.Clear();
            _elitePool.Clear();
            _bossPool.Clear();

            for (int i = 0; i < _db.Enemies.Length; i++)
            {
                var data = _db.Enemies[i];
                if (data == null || !data.IsAvailableAt(wave)) continue;
                if (data.Prefab == null) continue;

                // Read tier from prefab's EnemyController
                var ec = data.Prefab.GetComponent<EnemyController>();
                if (ec == null) continue;

                switch (ec.Tier)
                {
                    case EnemyTier.Normal: _normalPool.Add(data); break;
                    case EnemyTier.Elite: _elitePool.Add(data); break;
                    case EnemyTier.Boss: _bossPool.Add(data); break;
                }
            }

            // Safety: if elite/boss pool is empty, fall back to normal pool
            if (_elitePool.Count == 0 && _normalPool.Count > 0)
                _elitePool.AddRange(_normalPool);
            if (_bossPool.Count == 0 && _elitePool.Count > 0)
                _bossPool.AddRange(_elitePool);
        }

        // =================================================================
        // SPEND BUDGET
        // =================================================================

        private void SpendBudget(int budget, List<EnemyData> pool, int wave,
            EnemyTier tier, List<SpawnCommand> commands)
        {
            int remaining = budget;
            float interval = Mathf.Max(
                _baseSpawnInterval - wave * _spawnIntervalReduction,
                _minSpawnInterval);

            while (remaining > 0)
            {
                var affordable = new List<EnemyData>();
                for (int i = 0; i < pool.Count; i++)
                    if (pool[i].SpawnWeight <= remaining)
                        affordable.Add(pool[i]);

                if (affordable.Count == 0) break;

                var enemy = PickRandom(affordable);
                remaining -= enemy.SpawnWeight;

                commands.Add(new SpawnCommand
                {
                    Data = enemy,
                    Tier = tier,
                    Delay = interval + (float)(_rng.NextDouble() * 0.2 - 0.1)
                });
            }

            Shuffle(commands);
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private int CalculateBudget(int wave)
        {
            int safeWave = Mathf.Max(0, wave);
            int softCapWave = Mathf.Max(0, _budgetSoftCapWave);
            int curveWave = Mathf.Min(safeWave, softCapWave);

            // Preserve the original linear + quadratic curve through the soft-cap wave.
            // Beyond it, use a fixed linear increase so enemy counts remain manageable
            // while HP, elites, bosses, and player scaling continue progressing.
            long budget = _baseBudget
                        + (long)curveWave * _budgetPerWave
                        + Mathf.RoundToInt(curveWave * curveWave * _budgetQuadraticScale);

            if (safeWave > softCapWave)
                budget += (long)(safeWave - softCapWave)
                        * Mathf.Max(0, _budgetPerWaveAfterSoftCap);

            return (int)System.Math.Min(int.MaxValue, System.Math.Max(0L, budget));
        }

        private int GetEliteCount(int wave)
        {
            int count = 1 + wave / BalanceConstants.EliteCountWaveStep;
            if (wave >= _eliteFrequencyDoubleWave) count *= 2;
            return count;
        }

        private EnemyData PickRandom(List<EnemyData> pool)
            => pool[_rng.Next(pool.Count)];

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // =================================================================
        // SPAWN COROUTINE
        // =================================================================

        private IEnumerator SpawnWaveCoroutine(List<SpawnCommand> commands)
        {
            yield return new WaitForSeconds(_waveStartDelay);

            for (int i = 0; i < commands.Count; i++)
            {
                while (GameManager.Instance.CurrentState == GameState.Paused
                    || GameManager.Instance.CurrentState == GameState.LevelUp
                    || GameManager.Instance.CurrentState == GameState.EvolveChoice)
                    yield return null;

                var cmd = commands[i];
                _enemyManager.SpawnEnemy(cmd.Data, cmd.Tier, _currentWave, cmd.Delay);
                yield return new WaitForSeconds(cmd.Delay);
            }

            _isSpawning = false;
        }

        private void OnDestroy()
        {
            if (_spawnCoroutine != null) StopCoroutine(_spawnCoroutine);
            ServiceLocator.Unregister<WaveManager>();
        }

        private struct SpawnCommand
        {
            public EnemyData Data;
            public EnemyTier Tier;
            public float Delay;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugSetCurrentWaveForShowcase(int wave)
        {
            _currentWave = Mathf.Max(0, wave);
        }
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugSpawnSpecificWaveForMarketing(
            int wave,
            bool clearExistingEnemies = true,
            bool publishWaveStartedEvent = true,
            bool forceWaveActiveState = true)
        {
            wave = Mathf.Max(1, wave);

            if (_db == null || _enemyManager == null)
            {
                Debug.LogError("[WaveManager] Cannot spawn marketing wave. WaveManager is not initialized.");
                return;
            }

            if (_spawnCoroutine != null)
            {
                StopCoroutine(_spawnCoroutine);
                _spawnCoroutine = null;
            }

            if (clearExistingEnemies)
                _enemyManager.ClearAll();

            _currentWave = wave;
            _isSpawning = true;
            _enemyManager.ResetWaveSpawnOffset();

            if (forceWaveActiveState && GameManager.Instance != null)
                GameManager.Instance.SetState(GameState.WaveActive);

            if (publishWaveStartedEvent)
                EventBus.Publish(new WaveStartedEvent { WaveNumber = _currentWave });

            var commands = GenerateWave(_currentWave);

            Debug.Log($"[WaveManager] Marketing spawn wave {_currentWave}. Commands: {commands.Count}");

            _spawnCoroutine = StartCoroutine(SpawnWaveCoroutine(commands));
        }

        public void DebugStopMarketingWaveSpawn()
        {
            if (_spawnCoroutine != null)
            {
                StopCoroutine(_spawnCoroutine);
                _spawnCoroutine = null;
            }

            _isSpawning = false;
            Debug.Log("[WaveManager] Marketing wave spawn stopped.");
        }

        public int DebugPreviewMarketingWaveCommandCount(int wave)
        {
            wave = Mathf.Max(1, wave);

            if (_db == null)
                return 0;

            var commands = GenerateWave(wave);
            return commands.Count;
        }
#endif


    }
}
