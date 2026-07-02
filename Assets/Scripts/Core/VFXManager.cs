// ============================================================================
// ETD.Core - VFXManager.cs  [UPDATED - no Turrets dependency]
// Central VFX spawner + global status VFX registry.
// Enemies look up status VFX by StatusEffectType.
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ETD.Core
{
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [Header("Pool Settings")]
        [SerializeField] private int _defaultPoolSize = 32;
        [SerializeField] private int _poolExpandBatchSize = 16;

        [Tooltip("Max instances created synchronously per frame during background prewarm. Keeps a first-time-prefab pool build-up from freezing a single frame.")]
        [SerializeField] private int _maxPrewarmPerFrame = 4;
        [SerializeField] private Transform _vfxParent;

        [Header("Global Status VFX (wire in Inspector)")]
        [SerializeField] private GameObject _slowStatusVFX;
        [SerializeField] private GameObject _burnStatusVFX;
        [SerializeField] private GameObject _chainStatusVFX;

        private readonly Dictionary<int, Queue<ParticleSystem>> _pools = new();
        private readonly Dictionary<int, GameObject> _prefabLookup = new();
        private Dictionary<StatusEffectType, GameObject> _statusVFXMap;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ServiceLocator.Register(this);

            _statusVFXMap = new Dictionary<StatusEffectType, GameObject>
            {
                [StatusEffectType.Slow] = _slowStatusVFX,
                [StatusEffectType.Freeze] = _slowStatusVFX,
                [StatusEffectType.Burn] = _burnStatusVFX,
                [StatusEffectType.ChainLightning] = _chainStatusVFX
            };
        }

        // =================================================================
        // STATUS VFX — enemies call this, no Turrets assembly needed
        // =================================================================

        public GameObject GetStatusVFX(StatusEffectType type)
            => _statusVFXMap.TryGetValue(type, out var vfx) ? vfx : null;

        // =================================================================
        // SPAWN — one-shot at position
        // =================================================================

        public ParticleSystem Spawn(GameObject prefab, Vector3 position,
            Quaternion? rotation = null, Transform parent = null)
        {
            if (prefab == null) return null;

            int id = prefab.GetInstanceID();
            var ps = GetFromPool(id, prefab);
            ps.transform.position = position;
            ps.transform.rotation = rotation ?? Quaternion.identity;
            ps.transform.SetParent(parent != null ? parent : _vfxParent);
            // FIX (profiler-confirmed, 46.12ms self / 13 calls in ActivateAwakeRecursively):
            // No SetActive(true) here. The GameObject is activated exactly once, the
            // moment it's first created (see CreateInstance), and never touched again
            // for its entire lifetime. Reuse is driven purely by ParticleSystem
            // Play/Stop state, which avoids Unity's recursive Awake/OnEnable/
            // SortingGroup activation walk on every single reuse.
            ps.Play(true);
            StartCoroutine(ReturnWhenDone(id, ps));
            return ps;
        }

        // =================================================================
        // SPAWN — attached to moving target
        // =================================================================

        public ParticleSystem SpawnAttached(GameObject prefab, Transform target,
            Vector3 localOffset = default)
        {
            if (prefab == null || target == null) return null;

            int id = prefab.GetInstanceID();
            var ps = GetFromPool(id, prefab);
            ps.transform.SetParent(target);
            ps.transform.localPosition = localOffset;
            ps.transform.localRotation = Quaternion.identity;
            // FIX: see Spawn() above — no SetActive(true) on reuse, ever.
            ps.Play(true);
            return ps;
        }

        public void StopAttached(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            int id = GetPrefabId(ps);
            ps.transform.SetParent(_vfxParent);
            StartCoroutine(ReturnAfterDelay(id, ps, 0.5f));
        }

        // =================================================================
        // POOL
        // =================================================================

        // FIX (profiler-confirmed regression, follow-up capture): The previous fix
        // (raise pool size + batch-expand) helped steady state but made the FIRST
        // time a given VFX prefab is ever needed worse: a single Prewarm() call now
        // synchronously ran 64 CreateInstance calls in one frame (403ms total frame,
        // 204.84ms self in Instantiate.Awake alone) instead of the smaller repeated
        // misses seen before. Turning "many small spikes" into "one huge spike" is
        // not actually a win the first time a prefab is encountered mid-run.
        //
        // Fix: the CALLER still gets an instance synchronously this frame (combat
        // must not stall waiting on VFX), but the REST of the target pool size is
        // built up in the background across multiple frames, capped at
        // _maxPrewarmPerFrame CreateInstance calls per frame. Same idea applies to
        // expand-on-miss: hand back one instance now, grow the rest in the background.
        private readonly HashSet<int> _backgroundPrewarmInFlight = new();

        private ParticleSystem GetFromPool(int prefabId, GameObject prefab)
        {
            bool firstTimeSeen = !_prefabLookup.ContainsKey(prefabId);
            if (firstTimeSeen)
            {
                _prefabLookup[prefabId] = prefab;
                _pools[prefabId] = new Queue<ParticleSystem>();
            }

            var pool = _pools[prefabId];
            while (pool.Count > 0)
            {
                var item = pool.Dequeue();
                // FIX: availability is now the explicit IsAvailable flag, not
                // gameObject.activeInHierarchy — the GameObject stays active
                // permanently once created, so activeInHierarchy is no longer a
                // meaningful signal of pool availability.
                if (item != null)
                {
                    var tag = item.GetComponent<VFXTag>();
                    if (tag != null && tag.IsAvailable)
                    {
                        tag.IsAvailable = false;
                        return item;
                    }
                }
            }

            // Pool has nothing available right now (first-ever use, or exhausted
            // under load). Satisfy THIS call with exactly one synchronous instance...
            ParticleSystem immediate = CreateInstance(prefab, markAvailable: false);

            // ...and queue up the rest of the target size in the background so
            // future calls don't repeatedly pay this cost, without freezing the
            // current frame to build the whole batch at once.
            int targetPoolSize = firstTimeSeen ? _defaultPoolSize : _poolExpandBatchSize;
            if (targetPoolSize > 0 && !_backgroundPrewarmInFlight.Contains(prefabId))
            {
                _backgroundPrewarmInFlight.Add(prefabId);
                StartCoroutine(BackgroundPrewarm(prefabId, prefab, targetPoolSize));
            }

            return immediate;
        }

        private IEnumerator BackgroundPrewarm(int prefabId, GameObject prefab, int count)
        {
            int created = 0;
            while (created < count)
            {
                int thisFrameBudget = Mathf.Min(Mathf.Max(1, _maxPrewarmPerFrame), count - created);
                for (int i = 0; i < thisFrameBudget; i++)
                {
                    var ps = CreateInstance(prefab, markAvailable: true);
                    if (_pools.TryGetValue(prefabId, out var pool))
                        pool.Enqueue(ps);
                    created++;
                }
                yield return null;
            }
            _backgroundPrewarmInFlight.Remove(prefabId);
        }

        private void Prewarm(int prefabId, GameObject prefab, int count)
        {
            // Retained for any external/explicit prewarm callers (e.g. a loading
            // screen that wants to pay this cost upfront, all at once, on purpose).
            // The live combat path above no longer calls this directly.
            for (int i = 0; i < count; i++)
            {
                var ps = CreateInstance(prefab, markAvailable: true);
                _pools[prefabId].Enqueue(ps);
            }
        }

        private ParticleSystem CreateInstance(GameObject prefab, bool markAvailable)
        {
            var go = Instantiate(prefab, _vfxParent);
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) ps = go.AddComponent<ParticleSystem>();
            var tag = go.GetComponent<VFXTag>();
            if (tag == null) tag = go.AddComponent<VFXTag>();
            tag.PrefabId = prefab.GetInstanceID();

            // FIX (profiler-confirmed, 46.12ms self / 13 calls, ActivateAwakeRecursively):
            // This is now the ONLY SetActive(true) call this GameObject will ever
            // receive for its entire lifetime. It fires once, here, at creation —
            // during prewarm/background-prewarm (off the critical combat path) rather
            // than repeatedly on every reuse. Awake/OnEnable/the recursive activation
            // walk happen exactly once per instance, period.
            //
            // After this, "hidden" is represented by Stop(StopEmittingAndClear) —
            // zero live particles, nothing rendered — never by SetActive(false).
            // NOTE: this assumes the VFX prefab's only meaningful per-frame cost is
            // the ParticleSystem itself (confirmed: only ParticleSystem + VFXTag are
            // attached here, VFXTag has no Update()). If a hit-VFX prefab ever gains
            // another MonoBehaviour with real Update()/FixedUpdate() logic, that
            // component would now tick every frame even while pooled-and-idle, since
            // the GameObject is never deactivated. Worth a quick check on the actual
            // prefab asset if new VFX types are added later.
            go.SetActive(true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            tag.IsAvailable = markAvailable;
            return ps;
        }

        private IEnumerator ReturnWhenDone(int prefabId, ParticleSystem ps)
        {
            yield return new WaitForSeconds(0.1f);
            while (ps != null && ps.IsAlive(true))
                yield return new WaitForSeconds(0.1f);
            if (ps == null) yield break;
            ps.transform.SetParent(_vfxParent);
            // FIX: no SetActive(false) — mark available via VFXTag instead. Stop+Clear
            // defensively in case natural completion left any trailing state, so the
            // next reuse starts from a clean particle system.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var tag = ps.GetComponent<VFXTag>();
            if (tag != null) tag.IsAvailable = true;
            if (_pools.TryGetValue(prefabId, out var pool)) pool.Enqueue(ps);
        }

        private IEnumerator ReturnAfterDelay(int prefabId, ParticleSystem ps, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (ps == null) yield break;
            // FIX: no SetActive(false) — see ReturnWhenDone above. StopAttached()
            // already called Stop+Clear before scheduling this coroutine.
            var tag = ps.GetComponent<VFXTag>();
            if (tag != null) tag.IsAvailable = true;
            if (_pools.TryGetValue(prefabId, out var pool)) pool.Enqueue(ps);
        }

        private int GetPrefabId(ParticleSystem ps)
        {
            var tag = ps?.GetComponent<VFXTag>();
            return tag != null ? tag.PrefabId : -1;
        }

        private void OnDestroy() => ServiceLocator.Unregister<VFXManager>();
    }

    public class VFXTag : MonoBehaviour
    {
        public int PrefabId;

        // FIX: Pool availability is now tracked explicitly instead of via
        // gameObject.activeInHierarchy, because the pool no longer deactivates
        // GameObjects between uses (see VFXManager pool rewrite below).
        public bool IsAvailable;
    }
}