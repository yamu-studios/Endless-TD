// ============================================================================
// ETD.Enemies - EnemyManager.cs
// Spawns, pools, and tracks all active enemies. Handles split spawning.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using ETD.Core;
using ETD.Data;
using ETD.Grid;
using ETD.Pathfinding;

namespace ETD.Enemies
{
    public class EnemyManager : MonoBehaviour
    {
        [SerializeField] private Transform _enemyParent;

        private readonly List<EnemyController> _activeEnemies = new(64);
        private readonly Dictionary<EnemyController, int> _activeEnemyIndices = new(64);
        private readonly Dictionary<string, ObjectPool<EnemyController>> _pools = new();
        private GridSystem _grid;
        private AStarPathfinder _pathfinder;
        private int _nextInstanceId;

        public IReadOnlyList<EnemyController> ActiveEnemies => _activeEnemies;
        public int ActiveCount => _activeEnemies.Count;

        private float _waveSpawnOffset;
        private float _lastSpawnedEnemySpeed;

        [Header("Spatial Queries")]
        [Tooltip("World-space size of one enemy query bucket. Smaller buckets are faster for tiny ranges; larger buckets are better for very large ranges.")]
        [SerializeField] private float _spatialCellSize = 4f;

        [Tooltip("How often moving enemies are re-bucketed. 0.033 = about 30 Hz. Spawn/death still forces an immediate rebuild.")]
        [SerializeField] private float _spatialRebuildInterval = 0.033f;

        private readonly Dictionary<long, List<EnemyController>> _spatialBuckets = new(256);
        private readonly List<long> _usedSpatialKeys = new(256);
        private int _spatialRebuildFrame = -1;
        private float _nextSpatialRebuildTime = -1f;
        private bool _spatialDirty = true;
        private float _inverseSpatialCellSize = 0.25f;

        public static EnemyManager Instance;

        private void Update()
        {
            int count = _activeEnemies.Count;
            if (count == 0)
                return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            Profiler.BeginSample("EnemyManager.UpdateActiveEnemies");
            // Iterate backwards so enemies can die/reach the end and remove
            // themselves through callbacks without invalidating pending indices.
            for (int i = count - 1; i >= 0; i--)
            {
                if (i >= _activeEnemies.Count)
                    continue;

                EnemyController enemy = _activeEnemies[i];
                if (enemy != null)
                    enemy.ManagedUpdate(deltaTime);
            }
            Profiler.EndSample();
        }

        private void Awake()
        {
            Instance = this;
        }
        public void Initialize(GridSystem grid, AStarPathfinder pathfinder)
        {
            _grid = grid;
            _pathfinder = pathfinder;
            _nextInstanceId = 0;
            ServiceLocator.Register(this);

            EventBus.Subscribe<PathRecalculatedEvent>(OnPathRecalculated);
        }

