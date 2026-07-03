// ============================================================================
// ETD.Grid - GridSystem.cs
// Dynamic grid system that reads tile GameObjects from the scene.
// Works with any grid size/shape. Tiles are auto-discovered from children
// of the grid parent. Corridors are identified by tag.
// Neighbors are found by spatial proximity (adjacent tile positions).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;

namespace ETD.Grid
{
    public enum CellState
    {
        Empty,
        Occupied,
        Corridor,
        Blocked
    }

    public class GridCell
    {
        public Vector2Int GridPos;
        public CellState State;
        public TileSpecialty Specialty;
        public DynamicTileData DynamicTile;
        public int TurretInstanceId = -1;
        public GameObject TileObject;
        public Renderer TileRenderer;
        public Vector3 WorldCenter;
        public List<Vector2Int> Neighbors = new(4);

        public bool IsPlaceable => State == CellState.Empty;
        public bool IsWalkable => State == CellState.Empty || State == CellState.Corridor;
    }

    public class GridSystem : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform _gridParent;

        [Header("Tile Identification")]
        [SerializeField] private string _corridorTag = "Corridor";
        [SerializeField] private string _entryTag = "Entry";
        [SerializeField] private string _exitTag = "Exit";

        [Header("Neighbor Detection")]
        [Tooltip("Max distance between tile centers to be considered neighbors")]
        [SerializeField] private float _neighborThreshold = 1.5f;

        private bool _pathfinderMissingLogged;

        // Dictionary-based storage instead of 2D array — supports any grid shape
        private readonly Dictionary<Vector2Int, GridCell> _cells = new();
        private readonly List<Vector2Int> _allPositions = new();

        // Entry/exit — supports multiple entry/exit points
        private readonly List<Vector2Int> _entryPoints = new();
        private readonly List<Vector2Int> _exitPoints = new();

        public IReadOnlyDictionary<Vector2Int, GridCell> Cells => _cells;
        public IReadOnlyList<Vector2Int> AllPositions => _allPositions;
        public IReadOnlyList<Vector2Int> EntryPoints => _entryPoints;
        public IReadOnlyList<Vector2Int> ExitPoints => _exitPoints;

        // Convenience — first entry/exit for simple maps
        public Vector2Int EntryPoint => _entryPoints.Count > 0 ? _entryPoints[0] : Vector2Int.zero;
        public Vector2Int ExitPoint => _exitPoints.Count > 0 ? _exitPoints[0] : Vector2Int.zero;

        public void Initialize()
        {
            DiscoverTiles();
            BuildNeighborGraph();
            
            ServiceLocator.Register(this);
        }

        // =================================================================
        // DISCOVERY — reads tiles from scene hierarchy
        // =================================================================

        private void DiscoverTiles()
        {
            _cells.Clear();
            _allPositions.Clear();
            _entryPoints.Clear();
            _exitPoints.Clear();

            if (_gridParent == null)
            {
                Debug.LogError("[GridSystem] Grid parent not assigned!");
                return;
            }

            for (int i = 0; i < _gridParent.childCount; i++)
            {
                var child = _gridParent.GetChild(i);
                Vector2Int gridPos = WorldToGrid(child.position);

                // Skip duplicates
                if (_cells.ContainsKey(gridPos))
                {
                    Debug.LogWarning($"[GridSystem] Duplicate tile at {gridPos}, skipping {child.name}");
                    continue;
                }

                // Determine cell state from tags
                CellState state = CellState.Empty;
                bool isEntry = false;
                bool isExit = false;

                if (child.CompareTag(_corridorTag))
                    state = CellState.Corridor;
                if (child.CompareTag(_entryTag))
                {
                    state = CellState.Corridor;
                    isEntry = true;
                }
                if (child.CompareTag(_exitTag))
                {
                    state = CellState.Corridor;
                    isExit = true;
                }

                var cell = new GridCell
                {
                    GridPos = gridPos,
                    State = state,
                    Specialty = TileSpecialty.None,
                    TurretInstanceId = -1,
                    TileObject = child.gameObject,
                    TileRenderer = child.GetComponent<Renderer>(),
                    WorldCenter = child.position
                };

                _cells[gridPos] = cell;
                _allPositions.Add(gridPos);

                if (isEntry) _entryPoints.Add(gridPos);
                if (isExit) _exitPoints.Add(gridPos);
            }

            Debug.Log($"[GridSystem] Discovered {_cells.Count} tiles, " +
                      $"{_entryPoints.Count} entries, {_exitPoints.Count} exits");
        }

