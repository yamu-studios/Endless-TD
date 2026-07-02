// ============================================================================
// ETD.Marketing - TrailerCinematicCameraRig.cs
// Put in: Assets/Scripts/Marketing/TrailerCinematicCameraRig.cs
//
// Purpose:
// Modular repeatable cinematic camera rig for trailer capture.
//
// Optional motion layers:
// - Orbit around target
// - Camera pan offset animation
// - Target pan animation
// - Zoom/radius animation
// - Height animation
// - FOV / orthographic size animation
//
// Trailer trick:
// Use identical settings and identical duration for multiple board states.
// Cut between clips at the same timestamp to make the board appear to transform
// while the camera motion continues smoothly.
// ============================================================================

using UnityEngine;

namespace ETD.Marketing
{
    [ExecuteAlways]
    public class TrailerCinematicCameraRig : MonoBehaviour
    {
        public enum OrbitDirection
        {
            Clockwise,
            CounterClockwise
        }

        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _target;

        [Header("Playback")]
        [SerializeField] private bool _playOnStart = false;
        [SerializeField] private bool _useUnscaledTime = true;
        [SerializeField] private float _duration = 8f;
        [SerializeField] private bool _loop = false;

        [Header("Base Camera")]
        [SerializeField] private float _startYaw = 0f;
        [SerializeField] private float _baseRadius = 14f;
        [SerializeField] private float _baseHeight = 14f;
        [SerializeField] private float _lookAtHeightOffset = 0f;

