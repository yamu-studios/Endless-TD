// ============================================================================
// ETD.Projectiles - BeamRenderer.cs  [LASER PIPELINE OPTIMIZATION]
// Beam endpoint updates are throttled and native LineRenderer calls are avoided
// when neither endpoint/width has changed enough to be visible.
// ============================================================================
using UnityEngine;
using ETD.Enemies;

namespace ETD.Projectiles
{
    public class BeamRenderer : MonoBehaviour
    {
        [Header("Beam Settings")]
        [SerializeField] private float _baseWidth = 0.1f;
        [SerializeField] private float _maxWidthMultiplier = 4f;
        [SerializeField] private Color _beamColor = Color.cyan;

        [Header("Performance")]
        [Tooltip("Maximum visual endpoint refresh rate. 30 Hz is smooth for beams and greatly reduces LineRenderer native calls.")]
        [Range(15f, 60f)]
        [SerializeField] private float _positionUpdateRate = 30f;

        [Tooltip("Skip LineRenderer SetPosition if endpoint movement is below this distance.")]
        [SerializeField] private float _positionChangeThreshold = 0.0025f;

        [Header("Multi-target beams (Path B)")]
        [Tooltip("Assign additional LineRenderer components for multi-target. Index 0 is the base one.")]
        [SerializeField] private LineRenderer[] _beamLines;

        private Transform _origin;
        private EnemyController _primaryTarget;
        private EnemyController[] _multiTargets;
        private Vector3[] _lastEndPositions;
        private bool[] _hasLastPosition;
        private bool _isActive;
        private float _widthMultiplier = 1f;
        private float _appliedWidthMultiplier = -1f;
        private bool _isMultiMode;
        private float _nextPositionUpdateTime;
        private float _sqrPositionChangeThreshold;

        private void Awake()
        {
            if (_beamLines == null || _beamLines.Length == 0)
            {
                LineRenderer line = GetComponent<LineRenderer>();
                if (line != null)
                    _beamLines = new[] { line };
            }

            if (_beamLines == null)
                _beamLines = System.Array.Empty<LineRenderer>();

            _multiTargets = new EnemyController[Mathf.Max(1, _beamLines.Length)];
            _lastEndPositions = new Vector3[Mathf.Max(1, _beamLines.Length)];
            _hasLastPosition = new bool[Mathf.Max(1, _beamLines.Length)];
            _sqrPositionChangeThreshold = _positionChangeThreshold * _positionChangeThreshold;

            for (int i = 0; i < _beamLines.Length; i++)
            {
                LineRenderer line = _beamLines[i];
                if (line == null) continue;
                line.positionCount = 2;
                line.startColor = _beamColor;
                line.endColor = _beamColor;
                line.enabled = false;
            }
        }

        public void Activate(Transform origin, EnemyController target)
        {
            bool modeChanged = !_isActive || _isMultiMode;
            bool targetChanged = _primaryTarget != target || _origin != origin;

            _origin = origin;
            _primaryTarget = target;
            _isActive = target != null;
            _isMultiMode = false;
            _nextPositionUpdateTime = 0f;

            if (modeChanged)
            {
                SetLineEnabled(0, _isActive);
                for (int i = 1; i < _beamLines.Length; i++)
                    SetLineEnabled(i, false);
                ClearCachedPositions();
            }
            else if (targetChanged)
            {
                ClearCachedPosition(0);
            }
        }

        public void ActivateMulti(Transform origin, EnemyController[] targets)
        {
            ActivateMulti(origin, targets, targets != null ? targets.Length : 0);
        }

        public void ActivateMulti(Transform origin, EnemyController[] targets, int targetCount)
        {
            _origin = origin;
            _primaryTarget = targets != null && targetCount > 0 ? targets[0] : null;
            _isActive = _primaryTarget != null;
            _isMultiMode = true;
            _nextPositionUpdateTime = 0f;

            int count = targets != null ? Mathf.Min(targetCount, _beamLines.Length) : 0;
            for (int i = 0; i < _beamLines.Length; i++)
            {
                EnemyController next = i < count ? targets[i] : null;
                bool active = next != null && !next.IsDead && next.gameObject.activeInHierarchy;

                if (_multiTargets[i] != next)
                    ClearCachedPosition(i);

                _multiTargets[i] = active ? next : null;
                SetLineEnabled(i, active);
            }
        }

