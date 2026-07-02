// ============================================================================
// ETD.Core - ProjectileSpawnEventBatcher.cs
// Aggregates projectile-fire telemetry and dispatches it directly to an optional
// sink. EventBus remains a compatibility fallback for legacy scenes.
// ============================================================================
using UnityEngine;
using UnityEngine.Profiling;

namespace ETD.Core
{
    public sealed class ProjectileSpawnEventBatcher : MonoBehaviour
    {
        private static int _pendingCount;
        private static ProjectileSpawnEventBatcher _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _pendingCount = 0;
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (_instance != null)
                return;

            GameObject go = new GameObject("Projectile Spawn Event Batcher");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ProjectileSpawnEventBatcher>();
        }

        public static void Report(int count)
        {
            if (count <= 0)
                return;

            _pendingCount = _pendingCount > int.MaxValue - count
                ? int.MaxValue
                : _pendingCount + count;
        }

        private void LateUpdate()
        {
            if (_pendingCount <= 0)
                return;

            int count = _pendingCount;
            _pendingCount = 0;

            Profiler.BeginSample("ProjectileSpawnEventBatcher.Flush");
            // ChallengeTracker registers itself as this Core-level interface. This
            // avoids generic EventBus lookup/delegate invocation every render frame.
            if (ServiceLocator.TryGet<IProjectileSpawnTelemetrySink>(out IProjectileSpawnTelemetrySink sink))
                sink.ReportProjectileSpawns(count);
            else
                EventBus.Publish(new ProjectileSpawnedBatchEvent { Count = count });
            Profiler.EndSample();
        }

        private void OnApplicationQuit()
        {
            _pendingCount = 0;
        }
    }
}
