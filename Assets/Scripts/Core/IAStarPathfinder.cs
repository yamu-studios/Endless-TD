using UnityEngine;

namespace ETD.Core
{
    public interface IAStarPathfinder
    {
        bool HasPath(Vector2Int start, Vector2Int end);
    }
}