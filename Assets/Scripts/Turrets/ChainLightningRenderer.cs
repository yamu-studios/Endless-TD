// ============================================================================
// ETD.Turrets - ChainLightningRenderer.cs
// Pooled lightning arcs with no per-arc coroutine or runtime LineRenderer growth.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace ETD.Turrets
{
    public sealed class ChainLightningRenderer : MonoBehaviour
    {
        [Header("Chain Material")]
        [SerializeField] private Material _chainMaterial;

        [Header("Line Settings")]
        [SerializeField] private float _lineWidth = 0.08f;
        [SerializeField] private float _displayDuration = 0.12f;
        [SerializeField] private int _arcSegments = 8;
        [SerializeField] private float _arcAmplitude = 0.3f;
        [SerializeField] private float _yOffset = 0.3f;

        [Header("Pool")]
        [SerializeField] private int _poolSize = 8;

        [Header("Performance")]
        [Tooltip("Hard visual cap. Extra chain hits still deal damage but skip the cosmetic arc.")]
        [SerializeField] private int _maxVisibleArcs = 8;

        [Tooltip("Allowing expansion can cause Instantiate spikes during dense waves. Keep off for gameplay.")]
        [SerializeField] private bool _allowRuntimePoolExpansion = false;

        private sealed class Arc
        {
            public LineRenderer Renderer;
            public Vector3[] Positions;
            public float ExpireAt;
            public uint Seed;
        }

        private readonly Stack<Arc> _available = new();
        private readonly List<Arc> _active = new(16);
        private Material _fallbackMaterial;
        private int _pointCount;
        private uint _nextSeed = 1;

        private void Awake()
        {
            _arcSegments = Mathf.Max(1, _arcSegments);
            _pointCount = _arcSegments + 2;
            _poolSize = Mathf.Max(1, _poolSize);
            _maxVisibleArcs = Mathf.Max(1, _maxVisibleArcs);

            int warmCount = Mathf.Min(_poolSize, _maxVisibleArcs);
            for (int i = 0; i < warmCount; i++)
                _available.Push(CreateArc());

            // No active arcs means no Update cost on a lightning turret.
            enabled = false;
        }

        private void OnDisable()
        {
            ReleaseAllArcs();
            enabled = false;
        }

        private void OnDestroy()
        {
            if (_fallbackMaterial != null)
                Destroy(_fallbackMaterial);
        }

        /// <summary>Shows a cosmetic arc. Damage is handled independently by TurretController.</summary>
        public void ShowChain(Vector3 from, Vector3 to)
        {
            if (_active.Count >= _maxVisibleArcs)
                return;

            Arc arc = GetArc();
            if (arc == null)
                return;

            Profiler.BeginSample("ChainLightningRenderer.ShowChain");
            arc.Seed = _nextSeed++;
            arc.ExpireAt = Time.time + Mathf.Max(0.01f, _displayDuration);
            SetJaggedLine(arc, from, to);
            arc.Renderer.enabled = true;
            _active.Add(arc);
            enabled = true;
            Profiler.EndSample();
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Arc arc = _active[i];
                if (now < arc.ExpireAt)
                    continue;

                arc.Renderer.enabled = false;
                _available.Push(arc);
                _active.RemoveAt(i);
            }

            if (_active.Count == 0)
                enabled = false;
        }

        private Arc GetArc()
        {
            if (_available.Count > 0)
                return _available.Pop();

            if (_allowRuntimePoolExpansion && _active.Count < _maxVisibleArcs)
                return CreateArc();

            return null;
        }

        private Arc CreateArc()
        {
            var go = new GameObject("ChainArc");
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = GetMaterial();
            line.startWidth = _lineWidth;
            line.endWidth = _lineWidth * 0.5f;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.generateLightingData = false;
            line.positionCount = _pointCount;
            line.enabled = false;

            return new Arc
            {
                Renderer = line,
                Positions = new Vector3[_pointCount]
            };
        }

        private Material GetMaterial()
        {
            if (_chainMaterial != null)
                return _chainMaterial;

            if (_fallbackMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                _fallbackMaterial = new Material(shader);
            }

            return _fallbackMaterial;
        }

        private void SetJaggedLine(Arc arc, Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            float horizontalSqr = (direction.x * direction.x) + (direction.z * direction.z);
            Vector3 perpendicular;
            if (horizontalSqr <= 0.000001f)
            {
                perpendicular = Vector3.right;
            }
            else
            {
                float inverseLength = 1f / Mathf.Sqrt(horizontalSqr);
                perpendicular = new Vector3(-direction.z * inverseLength, 0f, direction.x * inverseLength);
            }

            Vector3[] positions = arc.Positions;
            positions[0] = from;
            positions[_pointCount - 1] = to;

            float inverseSegmentCount = 1f / (_pointCount - 1);
            for (int i = 1; i < _pointCount - 1; i++)
            {
                float t = i * inverseSegmentCount;
                float offset = SignedNoise(arc.Seed + (uint)(i * 747796405)) * _arcAmplitude;
                positions[i] = new Vector3(
                    Mathf.LerpUnclamped(from.x, to.x, t) + (perpendicular.x * offset),
                    Mathf.LerpUnclamped(from.y, to.y, t) + (offset * 0.5f) + _yOffset,
                    Mathf.LerpUnclamped(from.z, to.z, t) + (perpendicular.z * offset));
            }

            // One native call instead of SetPosition once per segment.
            arc.Renderer.SetPositions(positions);
        }

        private static float SignedNoise(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352d;
            value ^= value >> 15;
            value *= 0x846ca68b;
            value ^= value >> 16;
            return ((value & 0x00ffffff) / 8388607.5f) - 1f;
        }

        private void ReleaseAllArcs()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Arc arc = _active[i];
                if (arc?.Renderer != null)
                    arc.Renderer.enabled = false;
                _available.Push(arc);
            }
            _active.Clear();
        }
    }
}