        // =================================================================
        // NEIGHBOR GRAPH — built from spatial proximity
        // =================================================================

        private void BuildNeighborGraph()
        {
            float sqrThreshold = _neighborThreshold * _neighborThreshold;

            for (int i = 0; i < _allPositions.Count; i++)
            {
                var posA = _allPositions[i];
                var cellA = _cells[posA];

                for (int j = i + 1; j < _allPositions.Count; j++)
                {
                    var posB = _allPositions[j];
                    var cellB = _cells[posB];

                    float sqrDist = (cellA.WorldCenter - cellB.WorldCenter).sqrMagnitude;
                    if (sqrDist <= sqrThreshold)
                    {
                        cellA.Neighbors.Add(posB);
                        cellB.Neighbors.Add(posA);
                    }
                }
            }
        }

        // =================================================================
        // TILE SPECIALTIES — applied at run start
        // =================================================================
        public bool CanPlaceTile(Vector2Int gridPos)
        {
            if (!CanPlace(gridPos)) return false;
            return !WouldBlockPath(gridPos);
        }
        public void ApplyRandomSpecialties(System.Random rng, GameDatabase database)
        {
            if (database?.DynamicTiles == null || database.DynamicTiles.Length == 0) return;

            var blessings = database.GetTilesByCategory(TileSpecialty.Blessed);
            var curses = database.GetTilesByCategory(TileSpecialty.Cursed);
            var greeds = database.GetTilesByCategory(TileSpecialty.Greed);

            for (int i = 0; i < _allPositions.Count; i++)
            {
                var cell = _cells[_allPositions[i]];
                if (cell.State != CellState.Empty) continue;
                if (rng.NextDouble() > GameConstants.TILE_SPECIALTY_CHANCE) continue;
               
                if (cell.GridPos == new Vector2Int(-6,4) || cell.GridPos == new Vector2Int(13,4)) continue;
                // Pick category: 40% blessing, 35% curse, 25% greed
                double roll = rng.NextDouble();
                DynamicTileData[] pool;
                TileSpecialty category;

                if (roll < 0.4)
                {
                    pool = blessings;
                    category = TileSpecialty.Blessed;
                }
                else if (roll < 0.75)
                {
                    pool = curses;
                    category = TileSpecialty.Cursed;
                }
                else
                {
                    pool = greeds;
                    category = TileSpecialty.Greed;
                }

                if (pool.Length == 0) continue;

                var tile = pool[rng.Next(pool.Length)];
                cell.Specialty = category;
                cell.DynamicTile = tile;

                // Apply visual material
                if (tile.TileMaterial != null && cell.TileRenderer != null)
                    cell.TileRenderer.material = tile.TileMaterial;
                EventBus.Publish(new TileSpecialtyAppliedEvent
                {
                    GridPos = cell.GridPos,
                    Specialty = (int)category
                });
            }
        }

        // =================================================================
        // QUERIES
        // =================================================================

        public bool HasCell(Vector2Int pos) => _cells.ContainsKey(pos);

        public GridCell GetCell(Vector2Int pos)
        {
            return _cells.TryGetValue(pos, out var cell) ? cell : null;
        }

        public GridCell GetCell(int x, int y) => GetCell(new Vector2Int(x, y));

        public bool CanPlace(Vector2Int pos)
        {
            return _cells.TryGetValue(pos, out var cell) && cell.IsPlaceable;
        }

        public bool IsWalkable(Vector2Int pos)
        {
            return _cells.TryGetValue(pos, out var cell) && cell.IsWalkable;
        }

        /// <summary>
        /// Overload for pathfinder compatibility
        /// </summary>
        public bool IsWalkable(int x, int y) => IsWalkable(new Vector2Int(x, y));