        public void SetStackMultiplier(float multiplier)
        {
            float next = Mathf.Clamp(multiplier, 1f, _maxWidthMultiplier);
            if (Mathf.Abs(next - _widthMultiplier) < 0.0001f)
                return;

            _widthMultiplier = next;
            ApplyWidth();
        }

        public void Deactivate()
        {
            if (!_isActive && _primaryTarget == null)
                return;

            _isActive = false;
            _primaryTarget = null;
            _widthMultiplier = 1f;
            _appliedWidthMultiplier = -1f;
            _isMultiMode = false;

            for (int i = 0; i < _beamLines.Length; i++)
            {
                SetLineEnabled(i, false);
                _multiTargets[i] = null;
            }

            ClearCachedPositions();
        }

        private void Update()
        {
            if (!_isActive)
                return;

            float now = Time.time;
            if (now < _nextPositionUpdateTime)
                return;

            float interval = 1f / Mathf.Max(1f, _positionUpdateRate);
            _nextPositionUpdateTime = now + interval;

            if (_origin == null)
            {
                Deactivate();
                return;
            }

            if (_isMultiMode)
                UpdateMulti();
            else
                UpdateSingle();
        }

        private void UpdateSingle()
        {
            if (_beamLines.Length == 0 || _beamLines[0] == null)
                return;

            if (_primaryTarget == null || _primaryTarget.IsDead || !_primaryTarget.gameObject.activeInHierarchy)
            {
                Deactivate();
                return;
            }

            SetBeamEndpoint(0, _primaryTarget);
        }

        private void UpdateMulti()
        {
            bool anyActive = false;
            for (int i = 0; i < _beamLines.Length; i++)
            {
                EnemyController target = _multiTargets[i];
                if (target == null || target.IsDead || !target.gameObject.activeInHierarchy)
                {
                    _multiTargets[i] = null;
                    SetLineEnabled(i, false);
                    continue;
                }

                SetLineEnabled(i, true);
                SetBeamEndpoint(i, target);
                anyActive = true;
            }

            if (!anyActive)
                Deactivate();
        }

        private void SetBeamEndpoint(int index, EnemyController target)
        {
            if (index < 0 || index >= _beamLines.Length || _beamLines[index] == null || target == null)
                return;

            Vector3 relativePosition = _origin.InverseTransformPoint(target.hitTransform.position);
            if (_hasLastPosition[index] &&
                (relativePosition - _lastEndPositions[index]).sqrMagnitude <= _sqrPositionChangeThreshold)
                return;

            LineRenderer line = _beamLines[index];
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, relativePosition);
            _lastEndPositions[index] = relativePosition;
            _hasLastPosition[index] = true;
        }

        private void ApplyWidth()
        {
            if (_beamLines == null || Mathf.Abs(_appliedWidthMultiplier - _widthMultiplier) < 0.0001f)
                return;

            float width = _baseWidth * _widthMultiplier;
            for (int i = 0; i < _beamLines.Length; i++)
            {
                LineRenderer line = _beamLines[i];
                if (line == null) continue;
                line.startWidth = width;
                line.endWidth = width * 0.5f;
            }

            _appliedWidthMultiplier = _widthMultiplier;
        }

        private void SetLineEnabled(int index, bool enabled)
        {
            if (index < 0 || index >= _beamLines.Length || _beamLines[index] == null)
                return;

            if (_beamLines[index].enabled != enabled)
                _beamLines[index].enabled = enabled;
        }

        private void ClearCachedPositions()
        {
            for (int i = 0; i < _hasLastPosition.Length; i++)
                _hasLastPosition[i] = false;
        }

        private void ClearCachedPosition(int index)
        {
            if (index >= 0 && index < _hasLastPosition.Length)
                _hasLastPosition[index] = false;
        }

        public bool IsActive => _isActive;
        public float CurrentWidth => _baseWidth * _widthMultiplier;
    }
}
