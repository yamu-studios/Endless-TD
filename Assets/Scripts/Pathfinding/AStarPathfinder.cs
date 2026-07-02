// ============================================================================
// ETD.Pathfinding - AStarPathfinder.cs
// Optimized A* with binary heap priority queue. Recalculates on turret place/sell.
// Uses grid reference from GridSystem.
//
// FIX (memory-leak #1): ReconstructPath no longer allocates a new List<Vector2Int>
// on every call. It instead writes into a caller-supplied list that enemies own
// and reuse. RecalculatePathLocal now accepts an output buffer.
// This eliminates O(activeEnemies × pathLength) heap allocations every time a
// turret is placed or sold, which was the primary cause of the 3 GB memory growth
// observed between wave 1 and wave 100.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Grid;

namespace ETD.Pathfinding
{
    public class AStarPathfinder : MonoBehaviour, IAStarPathfinder
    {
        private GridSystem _grid;

        // Reusable collections to avoid GC pressure on each FindPath call.
        private readonly Dictionary<Vector2Int, float> _gScore = new(256);
        private readonly Dictionary<Vector2Int, float> _fScore = new(256);
        private readonly Dictionary<Vector2Int, Vector2Int> _cameFrom = new(256);
        private readonly HashSet<Vector2Int> _closedSet = new(256);
        private readonly BinaryHeap _openSet = new(256);

        // Reusable scratch buffer for ReconstructPath. Avoids one allocation per call.
        private readonly List<Vector2Int> _reconstructScratch = new(64);

        // Cached path for enemies to reference
        private List<Vector2Int> _currentPath = new();
        public IReadOnlyList<Vector2Int> CurrentPath => _currentPath;
        public bool HasValidPath => _currentPath.Count > 0;

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        public void Initialize(GridSystem grid)
        {
            ServiceLocator.Register<IAStarPathfinder>(this);
            _grid = grid;
            ServiceLocator.Register(this);
            RecalculatePath();
        }

        public void RecalculatePath()
        {
            if (_currentPath == null)
                _currentPath = new List<Vector2Int>();

            _currentPath.Clear();
            if (FindPathInto(_grid.EntryPoint, _grid.ExitPoint, _currentPath) && _currentPath.Count > 0)
            {
                EventBus.Publish(new PathRecalculatedEvent { NewPath = _currentPath });
            }
        }

        /// <summary>
        /// Recalculates a local path from <paramref name="myGrid"/> to the exit and
        /// writes the result into <paramref name="outputPath"/>, which the caller owns
        /// and reuses. Returns true if a path was found.
        ///
        /// OLD signature (kept for compatibility — wraps the zero-alloc version):
        ///   public List&lt;Vector2Int&gt; RecalculatePathLocal(Vector2Int myGrid)
        /// NEW zero-alloc version:
        ///   public bool RecalculatePathLocal(Vector2Int myGrid, List&lt;Vector2Int&gt; outputPath)
        /// </summary>
        public bool RecalculatePathLocal(Vector2Int myGrid, List<Vector2Int> outputPath)
        {
            outputPath.Clear();
            return FindPathInto(myGrid, _grid.ExitPoint, outputPath);
        }

        /// <summary>
        /// Legacy overload that still allocates. Kept so existing call-sites outside
        /// EnemyManager compile without changes. EnemyManager itself uses the new
        /// zero-alloc overload.
        /// </summary>
        public List<Vector2Int> RecalculatePathLocal(Vector2Int myGrid)
        {
            var list = new List<Vector2Int>(32);
            FindPathInto(myGrid, _grid.ExitPoint, list);
            return list.Count > 0 ? list : null;
        }

        public bool HasPath(Vector2Int start, Vector2Int end)
        {
            var scratch = new List<Vector2Int>(4);
            return FindPathInto(start, end, scratch) && scratch.Count > 0;
        }

