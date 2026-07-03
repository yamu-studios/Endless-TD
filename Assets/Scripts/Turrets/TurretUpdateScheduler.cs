// ============================================================================
// ETD.Turrets - TurretUpdateScheduler.cs
// Centralized, due-time based update runner for turrets.
//
// Performance notes:
// - TurretController does not have a per-turret Unity Update().
// - The scheduler no longer scans every turret every frame.
// - A binary min-heap stores the next due turret at the root, so Update() only
//   touches turrets that actually need managed work this frame.
// - Each turret receives elapsed dt since its previous managed tick, so attack
//   timers remain accurate while expensive searches are throttled.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace ETD.Turrets
{
    public sealed class TurretUpdateScheduler : MonoBehaviour
    {
        private static TurretUpdateScheduler _instance;

        // FIX (trait-reset / turret-skip bug #2): These were `static readonly` lists,
        // meaning they survive a scene reload (Hub -> Game -> Hub -> Game). On the
        // second run the lists still held references to turrets from the PREVIOUS
        // scene, all of which are null/destroyed. The scheduler would then skip those
        // dead slots, effectively never reaching real turrets — causing "turrets not
        // shooting" and "traits stop working" symptoms reported by players.
        //
        // Changed from `static readonly` to `static` so OnDestroy can null-and-replace
        // them with fresh instances, and EnsureInstance always creates a clean scheduler.
        private static List<TurretController> _heapTurrets = new(128);
        private static List<float> _nextUpdateTimes = new(128);
        private static List<float> _lastUpdateTimes = new(128);
        private static Dictionary<TurretController, int> _indices = new(128);

        [Header("Performance Budget")]
        [Tooltip("Hard cap for managed turret ticks processed in one frame. Prevents one bad frame from updating every overdue turret at once.")]
        [SerializeField] private int _maxManagedUpdatesPerFrame = 192;

        [Tooltip("Approximate frame-time budget in milliseconds for managed turret work. Only checked every few turrets to keep overhead low.")]
        [SerializeField] private float _maxManagedUpdateMilliseconds = 1.25f;

        [Tooltip("Minimum interval used if a turret reports a very small/invalid interval.")]
        [SerializeField] private float _minimumInterval = 0.01f;

        public static int RegisteredCount => _heapTurrets.Count;

        public static void Register(TurretController turret)
        {
            if (turret == null)
                return;

            EnsureInstance();

            if (_indices.ContainsKey(turret))
                return;

            int index = _heapTurrets.Count;
            float now = Time.time;
            float interval = Mathf.Max(_instance != null ? _instance._minimumInterval : 0.01f, turret.GetManagedUpdateInterval());

            // Deterministic stagger prevents newly restored/placed turrets from
            // all scanning enemies on the same frame.
            float stagger = ((index % 47) / 47f) * interval;

            _indices.Add(turret, index);
            _heapTurrets.Add(turret);
            _lastUpdateTimes.Add(now);
            _nextUpdateTimes.Add(now + stagger);
            SiftUp(index);
        }

        public static void Unregister(TurretController turret)
        {
            if (turret == null)
                return;

            if (!_indices.TryGetValue(turret, out int index))
                return;

            RemoveAt(index);
        }

        private void Awake()
        {
            // FIX (profiler-confirmed: Update() showed Calls=2): Game.unity contains
            // a scene-placed scheduler, but _instance was only ever assigned inside
            // EnsureInstance(). The scene instance ran without claiming the singleton,
            // so the first turret Register() spawned a SECOND scheduler. Both ticked
            // the same static heap every frame, doubling the per-frame managed-update
            // budget (~2x the intended turret CPU cost). Claim the singleton here and
            // destroy any duplicate.
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
                return;

            var go = new GameObject("Turret Update Scheduler");
            _instance = go.AddComponent<TurretUpdateScheduler>();
        }

        private void Update()
        {
            if (_heapTurrets.Count == 0)
                return;

            Profiler.BeginSample("TurretUpdateScheduler.Update - HeapDueOnly");

            float now = Time.time;
            int processed = 0;
            int maxUpdates = Mathf.Max(16, _maxManagedUpdatesPerFrame);
            float budgetSeconds = Mathf.Max(0.05f, _maxManagedUpdateMilliseconds) * 0.001f;
            float startRealtime = Time.realtimeSinceStartup;

            while (_heapTurrets.Count > 0)
            {
                if (_nextUpdateTimes[0] > now)
                    break;

                if (processed >= maxUpdates)
                    break;

                // Avoid calling Time.realtimeSinceStartup every turret; the call is
                // cheap but not free. Checking every 8 updates is enough for budget protection.
                if ((processed & 7) == 7 && Time.realtimeSinceStartup - startRealtime >= budgetSeconds)
                    break;

                TurretController turret = _heapTurrets[0];

                if (turret == null || !turret.isActiveAndEnabled)
                {
                    RemoveAt(0);
                    continue;
                }

                float dt = Mathf.Max(0.0001f, now - _lastUpdateTimes[0]);
                _lastUpdateTimes[0] = now;

                Profiler.BeginSample("TurretUpdateScheduler.ManagedTurretTick");
                turret.ManagedUpdate(dt, now);
                Profiler.EndSample();

                float interval = Mathf.Max(_minimumInterval, turret.GetManagedUpdateInterval());
                _nextUpdateTimes[0] = now + interval;
                SiftDown(0);

                processed++;
            }

            Profiler.EndSample();
        }

        private static void RemoveAt(int index)
        {
            int lastIndex = _heapTurrets.Count - 1;
            TurretController removed = _heapTurrets[index];
            _indices.Remove(removed);

            if (index != lastIndex)
            {
                TurretController moved = _heapTurrets[lastIndex];
                _heapTurrets[index] = moved;
                _nextUpdateTimes[index] = _nextUpdateTimes[lastIndex];
                _lastUpdateTimes[index] = _lastUpdateTimes[lastIndex];
                _indices[moved] = index;
            }

            _heapTurrets.RemoveAt(lastIndex);
            _nextUpdateTimes.RemoveAt(lastIndex);
            _lastUpdateTimes.RemoveAt(lastIndex);

            if (index < _heapTurrets.Count)
                HeapifyAt(index);
        }

        private static void HeapifyAt(int index)
        {
            int parent = (index - 1) >> 1;
            if (index > 0 && IsEarlier(index, parent))
                SiftUp(index);
            else
                SiftDown(index);
        }

        private static void SiftUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) >> 1;
                if (!IsEarlier(index, parent))
                    break;

                Swap(index, parent);
                index = parent;
            }
        }

        private static void SiftDown(int index)
        {
            int count = _heapTurrets.Count;

            while (true)
            {
                int left = (index << 1) + 1;
                if (left >= count)
                    break;

                int right = left + 1;
                int smallest = left;

                if (right < count && IsEarlier(right, left))
                    smallest = right;

                if (!IsEarlier(smallest, index))
                    break;

                Swap(index, smallest);
                index = smallest;
            }
        }

        private static bool IsEarlier(int a, int b)
        {
            float delta = _nextUpdateTimes[a] - _nextUpdateTimes[b];
            if (!Mathf.Approximately(delta, 0f))
                return delta < 0f;

            TurretController ta = _heapTurrets[a];
            TurretController tb = _heapTurrets[b];
            int ida = ta != null ? ta.InstanceId : int.MaxValue;
            int idb = tb != null ? tb.InstanceId : int.MaxValue;
            return ida < idb;
        }

        private static void Swap(int a, int b)
        {
            TurretController turret = _heapTurrets[a];
            _heapTurrets[a] = _heapTurrets[b];
            _heapTurrets[b] = turret;

            float next = _nextUpdateTimes[a];
            _nextUpdateTimes[a] = _nextUpdateTimes[b];
            _nextUpdateTimes[b] = next;

            float last = _lastUpdateTimes[a];
            _lastUpdateTimes[a] = _lastUpdateTimes[b];
            _lastUpdateTimes[b] = last;

            if (_heapTurrets[a] != null)
                _indices[_heapTurrets[a]] = a;
            if (_heapTurrets[b] != null)
                _indices[_heapTurrets[b]] = b;
        }

        private void OnDestroy()
        {
            // A destroyed DUPLICATE (see Awake) must not wipe the live scheduler's
            // state — only the owning instance resets the statics on scene unload.
            if (_instance != this)
                return;

            _instance = null;

            // FIX (bug #2 continued): Replace the static collections with fresh instances
            // so a subsequent scene load starts with a completely empty scheduler state.
            // The old code only called Clear() which left capacity-allocated buffers and
            // could still reference dead Unity objects through the dictionary.
            _heapTurrets = new List<TurretController>(128);
            _nextUpdateTimes = new List<float>(128);
            _lastUpdateTimes = new List<float>(128);
            _indices = new Dictionary<TurretController, int>(128);
        }
    }
}