        public List<Vector2Int> GetNeighbors(Vector2Int pos)
        {
            if (_cells.TryGetValue(pos, out var cell))
                return cell.Neighbors;
            return new List<Vector2Int>(0);
        }

        // =================================================================
        // TURRET PLACEMENT / REMOVAL
        // =================================================================

        public void SetCellState(Vector2Int pos, CellState state)
        {
            if (_cells.TryGetValue(pos, out var cell))
                cell.State = state;
        }

        public void PlaceTurret(Vector2Int pos, int turretInstanceId)
        {
            if (!_cells.TryGetValue(pos, out var cell)) return;
            cell.State = CellState.Occupied;
            cell.TurretInstanceId = turretInstanceId;
        }

        public void RemoveTurret(Vector2Int pos)
        {
            if (!_cells.TryGetValue(pos, out var cell)) return;
            cell.State = CellState.Empty;
            cell.TurretInstanceId = -1;
        }

        // =================================================================
        // COORDINATE CONVERSION
        // =================================================================

        /// <summary>
        /// Converts world position to grid coordinates.
        /// Uses X and Z axes, rounded to nearest int.
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.RoundToInt(worldPos.x),
                Mathf.RoundToInt(worldPos.z)
            );
        }

        /// <summary>
        /// Returns the world center of a grid cell.
        /// Reads from the actual tile object position.
        /// </summary>
        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            if (_cells.TryGetValue(gridPos, out var cell))
                return cell.WorldCenter;

            // Fallback: reconstruct from grid pos
            return new Vector3(gridPos.x, 0f, gridPos.y);
        }

        /// <summary>
        /// Finds the nearest valid grid cell to a world position.
        /// Useful for mouse raycast → grid snapping.
        /// </summary>
        public Vector2Int WorldToNearestCell(Vector3 worldPos)
        {
            Vector2Int nearest = WorldToGrid(worldPos);
            if (_cells.ContainsKey(nearest)) return nearest;

            // If exact pos doesn't match a cell, find closest
            float bestDist = float.MaxValue;
            Vector2Int bestPos = nearest;

            for (int i = 0; i < _allPositions.Count; i++)
            {
                float dist = (GridToWorld(_allPositions[i]) - worldPos).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestPos = _allPositions[i];
                }
            }
            return bestPos;
        }

        // =================================================================
        // PATH VALIDATION
        // =================================================================

        /// <summary>
        /// Tests if placing a turret would block all paths from entries to exits.
        /// Uses the IAStarPathfinder interface from Core to avoid circular dependency.
        /// </summary>
        public bool WouldBlockPath(Vector2Int testPos)
        {
            if (!CanPlace(testPos)) return true;

            // Temporarily block
            var cell = _cells[testPos];
            var originalState = cell.State;
            cell.State = CellState.Occupied;

            bool pathExists = true;
            if (ServiceLocator.TryGet<IAStarPathfinder>(out var pathfinder))
            {
                // Check that at least one entry→exit path still exists
                pathExists = false;
                for (int e = 0; e < _entryPoints.Count && !pathExists; e++)
                {
                    for (int x = 0; x < _exitPoints.Count && !pathExists; x++)
                    {
                        pathExists = pathfinder.HasPath(_entryPoints[e], _exitPoints[x]);
                    }
                }
            }
            else if (!_pathfinderMissingLogged)
            {
                // BUG HISTORY: a duplicate ETD.Core.IAStarPathfinder declaration in
                // the ETD.Pathfinding assembly once made this lookup silently miss
                // (two distinct Types with the same name), which disabled path-block
                // validation entirely — turrets could wall off the map. If this
                // error ever appears, the pathfinder is registering under a
                // different type than this assembly resolves.
                _pathfinderMissingLogged = true;
                Debug.LogError("[GridSystem] WouldBlockPath: no IAStarPathfinder registered — path-block validation is NOT running, placements are unrestricted!");
            }

            // Restore
            cell.State = originalState;
            return !pathExists;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<GridSystem>();
        }
    }
}