        [Header("Optional Orbit")]
        [SerializeField] private bool _enableOrbit = true;
        [SerializeField] private OrbitDirection _orbitDirection = OrbitDirection.Clockwise;
        [SerializeField] private float _degreesPerSecond = 6f;
        [SerializeField] private AnimationCurve _orbitSpeedMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("Optional Camera Pan")]
        [Tooltip("Moves the camera itself in world space over the shot. Good for side drift.")]
        [SerializeField] private bool _enableCameraPan = false;
        [SerializeField] private Vector3 _cameraPanStart = Vector3.zero;
        [SerializeField] private Vector3 _cameraPanEnd = Vector3.zero;
        [SerializeField] private AnimationCurve _cameraPanCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Optional Target Pan")]
        [Tooltip("Moves the look-at target over the shot. Good for scanning across a long 9x20 board.")]
        [SerializeField] private bool _enableTargetPan = false;
        [SerializeField] private Vector3 _targetPanStart = Vector3.zero;
        [SerializeField] private Vector3 _targetPanEnd = Vector3.zero;
        [SerializeField] private AnimationCurve _targetPanCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Optional Zoom")]
        [Tooltip("Animates orbit radius. Values under 1 zoom in, values over 1 zoom out.")]
        [SerializeField] private bool _enableZoom = false;
        [SerializeField] private AnimationCurve _radiusMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("Optional Height")]
        [Tooltip("Animates camera height. Useful for crane-like reveal shots.")]
        [SerializeField] private bool _enableHeightAnimation = false;
        [SerializeField] private AnimationCurve _heightMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("Optional FOV")]
        [Tooltip("Only affects perspective cameras.")]
        [SerializeField] private bool _enableFovAnimation = false;
        [SerializeField] private float _baseFov = 60f;
        [SerializeField] private AnimationCurve _fovMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("Optional Orthographic Size")]
        [Tooltip("Only affects orthographic cameras.")]
        [SerializeField] private bool _enableOrthoAnimation = false;
        [SerializeField] private float _baseOrthographicSize = 10f;
        [SerializeField] private AnimationCurve _orthographicSizeMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("Debug")]
        [SerializeField] private bool _drawGizmos = true;

        private bool _isPlaying;
        private float _elapsed;

        private static bool TrailerToolsAllowed
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        public bool IsPlaying => _isPlaying;
        public float Elapsed => _elapsed;
        public float Duration => _duration;
        public float NormalizedTime => GetNormalizedTime(_elapsed);
        public float CurrentYaw => CalculateYaw(_elapsed);

        private void Reset()
        {
            _camera = Camera.main;
        }

        private void Awake()
        {
            if (!TrailerToolsAllowed)
            {
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            if (!TrailerToolsAllowed)
                return;

            if (_camera == null)
                _camera = Camera.main;

            if (Application.isPlaying && _playOnStart)
                PlayFromStart();
            else
                ApplyAtTime(_elapsed);
        }

        private void LateUpdate()
        {
            if (!TrailerToolsAllowed)
                return;

            if (!Application.isPlaying)
            {
                ApplyAtTime(_elapsed);
                return;
            }

            if (!_isPlaying)
                return;

            float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _elapsed += dt;

            if (_duration > 0f && _elapsed >= _duration)
            {
                if (_loop)
                {
                    _elapsed %= _duration;
                }
                else
                {
                    _elapsed = _duration;
                    _isPlaying = false;
                }
            }

            ApplyAtTime(_elapsed);
        }

        public void PlayFromStart()
        {
            _elapsed = 0f;
            _isPlaying = true;
            ApplyAtTime(_elapsed);
        }

        public void PlayFromTime(float seconds)
        {
            _elapsed = Mathf.Max(0f, seconds);
            _isPlaying = true;
            ApplyAtTime(_elapsed);
        }

        public void Pause()
        {
            _isPlaying = false;
        }

        public void StopAndReset()
        {
            _isPlaying = false;
            _elapsed = 0f;
            ApplyAtTime(_elapsed);
        }

        public void SetTime(float seconds)
        {
            _elapsed = Mathf.Clamp(seconds, 0f, Mathf.Max(0f, _duration));
            ApplyAtTime(_elapsed);
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            ApplyAtTime(_elapsed);
        }

        public void SetCamera(Camera cam)
        {
            _camera = cam;
            ApplyAtTime(_elapsed);
        }

        public void ApplyAtTime(float seconds)
        {
            if (!TrailerToolsAllowed)
                return;

            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null || _target == null)
                return;

            float t = GetNormalizedTime(seconds);
            float yaw = CalculateYaw(seconds);

            Vector3 baseTargetPos = _target.position;
            Vector3 targetPanOffset = GetTargetPanOffset(t);
            Vector3 cameraPanOffset = GetCameraPanOffset(t);

            Vector3 effectiveTarget = baseTargetPos + targetPanOffset;
            Vector3 lookPoint = effectiveTarget + Vector3.up * _lookAtHeightOffset;

            float radius = GetRadius(t);
            float height = GetHeight(t);

            float radians = yaw * Mathf.Deg2Rad;

            Vector3 orbitOffset = new Vector3(
                Mathf.Sin(radians) * radius,
                height,
                Mathf.Cos(radians) * radius);

            _camera.transform.position = effectiveTarget + orbitOffset + cameraPanOffset;

            Vector3 direction = lookPoint - _camera.transform.position;

            if (direction.sqrMagnitude < 0.001f)
                direction = Vector3.forward;

            _camera.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            ApplyLens(t);
        }

        private float GetNormalizedTime(float seconds)
        {
            if (_duration <= 0f)
                return 0f;

            return Mathf.Clamp01(seconds / _duration);
        }

        private float CalculateYaw(float seconds)
        {
            if (!_enableOrbit)
                return _startYaw;

            float sign = _orbitDirection == OrbitDirection.Clockwise ? 1f : -1f;
            float t = GetNormalizedTime(seconds);
            float speedMultiplier = _orbitSpeedMultiplier != null ? _orbitSpeedMultiplier.Evaluate(t) : 1f;

            return _startYaw + sign * _degreesPerSecond * speedMultiplier * Mathf.Max(0f, seconds);
        }

        private Vector3 GetCameraPanOffset(float t)
        {
            if (!_enableCameraPan)
                return Vector3.zero;

            float v = _cameraPanCurve != null ? _cameraPanCurve.Evaluate(t) : t;
            return Vector3.LerpUnclamped(_cameraPanStart, _cameraPanEnd, v);
        }

        private Vector3 GetTargetPanOffset(float t)
        {
            if (!_enableTargetPan)
                return Vector3.zero;

            float v = _targetPanCurve != null ? _targetPanCurve.Evaluate(t) : t;
            return Vector3.LerpUnclamped(_targetPanStart, _targetPanEnd, v);
        }

        private float GetRadius(float t)
        {
            float multiplier = 1f;

            if (_enableZoom && _radiusMultiplier != null)
                multiplier = _radiusMultiplier.Evaluate(t);

            return Mathf.Max(0.1f, _baseRadius * multiplier);
        }

        private float GetHeight(float t)
        {
            float multiplier = 1f;

            if (_enableHeightAnimation && _heightMultiplier != null)
                multiplier = _heightMultiplier.Evaluate(t);

            return _baseHeight * multiplier;
        }

        private void ApplyLens(float t)
        {
            if (_camera == null)
                return;

            if (_camera.orthographic)
            {
                if (_enableOrthoAnimation && _orthographicSizeMultiplier != null)
                    _camera.orthographicSize = Mathf.Max(0.1f, _baseOrthographicSize * _orthographicSizeMultiplier.Evaluate(t));
            }
            else
            {
                if (_enableFovAnimation && _fovMultiplier != null)
                    _camera.fieldOfView = Mathf.Clamp(_baseFov * _fovMultiplier.Evaluate(t), 1f, 179f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawGizmos || _target == null)
                return;

            Vector3 center = _target.position;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(center, _baseRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(center + Vector3.up * _lookAtHeightOffset, 0.25f);

            if (_enableTargetPan)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(center + _targetPanStart, center + _targetPanEnd);
                Gizmos.DrawSphere(center + _targetPanStart, 0.2f);
                Gizmos.DrawSphere(center + _targetPanEnd, 0.2f);
            }

            if (_camera != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(_camera.transform.position, center + Vector3.up * _lookAtHeightOffset);
            }
        }
    }
}
