// ============================================================================
// ETD.Turrets - TurretManager.cs  [UPDATED]
// Added EvolveTurret() â destroys old GO, spawns evolved prefab in same spot,
// transfers all state (level, gold invested, grid position) to new controller.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;
using ETD.Grid;
using ETD.Pathfinding;
using ETD.Enemies;

namespace ETD.Turrets
{
    public class TurretManager : MonoBehaviour
    {
        [SerializeField] private Transform _turretParent;

        private readonly Dictionary<int, TurretController> _turrets = new();
        private readonly List<TurretController> _tempTurretList = new(8);

        [Header("Spatial Queries")]
        [SerializeField] private float _turretSpatialCellSize = 4f;
        private readonly Dictionary<long, List<TurretController>> _turretSpatialBuckets = new(128);
        private readonly List<long> _usedTurretSpatialKeys = new(128);
        private bool _turretSpatialDirty = true;
        private float _inverseTurretSpatialCellSize = 0.25f;

        private GridSystem _grid;
        private AStarPathfinder _pathfinder;
        private int _nextInstanceId;

        private readonly List<TurretController> _activeTurrets = new();
        private SupportAuraSystem _supportAuraSystem;

        public IReadOnlyCollection<TurretController> AllTurrets => _turrets.Values;

        /// <summary>
        /// Coalesces static support-aura recalculation. The system is created at
        /// runtime so no scene setup is required for existing gameplay scenes.
        /// </summary>
        public void MarkSupportAurasDirty()
        {
            _supportAuraSystem?.MarkTopologyDirty();
        }

        public void Initialize(GridSystem grid, AStarPathfinder pathfinder)
        {
            _grid = grid;
            _pathfinder = pathfinder;
            _nextInstanceId = 0;
            ServiceLocator.Register(this);

            _supportAuraSystem = GetComponent<SupportAuraSystem>();
            if (_supportAuraSystem == null)
                _supportAuraSystem = gameObject.AddComponent<SupportAuraSystem>();
            _supportAuraSystem.Initialize(this);

            EventBus.Subscribe<EnemyDebuffAuraEvent>(OnEnemyDebuffAura);
            EventBus.Subscribe<SpecCardChosenAfterEvent>(OnSpecCardChosenAfter);
            EventBus.Subscribe<TurretPlacedEvent>(OnTurretPlacedInternal);
            EventBus.Subscribe<TurretSoldEvent>(OnTurretSoldInternal);
        }

        // =================================================================
        // PLACEMENT
        // =================================================================

        public TurretController PlaceAndRestoreTurret(TurretData data, Vector2Int pos, PlacedTurretSnapshot snap)
        {
            var turret = PlaceTurretInternal(data, pos, publishPlacementEvent: false);
            if (turret == null) return null;

            if (snap.IsEvolved && snap.EvolutionPath >= 0)
                turret = EvolveTurret(turret.InstanceId, snap.EvolutionPath, publishEvent: false);

            if (turret != null)
            {
                turret.RestoreSnapshotState(snap.Level, snap.Gold, snap.IsEvolved, snap.EvolutionPath);
                turret.SetTargetingMode((TargetingMode)snap.TargetingMode);
                if (!_activeTurrets.Contains(turret))
                    _activeTurrets.Add(turret);
            }

            return turret;
        }



        public bool CanPlaceTurret(Vector2Int gridPos)
        {
            if (!_grid.CanPlace(gridPos)) return false;
            return !_grid.WouldBlockPath(gridPos);
        }

        public TurretController PlaceTurret(TurretData data, Vector2Int gridPos)
        {
            return PlaceTurretInternal(data, gridPos, publishPlacementEvent: true);
        }

