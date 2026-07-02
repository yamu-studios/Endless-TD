// ============================================================================
// ETD.Data - WaveData.cs
// ============================================================================
using UnityEngine;

namespace ETD.Data
{
    [System.Serializable]
    public class EnemySpawnEntry
    {
        public EnemyData EnemyData;
        public EnemyTier Tier;
        public int Count = 1;
        public float SpawnDelay = 0.5f;
    }

    [CreateAssetMenu(fileName = "New Wave", menuName = "ETD/Wave Data")]
    public class WaveData : ScriptableObject
    {
        public int WaveNumber;
        public EnemySpawnEntry[] SpawnEntries;
        public float PrepTime = 15f;
    }
}
