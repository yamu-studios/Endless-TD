// ============================================================================
// ETD.Enemies - EnemyHealthBarSystem.cs
// One shared world-space canvas + one LateUpdate for every enemy health bar.
// Replaces the previous one-Canvas-per-enemy setup where each visible bar was
// its own draw call, its own canvas rebuild, and its own LateUpdate callback.
// All bar content batches into a single canvas; the camera billboard rotation
// is computed once per frame instead of once per bar. Zero per-frame heap
// allocations.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace ETD.Enemies
{
    public sealed class EnemyHealthBarSystem : MonoBehaviour
    {
        private static EnemyHealthBarSystem _instance;

        private readonly List<EnemyHealthBar> _bars = new(128);
        private Canvas _canvas;
        private Transform _cameraTransform;
        private bool _layerAdopted;

        /// <summary>
        /// Parents a bar's content under the shared canvas, preserving its world
        /// transform so the on-screen size stays identical. The shared canvas
        /// adopts the first bar's layer: UGUI renders a whole canvas on the
        /// canvas GameObject's layer, and the prefab bars use a dedicated layer.
        /// </summary>
        internal static void AttachBar(RectTransform barRoot)
        {
            if (barRoot == null)
                return;

            EnsureInstance();
            if (_instance == null)
                return;

            Transform sharedRoot = _instance._canvas.transform;
            if (barRoot.parent == sharedRoot)
                return;

            if (!_instance._layerAdopted)
            {
                _instance._layerAdopted = true;
                _instance.gameObject.layer = barRoot.gameObject.layer;
            }

            barRoot.SetParent(sharedRoot, true);
        }

        internal static void Register(EnemyHealthBar bar)
        {
            if (bar == null)
                return;

            EnsureInstance();
            if (_instance == null)
                return;

            List<EnemyHealthBar> bars = _instance._bars;
            int index = bar.RegisteredIndex;
            if (index >= 0 && index < bars.Count && bars[index] == bar)
                return;

            bar.RegisteredIndex = bars.Count;
            bars.Add(bar);
        }

        internal static void Unregister(EnemyHealthBar bar)
        {
            if (bar == null)
                return;

            int index = bar.RegisteredIndex;
            bar.RegisteredIndex = -1;

            if (_instance == null)
                return;

            List<EnemyHealthBar> bars = _instance._bars;
            if (index < 0 || index >= bars.Count || bars[index] != bar)
                return;

            RemoveAt(bars, index);
        }

        private static void RemoveAt(List<EnemyHealthBar> bars, int index)
        {
            int lastIndex = bars.Count - 1;
            EnemyHealthBar moved = bars[lastIndex];
            bars[index] = moved;
            bars.RemoveAt(lastIndex);
            if (moved != null && index != lastIndex)
                moved.RegisteredIndex = index;
        }

        private static void EnsureInstance()
        {
            if (_instance != null || !Application.isPlaying)
                return;

            var go = new GameObject("[EnemyHealthBars]");
            _instance = go.AddComponent<EnemyHealthBarSystem>();

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // Matches the sorting order of the per-enemy canvases this replaces.
            canvas.sortingOrder = 10;
            _instance._canvas = canvas;

            var rect = go.GetComponent<RectTransform>();
            rect.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rect.localScale = Vector3.one;
            rect.sizeDelta = Vector2.zero;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void LateUpdate()
        {
            int count = _bars.Count;
            if (count == 0)
                return;

            Profiler.BeginSample("EnemyHealthBarSystem.LateUpdate");

            // Re-resolve every frame so camera switches (e.g. trailer rigs that
            // retag MainCamera) are picked up. Camera.main is cached by Unity.
            Camera cam = Camera.main;
            if (cam != null)
                _cameraTransform = cam.transform;

            bool hasBillboard = false;
            Quaternion billboard = Quaternion.identity;
            if (_cameraTransform != null)
            {
                Vector3 forward = _cameraTransform.forward;
                if (IsFinite(forward) && forward.sqrMagnitude > 0.0001f)
                {
                    // Same result as the previous per-bar "transform.forward = camForward".
                    billboard = Quaternion.LookRotation(forward);
                    hasBillboard = true;
                }
            }

            float deltaTime = Time.deltaTime;

            // Iterate backwards so a bar destroyed mid-loop can be swap-removed
            // without invalidating the remaining indices.
            for (int i = count - 1; i >= 0; i--)
            {
                if (i >= _bars.Count)
                    continue;

                EnemyHealthBar bar = _bars[i];
                if (bar == null)
                {
                    RemoveAt(_bars, i);
                    continue;
                }

                bar.ManagedLateUpdate(deltaTime, billboard, hasBillboard);
            }

            Profiler.EndSample();
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
