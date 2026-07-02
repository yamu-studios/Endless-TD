// ============================================================================
// ETD.Marketing - TrailerOrbitCameraRig.cs
// Put in: Assets/Scripts/Marketing/TrailerOrbitCameraRig.cs
//
// Purpose:
// Repeatable cinematic orbit camera for Steam trailer shots.
// Record multiple board states with the exact same orbit settings,
// then cut between clips at the same timestamp/angle.
// ============================================================================

using UnityEngine;

namespace ETD.Marketing
{
    [ExecuteAlways]
    public class TrailerOrbitCameraRig : MonoBehaviour
    {
        public enum OrbitDirection
        {
            Clockwise,
            CounterClockwise
        }

        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _target;

        [Header("Orbit")]
        [SerializeField] private OrbitDirection _direction = OrbitDirection.Clockwise;
        [SerializeField] private float _startYaw = 0f;
        [SerializeField] private float _degreesPerSecond = 6f;
        [SerializeField] private float _radius = 14f;
        [SerializeField] private float _height = 14f;

        [Header("Look At")]
        [SerializeField] private float _lookAtHeightOffset = 0f;
        [SerializeField] private bool _lookAtTarget = true;

        [Header("Playback")]
        [SerializeField] private bool _playOnStart = false;
        [SerializeField] private bool _useUnscaledTime = true;
        [SerializeField] private float _autoStopAfterSeconds = 8f;

        [Header("Optional Zoom")]
        [SerializeField] private bool _animateZoom = false;
        [SerializeField] private AnimationCurve _radiusCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        [SerializeField] private AnimationCurve _heightCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

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
                ApplyAtTime(0f);
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

            if (_autoStopAfterSeconds > 0f && _elapsed >= _autoStopAfterSeconds)
            {
                _elapsed = _autoStopAfterSeconds;
                _isPlaying = false;
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

        public void Stop()
        {
            _isPlaying = false;
            _elapsed = 0f;
            ApplyAtTime(_elapsed);
        }

        public void SetTime(float seconds)
        {
            _elapsed = Mathf.Max(0f, seconds);
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

            float yaw = CalculateYaw(seconds);

            float normalized = _autoStopAfterSeconds > 0f
                ? Mathf.Clamp01(seconds / _autoStopAfterSeconds)
                : 0f;

            float radiusMultiplier = _animateZoom && _radiusCurve != null ? _radiusCurve.Evaluate(normalized) : 1f;
            float heightMultiplier = _animateZoom && _heightCurve != null ? _heightCurve.Evaluate(normalized) : 1f;

            float currentRadius = Mathf.Max(0.1f, _radius * radiusMultiplier);
            float currentHeight = _height * heightMultiplier;

            Vector3 targetPos = _target.position;
            Vector3 lookPoint = targetPos + Vector3.up * _lookAtHeightOffset;

            float radians = yaw * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Sin(radians) * currentRadius,
                currentHeight,
                Mathf.Cos(radians) * currentRadius);

            _camera.transform.position = targetPos + offset;

            if (_lookAtTarget)
            {
                Vector3 direction = lookPoint - _camera.transform.position;

                if (direction.sqrMagnitude < 0.001f)
                    direction = Vector3.forward;

                _camera.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
            else
            {
                _camera.transform.rotation = Quaternion.Euler(65f, yaw, 0f);
            }
        }

        private float CalculateYaw(float seconds)
        {
            float sign = _direction == OrbitDirection.Clockwise ? 1f : -1f;
            return _startYaw + sign * _degreesPerSecond * Mathf.Max(0f, seconds);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawGizmos || _target == null)
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_target.position, _radius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_target.position + Vector3.up * _lookAtHeightOffset, 0.25f);

            if (_camera != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(_camera.transform.position, _target.position + Vector3.up * _lookAtHeightOffset);
            }
        }
    }
}