        private TurretController PlaceTurretInternal(TurretData data, Vector2Int gridPos, bool publishPlacementEvent)
        {
            if (!CanPlaceTurret(gridPos))
            {
                Debug.LogWarning($"[TurretManager] Cannot place at {gridPos}");
                return null;
            }

            var go = Instantiate(data.Prefab, _turretParent);
            go.transform.position = _grid.GridToWorld(gridPos) + new Vector3(0,data.yOffset,0);

            var controller = go.GetComponent<TurretController>();
            if (controller == null)
                controller = go.AddComponent<TurretController>();

            controller.InstanceId = _nextInstanceId++;
            var cell = _grid.GetCell(gridPos);
            controller.Initialize(data, gridPos, cell?.Specialty ?? TileSpecialty.None, cell?.DynamicTile);

            _turrets[controller.InstanceId] = controller;
            _turretSpatialDirty = true;
            MarkSupportAurasDirty();
            _grid.PlaceTurret(gridPos, controller.InstanceId);

            _pathfinder.RecalculatePath();

            if (publishPlacementEvent)
            {
                EventBus.Publish(new TurretPlacedEvent
                {
                    TurretId = controller.InstanceId,
                    GridPos = gridPos
                });
            }
            else if (!_activeTurrets.Contains(controller))
            {
                _activeTurrets.Add(controller);
            }

            return controller;
        }

        // =================================================================
        // EVOLUTION â destroy old prefab, spawn evolved prefab, transfer state
        // =================================================================

        public TurretController EvolveTurret(int instanceId, int evolutionPath, bool publishEvent = true)
        {
            if (!_turrets.TryGetValue(instanceId, out var oldController))
            {
                Debug.LogWarning($"[TurretManager] EvolveTurve: turret {instanceId} not found");
                return null;
            }

            if (oldController.IsEvolved)
            {
                Debug.LogWarning($"[TurretManager] Turret {instanceId} is already evolved.");
                return oldController;
            }

            if (oldController.Data == null)
            {
                Debug.LogWarning($"[TurretManager] Turret {instanceId} has no TurretData.");
                return oldController;
            }

            if (oldController.Data.Type == TurretType.Radar)
            {
                Debug.LogWarning("[TurretManager] Radar turrets cannot evolve.");
                return oldController;
            }

            if (evolutionPath != 0 && evolutionPath != 1)
            {
                Debug.LogWarning($"[TurretManager] Invalid evolution path: {evolutionPath}");
                return oldController;
            }

            var data = oldController.Data;
            var evo = evolutionPath == 0 ? data.PathA : data.PathB;

            if (evo == null)
            {
                Debug.LogWarning($"[TurretManager] Missing evolution data for {data.DisplayName}, path {evolutionPath}");
                return oldController;
            }

            // Determine which prefab to use
            GameObject evolvedPrefab = evo?.EvolvedPrefab;

            // Fallback: if no evolution-specific prefab, keep same GO and just update visuals
            if (evolvedPrefab == null)
            {
                Debug.Log($"[TurretManager] No EvolvedPrefab set for {data.DisplayName} path {evolutionPath} â evolving in place.");
                oldController.Evolve(evolutionPath, publishEvent);
                return oldController;
            }

            // Snapshot state from old turret
            Vector2Int gridPos = oldController.GridPosition;
            int level = oldController.Level;
            int goldInvested = oldController.TotalGoldInvested;
            TileSpecialty specialty = oldController.TileSpecialty;
            DynamicTileData dynamicTile = oldController.DynamicTile;
            Vector3 worldPos = oldController.transform.position;
            int savedInstanceId = oldController.InstanceId;

            // Destroy old turret GO
            _turrets.Remove(savedInstanceId);
            _turretSpatialDirty = true;
            Destroy(oldController.gameObject);

            // Spawn evolved prefab
            var newGO = Instantiate(evolvedPrefab, _turretParent);
            newGO.transform.position = worldPos;

            var newController = newGO.GetComponent<TurretController>();
            if (newController == null)
                newController = newGO.AddComponent<TurretController>();

            // Reuse same instance ID so UI/events still reference it correctly
            newController.InstanceId = savedInstanceId;

            // Initialize with same data + specialty
            newController.Initialize(data, gridPos, specialty, dynamicTile);

            // Restore level and gold invested
            newController.RestoreState(level, goldInvested, evolutionPath);

            // Register back in tracking
            _turrets[savedInstanceId] = newController;
            _turretSpatialDirty = true;
            MarkSupportAurasDirty();
            _grid.PlaceTurret(gridPos, savedInstanceId);

            _activeTurrets.RemoveAll(t => t == null || t.InstanceId == savedInstanceId);
            _activeTurrets.Add(newController);

            if (publishEvent)
            {
                EventBus.Publish(new TurretEvolvedEvent
                {
                    TurretId = savedInstanceId,
                    EvolutionPath = evolutionPath
                });

                Debug.Log($"[TurretManager] Evolved {data.DisplayName} -> path {evolutionPath} ({evo.Name})");
            }

            return newController;
        }

