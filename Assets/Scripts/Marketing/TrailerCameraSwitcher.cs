// ============================================================================
// ETD.Marketing - TrailerCameraSwitcher.cs
// Put in: Assets/Scripts/Marketing/TrailerCameraSwitcher.cs
//
// Purpose:
// Development-build camera switcher for trailer capture.
// Toggle between the normal gameplay camera and a separate trailer camera at
// runtime, without relying on UnityEditor tools.
//
// Works only in the Unity Editor or Development Builds.
// In normal release builds the component disables itself immediately.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace ETD.Marketing
{
    [DisallowMultipleComponent]
    public sealed class TrailerCameraSwitcher : MonoBehaviour
    {
        [Header("Camera References")]
        [Tooltip("Normal gameplay camera. If empty, Camera.main is used.")]
        [SerializeField] private Camera _mainCamera;

        [Tooltip("Separate trailer/cinematic camera. If empty, the script tries to find a camera with 'Trailer' or 'Cinematic' in its name.")]
        [SerializeField] private Camera _trailerCamera;

        [Header("Startup")]
        [SerializeField] private bool _useTrailerCameraOnStart = false;
        [SerializeField] private bool _autoFindMissingCameras = true;

        [Header("Hotkeys")]
        [SerializeField] private KeyCode _toggleCameraKey = KeyCode.F2;
        [SerializeField] private KeyCode _mainCameraKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode _trailerCameraKey = KeyCode.Alpha2;

        [Header("Switch Behaviour")]
        [Tooltip("Enable/disable Camera components so only one camera renders.")]
        [SerializeField] private bool _switchCameraEnabledState = true;

        [Tooltip("If the trailer camera object is inactive, activate it when needed.")]
        [SerializeField] private bool _activateCameraGameObjects = true;

        [Tooltip("Keep exactly one AudioListener active between the two cameras.")]
        [SerializeField] private bool _switchAudioListeners = true;

        [Tooltip("Make the active camera use the MainCamera tag. This helps systems that call Camera.main, such as world-space health bars.")]
        [SerializeField] private bool _retagActiveCameraAsMainCamera = true;

        [Tooltip("For Screen Space - Camera canvases, assign the active camera as worldCamera when switching.")]
        [SerializeField] private bool _updateScreenSpaceCameraCanvases = true;

        [Header("Optional Components")]
        [Tooltip("Components to disable while the trailer camera is active, for example your gameplay CameraController.")]
        [SerializeField] private Behaviour[] _disableWhileTrailerCameraActive;

        [Tooltip("Pause trailer rigs automatically when returning to the main camera.")]
        [SerializeField] private bool _pauseTrailerRigsWhenMainCameraActive = true;

        [SerializeField] private TrailerCinematicCameraRig _cinematicRig;
        [SerializeField] private TrailerOrbitCameraRig _orbitRig;

        private readonly Dictionary<Canvas, Camera> _originalCanvasCameras = new Dictionary<Canvas, Camera>(64);
        private string _mainCameraOriginalTag;
        private string _trailerCameraOriginalTag;
        private bool _usingTrailerCamera;
        private bool _initialized;

        private static bool IsAllowedBuild
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

        public Camera MainCamera => _mainCamera;
        public Camera TrailerCamera => _trailerCamera;
        public Camera ActiveCamera => _usingTrailerCamera ? _trailerCamera : _mainCamera;
        public bool IsUsingTrailerCamera => _usingTrailerCamera;
        public string ActiveCameraName => ActiveCamera != null ? ActiveCamera.name : "None";

        private void Reset()
        {
            _mainCamera = Camera.main;
            AutoFindTrailerCamera();
            AutoFindTrailerRigs();
        }

        private void Awake()
        {
            if (!IsAllowedBuild)
            {
                enabled = false;
                return;
            }

            InitializeIfNeeded();
            ApplyCameraState(_useTrailerCameraOnStart, false);
        }

        private void OnEnable()
        {
            if (!IsAllowedBuild)
            {
                enabled = false;
                return;
            }

            InitializeIfNeeded();
        }

        private void Update()
        {
            if (!IsAllowedBuild)
                return;

            if (UnityEngine.Input.GetKeyDown(_toggleCameraKey))
                ToggleCamera();

            if (UnityEngine.Input.GetKeyDown(_mainCameraKey))
                SwitchToMainCamera();

            if (UnityEngine.Input.GetKeyDown(_trailerCameraKey))
                SwitchToTrailerCamera();
        }

        private void OnDestroy()
        {
            if (!IsAllowedBuild)
                return;

            RestoreOriginalTags();
            RestoreOriginalCanvasCameras();
        }

        public void ToggleCamera()
        {
            ApplyCameraState(!_usingTrailerCamera, true);
        }

        public void SwitchToMainCamera()
        {
            ApplyCameraState(false, true);
        }

        public void SwitchToTrailerCamera()
        {
            ApplyCameraState(true, true);
        }

        public void RefreshReferences()
        {
            if (_autoFindMissingCameras)
            {
                if (_mainCamera == null)
                    _mainCamera = Camera.main;

                if (_trailerCamera == null)
                    AutoFindTrailerCamera();
            }

            AutoFindTrailerRigs();
        }

        private void InitializeIfNeeded()
        {
            if (_initialized)
                return;

            RefreshReferences();
            CacheOriginalTags();
            CacheOriginalCanvasCameras();
            _initialized = true;
        }

        private void ApplyCameraState(bool useTrailerCamera, bool log)
        {
            InitializeIfNeeded();
            RefreshReferences();

            if (_mainCamera == null)
            {
                Debug.LogWarning("[TrailerCameraSwitcher] Main camera is missing.", this);
                return;
            }

            if (_trailerCamera == null)
            {
                Debug.LogWarning("[TrailerCameraSwitcher] Trailer camera is missing. Assign it in the Inspector.", this);
                return;
            }

            if (_mainCamera == _trailerCamera)
            {
                Debug.LogWarning("[TrailerCameraSwitcher] Main camera and trailer camera reference the same Camera.", this);
                return;
            }

            _usingTrailerCamera = useTrailerCamera;

            Camera active = useTrailerCamera ? _trailerCamera : _mainCamera;
            Camera inactive = useTrailerCamera ? _mainCamera : _trailerCamera;

            if (_activateCameraGameObjects)
            {
                if (active != null && !active.gameObject.activeSelf)
                    active.gameObject.SetActive(true);
            }

            if (_switchCameraEnabledState)
            {
                if (active != null)
                    active.enabled = true;

                if (inactive != null)
                    inactive.enabled = false;
            }

            if (_switchAudioListeners)
                SwitchAudioListeners(active, inactive);

            if (_retagActiveCameraAsMainCamera)
                RetagActiveCamera(active, inactive);

            if (_updateScreenSpaceCameraCanvases)
                UpdateScreenSpaceCameraCanvases(active);

            ApplyOptionalComponentState(useTrailerCamera);

            if (!useTrailerCamera && _pauseTrailerRigsWhenMainCameraActive)
                PauseTrailerRigs();

            if (log)
                Debug.Log($"[TrailerCameraSwitcher] Active camera: {active.name}", this);
        }

        private void SwitchAudioListeners(Camera active, Camera inactive)
        {
            AudioListener activeListener = active != null ? active.GetComponent<AudioListener>() : null;
            AudioListener inactiveListener = inactive != null ? inactive.GetComponent<AudioListener>() : null;

            if (inactiveListener != null)
                inactiveListener.enabled = false;

            if (activeListener != null)
                activeListener.enabled = true;
        }

        private void RetagActiveCamera(Camera active, Camera inactive)
        {
            if (active == null || inactive == null)
                return;

            try
            {
                inactive.tag = "Untagged";
                active.tag = "MainCamera";
            }
            catch
            {
                // If tags are locked/missing for any reason, camera switching should still work.
            }
        }

        private void UpdateScreenSpaceCameraCanvases(Camera active)
        {
            if (active == null)
                return;

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                    continue;

                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    canvas.worldCamera = active;
            }
        }

        private void ApplyOptionalComponentState(bool trailerActive)
        {
            if (_disableWhileTrailerCameraActive == null)
                return;

            for (int i = 0; i < _disableWhileTrailerCameraActive.Length; i++)
            {
                Behaviour behaviour = _disableWhileTrailerCameraActive[i];
                if (behaviour == null)
                    continue;

                behaviour.enabled = !trailerActive;
            }
        }

        private void PauseTrailerRigs()
        {
            if (_cinematicRig != null)
                _cinematicRig.Pause();

            if (_orbitRig != null)
                _orbitRig.Pause();
        }

        private void AutoFindTrailerCamera()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Camera best = null;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (cam == null || cam == _mainCamera)
                    continue;

                string name = cam.name.ToLowerInvariant();
                if (name.Contains("trailer") || name.Contains("cinematic") || name.Contains("capture"))
                {
                    best = cam;
                    break;
                }
            }

            if (best == null)
            {
                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera cam = cameras[i];
                    if (cam != null && cam != _mainCamera)
                    {
                        best = cam;
                        break;
                    }
                }
            }

            _trailerCamera = best;
        }

        private void AutoFindTrailerRigs()
        {
            if (_cinematicRig == null)
                _cinematicRig = FindFirstObjectByType<TrailerCinematicCameraRig>();

            if (_orbitRig == null)
                _orbitRig = FindFirstObjectByType<TrailerOrbitCameraRig>();
        }

        private void CacheOriginalTags()
        {
            _mainCameraOriginalTag = _mainCamera != null ? _mainCamera.tag : null;
            _trailerCameraOriginalTag = _trailerCamera != null ? _trailerCamera.tag : null;
        }

        private void RestoreOriginalTags()
        {
            try
            {
                if (_mainCamera != null && !string.IsNullOrEmpty(_mainCameraOriginalTag))
                    _mainCamera.tag = _mainCameraOriginalTag;

                if (_trailerCamera != null && !string.IsNullOrEmpty(_trailerCameraOriginalTag))
                    _trailerCamera.tag = _trailerCameraOriginalTag;
            }
            catch
            {
                // Ignore tag restore failures during teardown.
            }
        }

        private void CacheOriginalCanvasCameras()
        {
            _originalCanvasCameras.Clear();

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceCamera)
                    continue;

                if (!_originalCanvasCameras.ContainsKey(canvas))
                    _originalCanvasCameras.Add(canvas, canvas.worldCamera);
            }
        }

        private void RestoreOriginalCanvasCameras()
        {
            foreach (KeyValuePair<Canvas, Camera> pair in _originalCanvasCameras)
            {
                if (pair.Key != null)
                    pair.Key.worldCamera = pair.Value;
            }
        }
    }
}