        /// <summary>
        /// Legacy allocating overload kept for call-sites outside EnemyManager.
        /// </summary>
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
        {
            var list = new List<Vector2Int>(32);
            FindPathInto(start, end, list);
            return list.Count > 0 ? list : null;
        }

        // =====================================================================
        // Core A* — writes into caller-supplied list, zero allocation on hot path.
        // =====================================================================

        private bool FindPathInto(Vector2Int start, Vector2Int end, List<Vector2Int> output)
        {
            _gScore.Clear();
            _fScore.Clear();
            _cameFrom.Clear();
            _closedSet.Clear();
            _openSet.Clear();

            _gScore[start] = 0;
            _fScore[start] = Heuristic(start, end);
            _openSet.Push(start, _fScore[start]);

            while (_openSet.Count > 0)
            {
                var current = _openSet.Pop();

                if (current == end)
                {
                    ReconstructPathInto(current, output);
                    return true;
                }

                _closedSet.Add(current);

                for (int i = 0; i < Directions.Length; i++)
                {
                    var neighbor = current + Directions[i];

                    if (_closedSet.Contains(neighbor)) continue;
                    if (!_grid.IsWalkable(neighbor.x, neighbor.y)) continue;

                    float tentativeG = _gScore[current] + 1f;

                    if (!_gScore.ContainsKey(neighbor) || tentativeG < _gScore[neighbor])
                    {
                        _cameFrom[neighbor] = current;
                        _gScore[neighbor] = tentativeG;
                        float f = tentativeG + Heuristic(neighbor, end);
                        _fScore[neighbor] = f;
                        _openSet.Push(neighbor, f);
                    }
                }
            }

            return false; // No path found
        }

        private void ReconstructPathInto(Vector2Int current, List<Vector2Int> output)
        {
            // Walk the came-from chain into the scratch buffer, then reverse into output.
            _reconstructScratch.Clear();
            _reconstructScratch.Add(current);
            while (_cameFrom.ContainsKey(current))
            {
                current = _cameFrom[current];
                _reconstructScratch.Add(current);
            }

            output.Clear();
            for (int i = _reconstructScratch.Count - 1; i >= 0; i--)
                output.Add(_reconstructScratch[i]);
        }

        private static float Heuristic(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<AStarPathfinder>();
        }
    }

    /// <summary>
    /// Simple binary min-heap for A* open set. Avoids SortedSet allocation overhead.
    /// </summary>
    public class BinaryHeap
    {
        private struct HeapNode
        {
            public Vector2Int Position;
            public float Priority;
        }

        private HeapNode[] _nodes;
        private int _count;

        public int Count => _count;

        public BinaryHeap(int capacity)
        {
            _nodes = new HeapNode[capacity];
            _count = 0;
        }

        public void Clear()
        {
            _count = 0;
        }

        public void Push(Vector2Int pos, float priority)
        {
            if (_count >= _nodes.Length)
                System.Array.Resize(ref _nodes, _nodes.Length * 2);

            _nodes[_count] = new HeapNode { Position = pos, Priority = priority };
            BubbleUp(_count);
            _count++;
        }

        public Vector2Int Pop()
        {
            var result = _nodes[0].Position;
            _count--;
            _nodes[0] = _nodes[_count];
            BubbleDown(0);
            return result;
        }

        private void BubbleUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_nodes[index].Priority >= _nodes[parent].Priority) break;
                (_nodes[index], _nodes[parent]) = (_nodes[parent], _nodes[index]);
                index = parent;
            }
        }

        private void BubbleDown(int index)
        {
            while (true)
            {
                int left = 2 * index + 1;
                int right = 2 * index + 2;
                int smallest = index;

                if (left < _count && _nodes[left].Priority < _nodes[smallest].Priority)
                    smallest = left;
                if (right < _count && _nodes[right].Priority < _nodes[smallest].Priority)
                    smallest = right;

                if (smallest == index) break;

                (_nodes[index], _nodes[smallest]) = (_nodes[smallest], _nodes[index]);
                index = smallest;
            }
        }
    }
}