        public void RegisterPool(EnemyData data, int preWarmCount = 256)
        {
            if (_pools.ContainsKey(data.Id)) return;

            var prefabController = data.Prefab.GetComponent<EnemyController>();
            if (prefabController == null)
            {
                Debug.LogError($"[EnemyManager] Prefab for {data.Id} missing EnemyController!");
                return;
            }

            _pools[data.Id] = new ObjectPool<EnemyController>(
                prefabController, _enemyParent, preWarmCount,
                onGet: e => e.gameObject.SetActive(true),
                onRelease: e =>
                {
                    e.gameObject.SetActive(false);
                    e.OnDeath = null;
                    e.OnReachedEnd = null;
                }
            );
        }
        public void ResetWaveSpawnOffset()
        {
            _waveSpawnOffset = 0f;
        }
        public EnemyController SpawnEnemy(EnemyData data, EnemyTier tier, int waveNumber, float spawnDelay,bool hasPos = false, Vector3? fixedPos = null)
        {
            if (!_pools.TryGetValue(data.Id, out var pool))
            {
                RegisterPool(data);
                pool = _pools[data.Id];
            }

            var enemy = pool.Get();
            enemy.InstanceId = _nextInstanceId++;

            var startPoint = _grid.EntryPoint;
            if (hasPos)
                startPoint = Vector2Int.RoundToInt(new Vector2(fixedPos.Value.x,fixedPos.Value.z));
            var gridPath = _pathfinder.FindPath(startPoint, _grid.ExitPoint);
            if (gridPath == null || gridPath.Count == 0)
            {
                Debug.LogError("[EnemyManager] No valid path for enemy spawn!");
                pool.Release(enemy);
                return null;
            }
            // Calculate spawn offset: how far behind entry this enemy starts
            // Based on accumulated delay × average speed
            float avgSpeed = data.GetScaledSpeed(waveNumber);
            _waveSpawnOffset += spawnDelay * avgSpeed;

            enemy.Initialize(data, tier, waveNumber, gridPath, _grid, _waveSpawnOffset,hasPos,fixedPos);
            enemy.OnDeath = OnEnemyDeath;
            enemy.OnReachedEnd = OnEnemyReachedEnd;

            _activeEnemyIndices[enemy] = _activeEnemies.Count;
            _activeEnemies.Add(enemy);
            MarkSpatialDirty();

            EventBus.Publish(new EnemySpawnedEvent
            {
                EnemyId = enemy.InstanceId,
                Position = enemy.transform.position
            });

            return enemy;
        }

        private void OnEnemyDeath(EnemyController enemy)
        {
            RemoveActiveEnemyFast(enemy);

            if (_pools.TryGetValue(enemy.Data.Id, out var pool))
                pool.Release(enemy);
        }

        private void OnEnemyReachedEnd(EnemyController enemy)
        {
            RemoveActiveEnemyFast(enemy);

            if (_pools.TryGetValue(enemy.Data.Id, out var pool))
                pool.Release(enemy);
        }

        private void RemoveActiveEnemyFast(EnemyController enemy)
        {
            if (enemy == null)
                return;

            if (!_activeEnemyIndices.TryGetValue(enemy, out int index))
                return;

            int lastIndex = _activeEnemies.Count - 1;
            EnemyController moved = _activeEnemies[lastIndex];

            _activeEnemies[index] = moved;
            _activeEnemies.RemoveAt(lastIndex);
            _activeEnemyIndices.Remove(enemy);

            if (index != lastIndex && moved != null)
                _activeEnemyIndices[moved] = index;

            MarkSpatialDirty();
        }

        // FIX (memory-leak #1c): Reuse a single shared grid-path buffer across all
        // enemies when a path-recalc event fires. Previously each call to
        // RecalculatePathLocal returned a freshly allocated List<Vector2Int>, and
        // UpdatePath allocated another List<Vector3> to convert it to world-space.
        // With 80-120 enemies alive at wave 100 that was 160-240 heap allocations
        // per turret placement, creating steady GC pressure that compounded into
        // the observed 3 GB memory growth and mid-wave freeze spikes.
        //
        // Now: one shared _pathRecalcBuffer is passed into the zero-alloc overload
        // of RecalculatePathLocal. UpdatePath clears and refills the enemy's own
        // _worldPath list in-place (no allocation). Net heap allocations per
        // path-recalc event: 0 (down from 2 x activeEnemyCount).
        private readonly List<Vector2Int> _pathRecalcBuffer = new(64);

        private void OnPathRecalculated(PathRecalculatedEvent evt)
        {
            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                EnemyController enemy = _activeEnemies[i];
                if (enemy == null) continue;

                Vector2Int gridPos = _grid.WorldToGrid(enemy.transform.position);
                bool found = _pathfinder.RecalculatePathLocal(gridPos, _pathRecalcBuffer);
                if (found && _pathRecalcBuffer.Count > 0)
                    enemy.UpdatePath(_pathRecalcBuffer);
            }
        }

        private void MarkSpatialDirty()
        {
            _spatialDirty = true;
            _spatialRebuildFrame = -1;
            _nextSpatialRebuildTime = -1f;
        }

