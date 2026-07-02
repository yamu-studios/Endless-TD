// ============================================================================
// ETD.Projectiles - ProjectileManager.cs
// Pooled projectile spawning plus centralized projectile simulation.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using ETD.Core;
using ETD.Data;
using ETD.Enemies;

namespace ETD.Projectiles
{
    public sealed class ProjectileManager : MonoBehaviour
    {
        [SerializeField] private Transform _projectileParent;

        [Header("Pooling")]
        // FIX (profiler-confirmed, 2026-06-30 capture): At wave 55 with multiple
        // high-fire-rate turrets active, 11 fresh Instantiate() calls landed in a
        // single frame, costing 95.31ms total / 90.78ms self in Instantiate.Awake
        // alone (39.6% of a 229ms frame). This is the pool running dry under real
        // combat load and falling back to cold instantiation. Raised from 96 to
        // 192 and expand batch from 32 to 64 so the pre-warm absorbs late-wave
        // density up front (one-time scene-load cost) instead of paying for it
        // mid-combat where it causes visible frame drops.
        [SerializeField] private int _poolSizePerPrefab = 192;
        [SerializeField] private int _poolExpandBatchSize = 64;

        [Header("Visual Budget")]
        [SerializeField] private int _maxActiveVisualProjectiles = 384;
        [SerializeField] private bool _useDirectDamageWhenBudgetExceeded = true;

        [Header("Spawn Budget")]
        [Tooltip("Hard cap on visual projectile activations per rendered frame. Extra logical shots become direct hits, preserving damage/status while preventing SetActive spikes.")]
        [SerializeField, Min(1)] private int _maxVisualSpawnsPerFrame = 96;

        [Header("Adaptive Visual LOD")]
        [SerializeField] private bool _enableAdaptiveVisualLod = true;
        [Range(0.05f, 1f)] [SerializeField] private float _reducedVisualLodThreshold = 0.45f;
        [Range(0.05f, 1f)] [SerializeField] private float _criticalVisualLodThreshold = 0.72f;
        [Min(1)] [SerializeField] private int _reducedVisualShotInterval = 2;
        [Min(1)] [SerializeField] private int _criticalVisualShotInterval = 4;

        [Header("Projectile Simulation LOD")]
        [Tooltip("When active visuals exceed this count, simulate them at the reduced interval. Logical projectile travel remains time-correct; only visible update cadence drops.")]
        [SerializeField, Min(1)] private int _reducedSimulationThreshold = 96;
        [SerializeField, Min(0f)] private float _reducedSimulationInterval = 0.0333f;

        [Tooltip("When active visuals exceed this count, simulate them at the critical interval. Use direct-hit visual LOD as the main protection before reaching this tier.")]
        [SerializeField, Min(1)] private int _criticalSimulationThreshold = 192;
        [SerializeField, Min(0f)] private float _criticalSimulationInterval = 0.05f;

        [Header("Emergency Projectile Pressure")]
        [Tooltip("Hard cap for simulated visual projectiles. If late waves exceed this, the oldest extra visuals are resolved immediately as direct hits. This is the strongest protection for GTX1050-class laptops.")]
        [SerializeField, Min(16)] private int _hardActiveSimulationCap = 160;

        [Tooltip("Maximum overflow projectiles that can be converted to direct hits in one frame. Prevents a single massive cleanup spike.")]
        [SerializeField, Min(1)] private int _maxOverflowResolvesPerFrame = 48;

        [Tooltip("Projectile transform rotation is skipped above this count. Movement stays correct; only visual facing is simplified.")]
        [SerializeField, Min(0)] private int _disableProjectileRotationAboveCount = 96;

        internal sealed class ProjectilePool
        {
            public GameObject Prefab;
            public readonly Queue<Projectile> Inactive = new();
            public int TotalCreated;
            public bool IsRegistered = true;
        }

        /// <summary>
        /// Cached by TurretController at placement/evolution time. Carries the pool
        /// itself, eliminating a hot-path Dictionary lookup for normal shots.
        /// </summary>
        public readonly struct ProjectilePoolHandle
        {
            internal readonly int PrefabId;
            internal readonly GameObject Prefab;
            internal readonly ProjectilePool Pool;

            internal ProjectilePoolHandle(int prefabId, GameObject prefab, ProjectilePool pool)
            {
                PrefabId = prefabId;
                Prefab = prefab;
                Pool = pool;
            }

            public bool IsValid => Prefab != null && PrefabId != 0 && Pool != null && Pool.IsRegistered;
        }

        private readonly Dictionary<int, ProjectilePool> _pools = new();
        private readonly List<Projectile> _activeProjectiles = new(384);
        private Action<Projectile> _returnToPoolCallback;
        private int _activeVisualProjectiles;
        private int _visualBudgetFrame = -1;
        private int _visualSpawnRequestsThisFrame;
        private float _nextSimulationTime;
        private float _lastSimulationTime = -1f;

