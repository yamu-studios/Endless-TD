// ============================================================================
// ETD.Core - ObjectPool.cs
// Generic object pool. Pre-warms objects, grows on demand. Zero-alloc at runtime.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ETD.Core
{
    public class ObjectPool<T> where T : Component
    {
        private readonly Queue<T> _pool = new();
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;

        public int CountActive { get; private set; }
        public int CountInactive => _pool.Count;

        public ObjectPool(T prefab, Transform parent, int preWarmCount,
            Action<T> onGet = null, Action<T> onRelease = null)
        {
            _prefab = prefab;
            _parent = parent;
            _onGet = onGet;
            _onRelease = onRelease;

            for (int i = 0; i < preWarmCount; i++)
            {
                var obj = UnityEngine.Object.Instantiate(_prefab, _parent);

                // FIX (profiler-confirmed, wave-55+ capture): If the prefab's root is
                // inactive, Instantiate() defers Awake()/OnEnable() until the first
                // SetActive(true) call. Previously that meant the ENTIRE first-activation
                // cost (component Awake logic, GetComponentsInChildren scans, any
                // material/shader work) landed on the first live SpawnEnemy() call
                // during combat instead of during this prewarm pass. Confirmed in
                // profiler: a single enemy's first activation cost 126.80ms self-time
                // in GameObject.ActivateAwakeRecursively, mid-wave.
                //
                // Cycling true->false here forces Awake()/OnEnable() to run now,
                // during pool warmup (scene load / wave-transition idle time), instead
                // of synchronously blocking a combat frame later.
                obj.gameObject.SetActive(true);
                obj.gameObject.SetActive(false);

                _pool.Enqueue(obj);
            }
        }

        public T Get()
        {
            T obj;
            bool isFreshInstance = false;
            if (_pool.Count > 0)
            {
                obj = _pool.Dequeue();
            }
            else
            {
                obj = UnityEngine.Object.Instantiate(_prefab, _parent);
                isFreshInstance = true;
            }

            if (isFreshInstance)
            {
                // Same reasoning as the prewarm loop above: pay any first-activation
                // cost in a controlled pass rather than leaving it to compound with
                // whatever else this frame is already doing. Since this branch only
                // runs when the pool is exhausted (rare after a reasonable preWarmCount),
                // the extra activate/deactivate cycle here is cheap insurance, not a
                // recurring cost.
                obj.gameObject.SetActive(true);
                obj.gameObject.SetActive(false);
            }

            obj.gameObject.SetActive(true);
            CountActive++;
            _onGet?.Invoke(obj);
            return obj;
        }

        public void Release(T obj)
        {
            if (obj == null) return;
            obj.gameObject.SetActive(false);
            _onRelease?.Invoke(obj);
            _pool.Enqueue(obj);
            CountActive--;
        }

        public void ReleaseAll(List<T> activeObjects)
        {
            for (int i = activeObjects.Count - 1; i >= 0; i--)
                Release(activeObjects[i]);
            activeObjects.Clear();
        }

        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var obj = _pool.Dequeue();
                if (obj != null)
                    UnityEngine.Object.Destroy(obj.gameObject);
            }
            CountActive = 0;
        }
    }
}