        private void RebuildSpatialIndexIfNeeded()
        {
            int frame = Time.frameCount;
            if (_spatialRebuildFrame == frame)
                return;

            float now = Time.time;
            if (!_spatialDirty && now < _nextSpatialRebuildTime && _usedSpatialKeys.Count > 0)
                return;

            Profiler.BeginSample("EnemyManager.Spatial.Rebuild");

            _spatialRebuildFrame = frame;
            _spatialDirty = false;
            _nextSpatialRebuildTime = now + Mathf.Max(0.005f, _spatialRebuildInterval);

            _spatialCellSize = Mathf.Max(1f, _spatialCellSize);
            _inverseSpatialCellSize = 1f / _spatialCellSize;

            for (int i = 0; i < _usedSpatialKeys.Count; i++)
            {
                if (_spatialBuckets.TryGetValue(_usedSpatialKeys[i], out var bucket))
                    bucket.Clear();
            }
            _usedSpatialKeys.Clear();

            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                EnemyController enemy = _activeEnemies[i];
                if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                    continue;

                Vector3 pos = enemy.transform.position;
                long key = MakeSpatialKey(WorldToSpatialCell(pos.x), WorldToSpatialCell(pos.z));

                if (!_spatialBuckets.TryGetValue(key, out var bucket))
                {
                    bucket = new List<EnemyController>(8);
                    _spatialBuckets.Add(key, bucket);
                }

                if (bucket.Count == 0)
                    _usedSpatialKeys.Add(key);

                bucket.Add(enemy);
            }