        // =================================================================
        // SELL
        // =================================================================

        public int SellTurret(int instanceId)
        {
            if (!_turrets.TryGetValue(instanceId, out var turret)) return 0;

            int refund = turret.GetSellValue();
            _grid.RemoveTurret(turret.GridPosition);
            _turrets.Remove(instanceId);
            _turretSpatialDirty = true;
            MarkSupportAurasDirty();

            var sellVFX = turret.GetComponent<TurretVFXConfig>();
            var sellSound = turret.GetComponent<TurretSoundConfig>();
            sellVFX?.SpawnSell();
            sellSound?.PlaySell();

            Destroy(turret.gameObject);

            _pathfinder.RecalculatePath();

            EventBus.Publish(new TurretSoldEvent
            {
                TurretId = instanceId,
                RefundAmount = refund
            });

            return refund;
        }

        // =================================================================
        // QUERIES
        // =================================================================

        public TurretController GetTurret(int instanceId)
        {
            return _turrets.TryGetValue(instanceId, out var t) ? t : null;
        }

        public void GetTurretsInRange(Vector3 center, float radius, List<TurretController> results)
        {
            results.Clear();

            if (_turrets.Count == 0)
                return;

            // For tiny turret counts, a direct scan is cheaper than bucket setup.
            if (_turrets.Count < 12)
            {
                float directSqrRadius = radius * radius;
                foreach (var turret in _turrets.Values)
                {
                    if (turret == null) continue;

                    Vector3 delta = turret.transform.position - center;
                    delta.y = 0f;
                    if (delta.sqrMagnitude <= directSqrRadius)
                        results.Add(turret);
                }
                return;
            }

            RebuildTurretSpatialIndexIfNeeded();

            float sqrRadius = radius * radius;
            int minX = WorldToTurretSpatialCell(center.x - radius);
            int maxX = WorldToTurretSpatialCell(center.x + radius);
            int minZ = WorldToTurretSpatialCell(center.z - radius);
            int maxZ = WorldToTurretSpatialCell(center.z + radius);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (!_turretSpatialBuckets.TryGetValue(MakeTurretSpatialKey(x, z), out var bucket))
                        continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        TurretController turret = bucket[i];
                        if (turret == null) continue;

                        Vector3 delta = turret.transform.position - center;
                        delta.y = 0f;
                        if (delta.sqrMagnitude <= sqrRadius)
                            results.Add(turret);
                    }
                }
            }
        }

        private void RebuildTurretSpatialIndexIfNeeded()
        {
            if (!_turretSpatialDirty)
                return;

            _turretSpatialDirty = false;
            _turretSpatialCellSize = Mathf.Max(1f, _turretSpatialCellSize);
            _inverseTurretSpatialCellSize = 1f / _turretSpatialCellSize;

            for (int i = 0; i < _usedTurretSpatialKeys.Count; i++)
            {
                if (_turretSpatialBuckets.TryGetValue(_usedTurretSpatialKeys[i], out var bucket))
                    bucket.Clear();
            }
            _usedTurretSpatialKeys.Clear();

            foreach (var turret in _turrets.Values)
            {
                if (turret == null) continue;

                Vector3 pos = turret.transform.position;
                long key = MakeTurretSpatialKey(WorldToTurretSpatialCell(pos.x), WorldToTurretSpatialCell(pos.z));

                if (!_turretSpatialBuckets.TryGetValue(key, out var bucket))
                {
                    bucket = new List<TurretController>(4);
                    _turretSpatialBuckets.Add(key, bucket);
                }

                if (bucket.Count == 0)
                    _usedTurretSpatialKeys.Add(key);

                bucket.Add(turret);
            }
        }

        private int WorldToTurretSpatialCell(float value)
        {
            return Mathf.FloorToInt(value * _inverseTurretSpatialCellSize);
        }

        private static long MakeTurretSpatialKey(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        // =================================================================
        // BUFFS / DEBUFFS
        // =================================================================

        //private void OnEnemyDebuffAura(EnemyDebuffAuraEvent evt)
        //{
        //    GetTurretsInRange(evt.Center, evt.Radius, _tempTurretList);
        //    for (int i = 0; i < _tempTurretList.Count; i++)
        //        _tempTurretList[i].ApplyExternalBuff(-evt.DebuffPercent, false, false);
        //}
        private void OnEnemyDebuffAura(EnemyDebuffAuraEvent evt)
        {
            GetTurretsInRange(evt.Center, evt.Radius, _tempTurretList);

            for (int i = 0; i < _tempTurretList.Count; i++)
            {
                if (_tempTurretList[i] == null)
                    continue;

                _tempTurretList[i].ApplyEnemyDamageDebuff(evt.DebuffPercent, 0.20f);
            }
        }

        private void OnSpecCardChosenAfter(SpecCardChosenAfterEvent evt)
        {
            // Recalculate every placed turret with the new spec bonuses
            for (int i = 0; i < _activeTurrets.Count; i++)
            {
                if (_activeTurrets[i] != null)
                    _activeTurrets[i].RecalculateStats();
            }

            // Support aura multiplier/radius can be changed by run modifiers.
            MarkSupportAurasDirty();
        }

        /// <summary>
        /// Recalculates every placed turret and refreshes support auras. Called after a
        /// run is restored, once ALL run state (traits, spec bonuses, permanent bonuses,
        /// wave, TurretsPlaced, TotalGoldSpent) is in place. Turrets restored mid-load ran
        /// RecalculateStats while those values were still stale (e.g. wave 0), which made
        /// wave-scaling (Infinite Scaling) and per-owned-turret traits look disabled after
        /// resume until the next upgrade or wave.
        /// </summary>
        public void RecalculateAllTurretsAndRefreshAuras()
        {
            for (int i = 0; i < _activeTurrets.Count; i++)
            {
                if (_activeTurrets[i] != null)
                    _activeTurrets[i].RecalculateStats();
            }

            MarkSupportAurasDirty();
        }

        private void OnTurretPlacedInternal(TurretPlacedEvent evt)
        {
            var turret = GetTurret(evt.TurretId);
            if (turret != null && !_activeTurrets.Contains(turret))
                _activeTurrets.Add(turret);
        }

        private void OnTurretSoldInternal(TurretSoldEvent evt)
        {
            _activeTurrets.RemoveAll(t => t == null || t.InstanceId == evt.TurretId);
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        public void ClearAll()
        {
            foreach (var turret in _turrets.Values)
                if (turret != null) Destroy(turret.gameObject);
            _turrets.Clear();
            _turretSpatialDirty = true;
            MarkSupportAurasDirty();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<EnemyDebuffAuraEvent>(OnEnemyDebuffAura);
            ServiceLocator.Unregister<TurretManager>();
            EventBus.Unsubscribe<SpecCardChosenAfterEvent>(OnSpecCardChosenAfter);
            EventBus.Unsubscribe<TurretPlacedEvent>(OnTurretPlacedInternal);
            EventBus.Unsubscribe<TurretSoldEvent>(OnTurretSoldInternal);
        }
    }
}
