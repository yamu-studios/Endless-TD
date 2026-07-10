// ============================================================================
// ETD.Core - ChainLightningHitBatcher.cs
// Aggregates lightning-chain telemetry once per rendered frame.
// v5: flushes directly into a telemetry sink and uses split profiler markers by
// default. The old outer ChainLightningHitBatcher.Flush wrapper is optional,
// because nested markers can be hidden depending on Unity Profiler view/settings.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace ETD.Core
{
    public interface IChainLightningHitTelemetrySink
    {
        void OnChainLightningHitBatch(int count, List<int> enemyIds);
    }

    public sealed class ChainLightningHitBatcher : MonoBehaviour
    {
        private const int InitialCapacity = 128;
        private const int InitialStampCapacity = 1024;
        private static ChainLightningHitBatcher _instance;

        public static bool WantsEnemyIds = true;

        [Header("Profiling")]
        [Tooltip("Keep disabled while diagnosing spikes. When disabled, ChainFlush.* markers appear directly in the Profiler instead of being hidden under one parent sample.")]
        [SerializeField] private bool _profileOuterFlushTotal = false;

        [Tooltip("Enable while profiling to show the internal cost split. Disable for final release builds if you want zero marker overhead.")]
        [SerializeField] private bool _profileFlushDetails = true;

        private readonly List<int> _uniqueEnemyIds = new(InitialCapacity);
        private int[] _seenEnemyIdStamps = new int[InitialStampCapacity];
        private int _seenStamp = 1;
        private int _hitCount;

        private IChainLightningHitTelemetrySink _cachedTelemetrySink;
        private bool _sinkResolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            WantsEnemyIds = true;
        }

        public static void InvalidateTelemetrySink()
        {
            if (_instance == null)
                return;

            _instance._sinkResolved = false;
            _instance._cachedTelemetrySink = null;
        }

        public static void ReportCount(int hitCount)
        {
            if (hitCount <= 0)
                return;

            ChainLightningHitBatcher instance = EnsureInstance();
            instance._hitCount += hitCount;
        }

        public static void Report(int enemyId)
        {
            if (enemyId < 0)
                return;

            ChainLightningHitBatcher instance = EnsureInstance();
            instance._hitCount++;
            if (WantsEnemyIds)
                instance.AddUniqueEnemyId(enemyId);
        }

        public static void Report(List<int> enemyIds)
        {
            if (enemyIds == null || enemyIds.Count == 0)
                return;

            ChainLightningHitBatcher instance = EnsureInstance();
            if (!WantsEnemyIds)
            {
                instance._hitCount += enemyIds.Count;
                return;
            }

            for (int i = 0; i < enemyIds.Count; i++)
            {
                int enemyId = enemyIds[i];
                if (enemyId < 0)
                    continue;

                instance._hitCount++;
                instance.AddUniqueEnemyId(enemyId);
            }
        }

        private void AddUniqueEnemyId(int enemyId)
        {
            EnsureStampCapacity(enemyId);
            if (_seenEnemyIdStamps[enemyId] == _seenStamp)
                return;

            _seenEnemyIdStamps[enemyId] = _seenStamp;
            _uniqueEnemyIds.Add(enemyId);
        }

        private void EnsureStampCapacity(int enemyId)
        {
            if (enemyId < _seenEnemyIdStamps.Length)
                return;

            int newSize = _seenEnemyIdStamps.Length;
            while (newSize <= enemyId)
                newSize <<= 1;

            System.Array.Resize(ref _seenEnemyIdStamps, newSize);
        }

        private static ChainLightningHitBatcher EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            var go = new GameObject("ChainLightningHitBatcher");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ChainLightningHitBatcher>();
            return _instance;
        }

        private IChainLightningHitTelemetrySink GetTelemetrySink()
        {
            if (_sinkResolved)
                return _cachedTelemetrySink;

            _sinkResolved = true;
            if (ServiceLocator.TryGet(out IChainLightningHitTelemetrySink sink))
                _cachedTelemetrySink = sink;
            else
                _cachedTelemetrySink = null;

            return _cachedTelemetrySink;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void LateUpdate()
        {
            if (_hitCount <= 0)
                return;

            bool profileOuterTotal = _profileOuterFlushTotal;
            bool profileDetails = _profileFlushDetails;
            if (profileOuterTotal)
                Profiler.BeginSample("ChainLightningHitBatcher.Flush");

            int count = _hitCount;
            List<int> enemyIds = WantsEnemyIds ? _uniqueEnemyIds : null;

            if (profileDetails) Profiler.BeginSample("ChainFlush.01.ResolveSink");
            IChainLightningHitTelemetrySink sink = GetTelemetrySink();
            if (profileDetails) Profiler.EndSample();

            if (sink != null)
            {
                // In split-marker mode, do not wrap the sink call. This lets
                // ChallengeTracker.ChainBatch.* appear as separate top-level samples
                // instead of being hidden under another parent marker.
                bool profileSinkWrapper = profileDetails && profileOuterTotal;
                if (profileSinkWrapper) Profiler.BeginSample("ChainFlush.02.DirectTelemetrySink");
                sink.OnChainLightningHitBatch(count, enemyIds);
                if (profileSinkWrapper) Profiler.EndSample();
            }

            // FIX (achievements): ALWAYS publish the batch event, not only when no
            // sink exists. The direct sink call above is just a fast path for
            // ChallengeTracker; passive listeners (SteamAchievementManager counts
            // chain hits for ACH_LIGHTNING_NETWORK / ACH_ALL_EFFECTS) rely on this
            // event. It fires at most once per frame, so the cost is negligible.
            if (profileDetails) Profiler.BeginSample("ChainFlush.03.EventBusPublish");
            EventBus.Publish(new ChainLightningHitBatchEvent
            {
                Count = count,
                EnemyIds = enemyIds
            });
            if (profileDetails) Profiler.EndSample();

            if (profileDetails) Profiler.BeginSample("ChainFlush.05.ResetCounters");
            _hitCount = 0;
            if (_uniqueEnemyIds.Count > 0)
            {
                _uniqueEnemyIds.Clear();

                _seenStamp++;
                if (_seenStamp == int.MaxValue)
                {
                    System.Array.Clear(_seenEnemyIdStamps, 0, _seenEnemyIdStamps.Length);
                    _seenStamp = 1;
                }
            }
            if (profileDetails) Profiler.EndSample();

            if (profileOuterTotal)
                Profiler.EndSample();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