            Profiler.EndSample();
        }

        private int WorldToSpatialCell(float value)
        {
            return Mathf.FloorToInt(value * _inverseSpatialCellSize);
        }

        private static long MakeSpatialKey(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        private bool TryGetSpatialBucket(int x, int z, out List<EnemyController> bucket)
        {
            return _spatialBuckets.TryGetValue(MakeSpatialKey(x, z), out bucket);
        }

        /// <summary>
        /// Gets up to <paramref name="maxResults"/> targetable enemies in the same
        /// deterministic spatial-bucket order used by GetEnemiesInRange. This is
        /// intended for effects such as chain lightning that never need every
        /// candidate, avoiding a full list fill at dense late-game waves.
        /// </summary>
        public void GetEnemiesInRangeLimited(
            Vector3 center,
            float radius,
            List<EnemyController> results,
            int maxResults,
            EnemyController exclude = null)
        {
            Profiler.BeginSample("EnemyManager.GetEnemiesInRangeLimited");
            results.Clear();

            if (maxResults <= 0 || _activeEnemies.Count == 0)
            {
                Profiler.EndSample();
                return;
            }

            RebuildSpatialIndexIfNeeded();

            float sqrRadius = radius * radius;
            int minX = WorldToSpatialCell(center.x - radius);
            int maxX = WorldToSpatialCell(center.x + radius);
            int minZ = WorldToSpatialCell(center.z - radius);
            int maxZ = WorldToSpatialCell(center.z + radius);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (!TryGetSpatialBucket(x, z, out var bucket))
                        continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        EnemyController enemy = bucket[i];
                        if (enemy == null || enemy == exclude || enemy.IsDead)
                            continue;
                        if (enemy.IsStealth && !enemy.IsRevealed)
                            continue;

                        Vector3 enemyPosition = enemy.transform.position;
                        float dx = enemyPosition.x - center.x;
                        float dz = enemyPosition.z - center.z;
                        if ((dx * dx) + (dz * dz) > sqrRadius)
                            continue;

                        results.Add(enemy);
                        if (results.Count >= maxResults)
                        {
                            Profiler.EndSample();
                            return;
                        }
                    }
                }
            }

            Profiler.EndSample();
        }

        /// <summary>
        /// Get all targetable enemies within radius. Uses a per-frame spatial hash
        /// so turret range checks do not scan the full active enemy list.
        /// </summary>
        public void GetEnemiesInRange(Vector3 center, float radius, List<EnemyController> results)
        {
            Profiler.BeginSample("EnemyManager.GetEnemiesInRange");
            results.Clear();

            if (_activeEnemies.Count == 0)
            {
                Profiler.EndSample();
                return;
            }

            RebuildSpatialIndexIfNeeded();

            float sqrRadius = radius * radius;
            int minX = WorldToSpatialCell(center.x - radius);
            int maxX = WorldToSpatialCell(center.x + radius);
            int minZ = WorldToSpatialCell(center.z - radius);
            int maxZ = WorldToSpatialCell(center.z + radius);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (!TryGetSpatialBucket(x, z, out var bucket))
                        continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        EnemyController enemy = bucket[i];
                        if (enemy == null || enemy.IsDead) continue;
                        if (enemy.IsStealth && !enemy.IsRevealed) continue;

                        Vector3 enemyPosition = enemy.transform.position;
                        float dx = enemyPosition.x - center.x;
                        float dz = enemyPosition.z - center.z;
                        if ((dx * dx) + (dz * dz) <= sqrRadius)
                            results.Add(enemy);
                    }
                }
            }
            Profiler.EndSample();
        }

        public void GetStealthEnemiesInRange(Vector3 center, float radius, List<EnemyController> results)
        {
            Profiler.BeginSample("EnemyManager.GetStealthEnemiesInRange");
            results.Clear();

            if (_activeEnemies.Count == 0)
            {
                Profiler.EndSample();
                return;
            }

            RebuildSpatialIndexIfNeeded();

            float sqrRadius = radius * radius;
            int minX = WorldToSpatialCell(center.x - radius);
            int maxX = WorldToSpatialCell(center.x + radius);
            int minZ = WorldToSpatialCell(center.z - radius);
            int maxZ = WorldToSpatialCell(center.z + radius);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (!TryGetSpatialBucket(x, z, out var bucket))
                        continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        EnemyController enemy = bucket[i];
                        if (enemy == null || enemy.IsDead) continue;
                        if (!enemy.IsStealth) continue;

                        Vector3 enemyPosition = enemy.transform.position;
                        float dx = enemyPosition.x - center.x;
                        float dz = enemyPosition.z - center.z;
                        if ((dx * dx) + (dz * dz) <= sqrRadius)
                            results.Add(enemy);
                    }
                }
            }
            Profiler.EndSample();
        }

        public EnemyController GetFirstEnemyInRange(Vector3 center, float radius, bool canTargetStealth)
        {
            Profiler.BeginSample("EnemyManager.GetFirstEnemyInRange");
            if (_activeEnemies.Count == 0)
            {
                Profiler.EndSample();
                return null;
            }

            RebuildSpatialIndexIfNeeded();

            float sqrRadius = radius * radius;
            EnemyController best = null;

            int minX = WorldToSpatialCell(center.x - radius);
            int maxX = WorldToSpatialCell(center.x + radius);
            int minZ = WorldToSpatialCell(center.z - radius);
            int maxZ = WorldToSpatialCell(center.z + radius);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (!TryGetSpatialBucket(x, z, out var bucket))
                        continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        EnemyController enemy = bucket[i];
                        if (enemy == null || enemy.IsDead) continue;
                        if (enemy.IsStealth && !enemy.IsRevealed && !canTargetStealth) continue;

                        Vector3 enemyPosition = enemy.transform.position;
                        float dx = enemyPosition.x - center.x;
                        float dz = enemyPosition.z - center.z;
                        if ((dx * dx) + (dz * dz) > sqrRadius) continue;

                        // Preserve previous behavior: lower InstanceId means the
                        // enemy was spawned earlier and approximates "first".
                        if (best == null || enemy.InstanceId < best.InstanceId)
                            best = enemy;
                    }
                }
            }

            Profiler.EndSample();
            return best;
        }

        public void ClearAll()
        {
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (_pools.TryGetValue(enemy.Data.Id, out var pool))
                    pool.Release(enemy);
            }
            _activeEnemies.Clear();
            _activeEnemyIndices.Clear();
            MarkSpatialDirty();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<PathRecalculatedEvent>(OnPathRecalculated);
            ServiceLocator.Unregister<EnemyManager>();
        }
    }
}