        public int ActiveVisualProjectiles => _activeVisualProjectiles;
        public int MaxActiveVisualProjectiles => _maxActiveVisualProjectiles;

        private void Awake()
        {
            _returnToPoolCallback = ReturnToPool;
            ServiceLocator.Register(this);
        }

        private void Update()
        {
            int activeCount = _activeProjectiles.Count;
            if (activeCount == 0)
            {
                _lastSimulationTime = -1f;
                _nextSimulationTime = 0f;
                return;
            }

            float now = Time.time;
            float interval = GetAdaptiveSimulationInterval(activeCount);
            if (interval > 0f && now < _nextSimulationTime)
                return;

            float deltaTime = _lastSimulationTime < 0f
                ? Time.deltaTime
                : Mathf.Max(0f, now - _lastSimulationTime);
            _lastSimulationTime = now;
            _nextSimulationTime = interval > 0f ? now + interval : now;

            Profiler.BeginSample("ProjectileManager.UpdateActiveProjectiles");

            ResolveProjectileOverflow();

            bool allowRotation = _disableProjectileRotationAboveCount <= 0 ||
                _activeProjectiles.Count < _disableProjectileRotationAboveCount;

            // Iterate backwards. A projectile can complete and remove itself in O(1)
            // without invalidating the remaining iteration.
            for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                Projectile projectile = _activeProjectiles[i];
                if (projectile == null)
                {
                    RemoveActiveAt(i);
                    continue;
                }

                projectile.ManagedTick(deltaTime, now, allowRotation);
            }

            Profiler.EndSample();
        }

        private void ResolveProjectileOverflow()
        {
            int hardCap = Mathf.Max(16, _hardActiveSimulationCap);
            int overflow = _activeProjectiles.Count - hardCap;
            if (overflow <= 0)
                return;

            Profiler.BeginSample("ProjectileManager.ResolveOverflow.DirectHit");
            int resolves = Mathf.Min(overflow, Mathf.Max(1, _maxOverflowResolvesPerFrame));

            // Resolve from the end for O(1) removals. These are usually the newest
            // visuals, but gameplay is preserved and the CPU gets immediate relief.
            for (int i = 0; i < resolves && _activeProjectiles.Count > hardCap; i++)
            {
                int index = _activeProjectiles.Count - 1;
                Projectile projectile = _activeProjectiles[index];
                if (projectile == null)
                {
                    RemoveActiveAt(index);
                    continue;
                }

                projectile.ResolveImmediate(playHitVFX: false);
            }

            Profiler.EndSample();
        }

        private float GetAdaptiveSimulationInterval(int activeCount)
        {
            if (activeCount >= _criticalSimulationThreshold)
                return Mathf.Max(0f, _criticalSimulationInterval);

            if (activeCount >= _reducedSimulationThreshold)
                return Mathf.Max(0f, _reducedSimulationInterval);

            return 0f;
        }

        public ProjectilePoolHandle GetPoolHandle(GameObject prefab)
        {
            if (prefab == null)
                return default;

            int prefabId = prefab.GetInstanceID();
            ProjectilePool pool = GetOrCreatePool(prefabId, prefab);
            return new ProjectilePoolHandle(prefabId, prefab, pool);
        }

        public bool ShouldSpawnVisualProjectile(ref int sequence)
        {
            sequence = sequence == int.MaxValue ? 0 : sequence + 1;

            if (_visualBudgetFrame != Time.frameCount)
            {
                _visualBudgetFrame = Time.frameCount;
                _visualSpawnRequestsThisFrame = 0;
            }

            if (_maxVisualSpawnsPerFrame > 0 && _visualSpawnRequestsThisFrame >= _maxVisualSpawnsPerFrame)
                return false;

            if (_enableAdaptiveVisualLod && _maxActiveVisualProjectiles > 0)
            {
                float pressure = (float)_activeVisualProjectiles / _maxActiveVisualProjectiles;
                int interval = 1;
                if (pressure >= _criticalVisualLodThreshold)
                    interval = Mathf.Max(1, _criticalVisualShotInterval);
                else if (pressure >= _reducedVisualLodThreshold)
                    interval = Mathf.Max(1, _reducedVisualShotInterval);

                if (interval > 1 && (sequence % interval) != 0)
                    return false;
            }

            _visualSpawnRequestsThisFrame++;
            return true;
        }

        public void WarmPool(GameObject prefab, int desiredCount = -1)
        {
            if (prefab == null)
                return;

            ProjectilePoolHandle handle = GetPoolHandle(prefab);
            if (!handle.IsValid)
                return;

            ProjectilePool pool = handle.Pool;
            int target = desiredCount > 0 ? desiredCount : Mathf.Max(1, _poolSizePerPrefab);
            if (pool.TotalCreated >= target)
                return;

            Profiler.BeginSample("ProjectileManager.WarmPool.Instantiate");
            CreateProjectiles(pool, target - pool.TotalCreated);
            Profiler.EndSample();
        }

