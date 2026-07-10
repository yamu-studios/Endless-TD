// ============================================================================
// ETD.Utilities - GameUtilities.cs
// Common utility methods, extensions, and helpers.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace ETD.Utilities
{
    public static class GameUtilities
    {
        /// <summary>
        /// Format seconds into MM:SS or HH:MM:SS
        /// </summary>
        public static string FormatTime(float seconds)
        {
            int totalSec = Mathf.FloorToInt(seconds);
            int hours = totalSec / 3600;
            int mins = (totalSec % 3600) / 60;
            int secs = totalSec % 60;

            return hours > 0
                ? $"{hours:D2}:{mins:D2}:{secs:D2}"
                : $"{mins:D2}:{secs:D2}";
        }

        /// <summary>
        /// Format large numbers with K/M suffixes
        /// </summary>
        public static string FormatNumber(int number)
        {
            // Delegate to the canonical formatter (K/M/B/T/Qa..Dc, then scientific).
            return ETD.Core.NumberFormat.Compact(number);
        }

        /// <summary>
        /// Weighted random selection from a list
        /// </summary>
        public static T WeightedRandom<T>(List<T> items, System.Func<T, float> weightFunc)
        {
            float totalWeight = 0f;
            for (int i = 0; i < items.Count; i++)
                totalWeight += weightFunc(items[i]);

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                cumulative += weightFunc(items[i]);
                if (roll <= cumulative)
                    return items[i];
            }
            return items[items.Count - 1];
        }

        /// <summary>
        /// Fisher-Yates shuffle
        /// </summary>
        public static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    /// <summary>
    /// Reusable cooldown timer to avoid repeated time checks
    /// </summary>
    public struct CooldownTimer
    {
        private float _endTime;

        public bool IsReady => Time.time >= _endTime;
        public float Remaining => Mathf.Max(0f, _endTime - Time.time);
        public float RemainingNormalized(float duration) =>
            duration > 0 ? Remaining / duration : 0f;

        public void Start(float duration)
        {
            _endTime = Time.time + duration;
        }

        public void Reset()
        {
            _endTime = 0f;
        }
    }

    /// <summary>
    /// Simple spatial hash for fast neighbor queries on the grid.
    /// Alternative to O(n) enemy scans for large enemy counts.
    /// </summary>
    public class SpatialHash<T> where T : class
    {
        private readonly Dictionary<int, List<T>> _buckets = new();
        private readonly float _cellSize;

        public SpatialHash(float cellSize = 2f)
        {
            _cellSize = cellSize;
        }

        public void Clear()
        {
            foreach (var bucket in _buckets.Values)
                bucket.Clear();
        }

        public void Insert(T item, Vector3 position)
        {
            int key = GetKey(position);
            if (!_buckets.TryGetValue(key, out var list))
            {
                list = new List<T>(8);
                _buckets[key] = list;
            }
            list.Add(item);
        }

        public void Query(Vector3 center, float radius, List<T> results)
        {
            results.Clear();
            int minX = Mathf.FloorToInt((center.x - radius) / _cellSize);
            int maxX = Mathf.FloorToInt((center.x + radius) / _cellSize);
            int minZ = Mathf.FloorToInt((center.z - radius) / _cellSize);
            int maxZ = Mathf.FloorToInt((center.z + radius) / _cellSize);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    int key = x * 73856093 ^ z * 19349663;
                    if (_buckets.TryGetValue(key, out var list))
                    {
                        for (int i = 0; i < list.Count; i++)
                            results.Add(list[i]);
                    }
                }
            }
        }

        private int GetKey(Vector3 pos)
        {
            int x = Mathf.FloorToInt(pos.x / _cellSize);
            int z = Mathf.FloorToInt(pos.z / _cellSize);
            return x * 73856093 ^ z * 19349663;
        }
    }
}
