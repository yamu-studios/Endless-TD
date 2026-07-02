// ============================================================================
// ETD.Input - CameraController.cs  [REWRITTEN - Perspective Camera]
// Pan: WASD or middle mouse drag or edge scroll
// Zoom: scroll wheel (moves camera along its angle)
// Bounds: interpolated between two sets based on zoom level.
//   Zoomed IN  → tight bounds (_nearMin/Max)
//   Zoomed OUT → wide bounds  (_farMin/Max)
//   In between → linearly interpolated
// ============================================================================
using ETD.Core;
using UnityEngine;

namespace ETD.Input
{
    public class CameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;

        [Header("Pan")]
        [SerializeField] private float _panSpeed = 20f;
        [SerializeField] private float _panSmoothing = 10f;
        [SerializeField] private bool _enableEdgeScroll = false;
        [SerializeField] private float _edgeScrollThreshold = 20f;

        [Header("Zoom")]
        [SerializeField] private float _zoomSpeed = 5f;
        [SerializeField] private float _zoomSmoothing = 8f;
        [SerializeField] private float _minZoom = 5f;
        [SerializeField] private float _maxZoom = 30f;

        [Header("Middle Mouse Drag")]
        [SerializeField] private float _dragSpeed = 0.5f;

        [Header("Bounds when Zoomed IN (tightest)")]
        [SerializeField] private float _nearMinX = 0f;
        [SerializeField] private float _nearMaxX = 18f;
        [SerializeField] private float _nearMinZ = 0f;
        [SerializeField] private float _nearMaxZ = 22f;

        [Header("Bounds when Zoomed OUT (widest)")]
        [SerializeField] private float _farMinX = -5f;
        [SerializeField] private float _farMaxX = 25f;
        [SerializeField] private float _farMinZ = -5f;
        [SerializeField] private float _farMaxZ = 30f;

        // Runtime
        private Vector3 _targetPosition;
        private float _targetZoom;
        private float _currentZoom;