        public Projectile SpawnProjectile(
            ProjectilePoolHandle handle,
            Vector3 origin,
            EnemyController target,
            float damage,
            float speed = 15f,
            StatusEffectType statusType = StatusEffectType.None,
            float statusValue = 0f,
            float statusDuration = 0f,
            bool isCritical = false,
            Action<EnemyController, float> onHitAfterDamage = null,
            IProjectileHitHandler hitHandler = null,
            bool requestVisual = true,
            int sourceTurretType = -1,
            int sourceTurretId = -1)
        {
            if (target == null || target.IsDead)
                return null;

            if (!requestVisual || !handle.IsValid ||
                (_hardActiveSimulationCap > 0 && _activeProjectiles.Count >= _hardActiveSimulationCap) ||
                (_useDirectDamageWhenBudgetExceeded && _maxActiveVisualProjectiles > 0 &&
                 _activeVisualProjectiles >= _maxActiveVisualProjectiles))
            {
                Profiler.BeginSample("ProjectileManager.Spawn.DirectHit.VisualLOD");
                ApplyDirectHit(target, damage, statusType, statusValue, statusDuration, isCritical, onHitAfterDamage, hitHandler, sourceTurretType, sourceTurretId);
                Profiler.EndSample();
                return null;
            }

            Profiler.BeginSample("ProjectileManager.Spawn.FastPool");
            ProjectilePool pool = handle.Pool;
            if (pool == null || !pool.IsRegistered)
            {
                Profiler.EndSample();
                ApplyDirectHit(target, damage, statusType, statusValue, statusDuration, isCritical, onHitAfterDamage, hitHandler, sourceTurretType, sourceTurretId);
                return null;
            }

            Projectile projectile = GetFromPool(pool);
            if (projectile == null)
            {
                Profiler.EndSample();
                ApplyDirectHit(target, damage, statusType, statusValue, statusDuration, isCritical, onHitAfterDamage, hitHandler, sourceTurretType, sourceTurretId);
                return null;
            }

            projectile.PoolPrefabId = handle.PrefabId;
            projectile.transform.SetPositionAndRotation(origin, Quaternion.identity);
            projectile.OnComplete = _returnToPoolCallback;
            projectile.gameObject.SetActive(true);
            projectile.Launch(target, damage, speed, statusType, statusValue, statusDuration, isCritical, onHitAfterDamage, hitHandler, sourceTurretType, sourceTurretId);
            RegisterActive(projectile);
            _activeVisualProjectiles++;

            Profiler.EndSample();
            return projectile;
        }

        public Projectile SpawnProjectile(GameObject prefab, Vector3 origin,
            EnemyController target, float damage, float speed = 15f,
            StatusEffectType statusType = StatusEffectType.None,
            float statusValue = 0f, float statusDuration = 0f,
            bool isCritical = false,
            Action<EnemyController, float> onHitAfterDamage = null,
            IProjectileHitHandler hitHandler = null,
            int sourceTurretType = -1,
            int sourceTurretId = -1)
        {
            return SpawnProjectile(GetPoolHandle(prefab), origin, target, damage, speed,
                statusType, statusValue, statusDuration, isCritical, onHitAfterDamage, hitHandler,
                requestVisual: true, sourceTurretType: sourceTurretType, sourceTurretId: sourceTurretId);
        }

        public Projectile SpawnProjectile(Vector3 origin, EnemyController target,
            float damage, float speed = 15f,
            StatusEffectType statusType = StatusEffectType.None,
            float statusValue = 0f, float statusDuration = 0f,
            bool isCritical = false,
            int sourceTurretType = -1,
            int sourceTurretId = -1)
        {
            if (target != null && !target.IsDead)
                target.TakeDamage(damage, statusType, statusValue, statusDuration,
                isCritical: isCritical, sourceTurretType: sourceTurretType, sourceTurretId: sourceTurretId);
            return null;
        }

        /// <summary>
        /// Fast logical hit used when projectile visuals are intentionally skipped.
        /// It preserves the same damage, status and hit-handler behavior as a pooled projectile impact.
        /// </summary>
        public void ResolveDirectHit(EnemyController target, float damage,
            StatusEffectType statusType, float statusValue, float statusDuration,
            bool isCritical = false,
            Action<EnemyController, float> onHitAfterDamage = null,
            IProjectileHitHandler hitHandler = null,
            int sourceTurretType = -1,
            int sourceTurretId = -1)
        {
            ApplyDirectHit(target, damage, statusType, statusValue, statusDuration, isCritical, onHitAfterDamage, hitHandler, sourceTurretType, sourceTurretId);
        }

        private static void ApplyDirectHit(EnemyController target, float damage,
            StatusEffectType statusType, float statusValue, float statusDuration,
            bool isCritical,
            Action<EnemyController, float> onHitAfterDamage,
            IProjectileHitHandler hitHandler,
            int sourceTurretType = -1,
            int sourceTurretId = -1)
        {
            if (target == null || target.IsDead)
                return;

            target.TakeDamage(damage, statusType, statusValue, statusDuration,
                isCritical: isCritical, sourceTurretType: sourceTurretType, sourceTurretId: sourceTurretId);
            hitHandler?.OnProjectileHitAfterDamage(target, damage, statusType, statusValue, statusDuration);
            onHitAfterDamage?.Invoke(target, damage);
        }

        private ProjectilePool GetOrCreatePool(int prefabId, GameObject prefab)
        {
            if (_pools.TryGetValue(prefabId, out ProjectilePool existing))
                return existing;

            ProjectilePool pool = new ProjectilePool { Prefab = prefab, IsRegistered = true };
            _pools[prefabId] = pool;
            return pool;
        }

        private Projectile GetFromPool(ProjectilePool pool)
        {
            if (pool == null || !pool.IsRegistered)
                return null;

            if (pool.TotalCreated == 0)
            {
                Profiler.BeginSample("ProjectileManager.FirstUsePrewarm.Instantiate");
                CreateProjectiles(pool, Mathf.Max(1, _poolSizePerPrefab));
                Profiler.EndSample();
            }

            if (pool.Inactive.Count == 0)
            {
                Profiler.BeginSample("ProjectileManager.PoolMiss.Expand.Instantiate");
                CreateProjectiles(pool, Mathf.Max(1, _poolExpandBatchSize));
                Profiler.EndSample();
            }

            while (pool.Inactive.Count > 0)
            {
                Projectile projectile = pool.Inactive.Dequeue();
                if (projectile != null)
                    return projectile;
            }

            return null;
        }

        private void CreateProjectiles(ProjectilePool pool, int count)
        {
            if (pool == null || !pool.IsRegistered || pool.Prefab == null || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(pool.Prefab, _projectileParent);
                Projectile projectile = go.GetComponent<Projectile>();
                if (projectile == null)
                    projectile = go.AddComponent<Projectile>();
                go.SetActive(false);
                pool.Inactive.Enqueue(projectile);
                pool.TotalCreated++;
            }
        }

        private void RegisterActive(Projectile projectile)
        {
            if (projectile == null || projectile.ActiveIndex >= 0)
                return;

            projectile.ActiveIndex = _activeProjectiles.Count;
            _activeProjectiles.Add(projectile);
        }

        private void UnregisterActive(Projectile projectile)
        {
            if (projectile == null)
                return;

            int index = projectile.ActiveIndex;
            if (index < 0 || index >= _activeProjectiles.Count || _activeProjectiles[index] != projectile)
                return;

            RemoveActiveAt(index);
        }

        private void RemoveActiveAt(int index)
        {
            int lastIndex = _activeProjectiles.Count - 1;
            Projectile removed = _activeProjectiles[index];
            Projectile last = _activeProjectiles[lastIndex];

            if (index != lastIndex)
            {
                _activeProjectiles[index] = last;
                if (last != null)
                    last.ActiveIndex = index;
            }

            _activeProjectiles.RemoveAt(lastIndex);
            if (removed != null)
                removed.ActiveIndex = -1;
        }

        private void ReturnToPool(Projectile projectile)
        {
            if (projectile == null)
                return;

            UnregisterActive(projectile);
            int prefabId = projectile.PoolPrefabId;
            projectile.OnComplete = null;
            projectile.gameObject.SetActive(false);
            _activeVisualProjectiles = Mathf.Max(0, _activeVisualProjectiles - 1);

            if (_pools.TryGetValue(prefabId, out ProjectilePool pool) && pool.IsRegistered)
                pool.Inactive.Enqueue(projectile);
            else
                Destroy(projectile.gameObject);
        }

        public void ClearAll()
        {
            for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                Projectile projectile = _activeProjectiles[i];
                if (projectile != null)
                {
                    projectile.OnComplete = null;
                    projectile.ActiveIndex = -1;
                    Destroy(projectile.gameObject);
                }
            }
            _activeProjectiles.Clear();

            foreach (ProjectilePool pool in _pools.Values)
            {
                pool.IsRegistered = false;
                while (pool.Inactive.Count > 0)
                {
                    Projectile projectile = pool.Inactive.Dequeue();
                    if (projectile != null)
                        Destroy(projectile.gameObject);
                }
            }

            _pools.Clear();
            _activeVisualProjectiles = 0;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ProjectileManager>();
        }
    }
}