        private bool _isDragging;
        private Vector3 _dragOrigin;
        private bool _cameraInputBlocked;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
        }

        private void Start()
        {
            //_targetPosition = transform.position;
            _targetPosition = new Vector3(3.899f, 12.124f, -2.525f);
            transform.position = _targetPosition;
            _currentZoom = GetCurrentZoomDistance();
            _targetZoom = _currentZoom;

            // Load saved camera speeds
            var save = SaveSystem.Load();
            if (save.CameraPanSpeed > 0) _panSpeed = save.CameraPanSpeed;
            if (save.CameraZoomSpeed > 0) _zoomSpeed = save.CameraZoomSpeed;

            EventBus.Subscribe<GamePausedEvent>(OnGamePaused);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            RefreshInputBlockState();
        }

        private void Update()
        {
            if (IsCameraInputBlocked())
            {
                _isDragging = false;
                _targetPosition = transform.position;
                _targetZoom = _currentZoom = GetCurrentZoomDistance();
                return;
            }

            HandleKeyboardPan();
            HandleEdgeScroll();
            HandleMiddleMouseDrag();
            HandleZoom();
            ApplyMovement();
        }

        /// <summary>Called by SettingsWindowUI when sliders change.</summary>
        public void SetSpeeds(float panSpeed, float zoomSpeed)
        {
            
            _panSpeed = panSpeed;
            _zoomSpeed = zoomSpeed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("New Pan Speed " + _panSpeed);
#endif
        }

        // =================================================================
        // PAN
        // =================================================================

        private void HandleKeyboardPan()
        {
            Vector3 input = Vector3.zero;

            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow)) input.z += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow)) input.z -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) input.x += 1f;

            if (input.sqrMagnitude < 0.001f) return;

            Vector3 forward = _camera.transform.forward; forward.y = 0; forward.Normalize();
            Vector3 right = _camera.transform.right; right.y = 0; right.Normalize();
            _targetPosition += (forward * input.z + right * input.x) * _panSpeed * Time.unscaledDeltaTime;
        }

        private void HandleEdgeScroll()
        {
            if (!_enableEdgeScroll) return;

            Vector3 mouse = UnityEngine.Input.mousePosition;
            Vector3 dir = Vector3.zero;

            if (mouse.x < _edgeScrollThreshold) dir.x -= 1f;
            if (mouse.x > Screen.width - _edgeScrollThreshold) dir.x += 1f;
            if (mouse.y < _edgeScrollThreshold) dir.z -= 1f;
            if (mouse.y > Screen.height - _edgeScrollThreshold) dir.z += 1f;

            if (dir.sqrMagnitude < 0.001f) return;

            _targetPosition += dir.normalized * _panSpeed * Time.unscaledDeltaTime;
        }

        private void HandleMiddleMouseDrag()
        {
            if (UnityEngine.Input.GetMouseButtonDown(2))
            {
                _isDragging = true;
                _dragOrigin = UnityEngine.Input.mousePosition;
            }

            if (UnityEngine.Input.GetMouseButtonUp(2))
                _isDragging = false;

            if (!_isDragging) return;

            Vector3 delta = UnityEngine.Input.mousePosition - _dragOrigin;
            _dragOrigin = UnityEngine.Input.mousePosition;

            Vector3 forward = _camera.transform.forward; forward.y = 0; forward.Normalize();
            Vector3 right = _camera.transform.right; right.y = 0; right.Normalize();

            _targetPosition -= (right * delta.x + forward * delta.y) * _dragSpeed * Time.unscaledDeltaTime;
        }

        // =================================================================
        // ZOOM
        // =================================================================

        private void HandleZoom()
        {
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.001f) return;

            _targetZoom -= scroll * _zoomSpeed;
            _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);
        }

        // =================================================================
        // APPLY MOVEMENT + ZOOM-BASED BOUNDS
        // =================================================================

        private void ApplyMovement()
        {
            // Smooth zoom
            _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom,
                Time.unscaledDeltaTime * _zoomSmoothing);

            // Compute zoom ratio: 0 = fully zoomed IN, 1 = fully zoomed OUT
            float zoomRatio = Mathf.InverseLerp(_minZoom, _maxZoom, _currentZoom);

            // Interpolate bounds based on zoom ratio
            float minX = Mathf.Lerp(_nearMinX, _farMinX, zoomRatio);
            float maxX = Mathf.Lerp(_nearMaxX, _farMaxX, zoomRatio);
            float minZ = Mathf.Lerp(_nearMinZ, _farMinZ, zoomRatio);
            float maxZ = Mathf.Lerp(_nearMaxZ, _farMaxZ, zoomRatio);

            // Clamp target position to current bounds
            _targetPosition.x = Mathf.Clamp(_targetPosition.x, minX, maxX);
            _targetPosition.z = Mathf.Clamp(_targetPosition.z, minZ, maxZ);

            // Smooth pan
            Vector3 newPos = Vector3.Lerp(transform.position, _targetPosition,
                Time.unscaledDeltaTime * _panSmoothing);

            // Rebuild camera position along its angle at current zoom distance
            Vector3 camDir = _camera.transform.forward;
            float t = newPos.y / -camDir.y;
            Vector3 lookAtGround = newPos + camDir * t;
            Vector3 finalPos = lookAtGround - camDir * _currentZoom;

            transform.position = finalPos;
            _targetPosition = new Vector3(finalPos.x, finalPos.y, finalPos.z);
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private float GetCurrentZoomDistance()
        {
            Vector3 camDir = _camera.transform.forward;
            if (Mathf.Abs(camDir.y) < 0.001f) return _minZoom;
            return transform.position.y / -camDir.y;
        }

        private void OnGamePaused(GamePausedEvent evt)
        {
            _cameraInputBlocked = evt.IsPaused;
            if (_cameraInputBlocked)
                _isDragging = false;
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            RefreshInputBlockState();
        }

        private void RefreshInputBlockState()
        {
            _cameraInputBlocked = IsCameraInputBlocked();
        }

        private bool IsCameraInputBlocked()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
                return _cameraInputBlocked;

            return manager.CurrentState == GameState.Paused
                || manager.CurrentState == GameState.LevelUp
                || manager.CurrentState == GameState.EvolveChoice
                || manager.CurrentState == GameState.GameOver;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<GamePausedEvent>(OnGamePaused);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        public void SnapTo(Vector3 worldPos)
        {
            float zoomRatio = Mathf.InverseLerp(_minZoom, _maxZoom, _currentZoom);
            worldPos.x = Mathf.Clamp(worldPos.x,
                Mathf.Lerp(_nearMinX, _farMinX, zoomRatio),
                Mathf.Lerp(_nearMaxX, _farMaxX, zoomRatio));
            worldPos.z = Mathf.Clamp(worldPos.z,
                Mathf.Lerp(_nearMinZ, _farMinZ, zoomRatio),
                Mathf.Lerp(_nearMaxZ, _farMaxZ, zoomRatio));

            Vector3 camDir = _camera.transform.forward;
            transform.position = worldPos - camDir * _currentZoom;
            _targetPosition = transform.position;
        }
    }
}