// ============================================================================
// ETD.Marketing - TrailerRuntimeCameraControls.cs
// Put in: Assets/Scripts/Marketing/TrailerRuntimeCameraControls.cs
//
// Purpose:
// Runtime / Development Build control panel for trailer camera rigs.
// This replaces the EditorWindow workflow when capturing footage from a build.
//
// Works only in the Unity Editor or Development Builds.
// In normal release builds the component disables itself immediately.
// ============================================================================

using UnityEngine;

namespace ETD.Marketing
{
    [DisallowMultipleComponent]
    public sealed class TrailerRuntimeCameraControls : MonoBehaviour
    {
        private enum ActiveRig
        {
            Cinematic,
            Orbit
        }

        [Header("References")]
        [SerializeField] private TrailerCinematicCameraRig _cinematicRig;
        [SerializeField] private TrailerOrbitCameraRig _orbitRig;
        [SerializeField] private TrailerCameraSwitcher _cameraSwitcher;

        [Header("Default Active Rig")]
        [SerializeField] private ActiveRig _activeRig = ActiveRig.Cinematic;

        [Header("Runtime Panel")]
        [SerializeField] private bool _showPanelOnStart = true;
        [SerializeField] private KeyCode _togglePanelKey = KeyCode.F1;
        [SerializeField] private KeyCode _selectCinematicKey = KeyCode.C;
        [SerializeField] private KeyCode _selectOrbitKey = KeyCode.O;
        [SerializeField] private KeyCode _playFromStartKey = KeyCode.P;
        [SerializeField] private KeyCode _pauseKey = KeyCode.Space;
        [SerializeField] private KeyCode _resetKey = KeyCode.R;
        [SerializeField] private KeyCode _stepBackKey = KeyCode.LeftBracket;
        [SerializeField] private KeyCode _stepForwardKey = KeyCode.RightBracket;
        [SerializeField] private float _timeStep = 0.25f;
        [SerializeField] private float _jumpTime = 0f;

        [Header("Capture Build Performance")]
        [Tooltip("Development build capture usually feels better with VSync disabled and a fixed frame-rate target.")]
        [SerializeField] private bool _applyCaptureFrameRateSettings = true;
        [SerializeField] private bool _disableVSync = true;
        [SerializeField] private int _targetFrameRate = 60;
        [SerializeField] private bool _hideCursorDuringPlayback = false;

        [Header("Panel Layout")]
        [SerializeField] private Vector2 _panelPosition = new Vector2(16f, 16f);
        [SerializeField] private Vector2 _panelSize = new Vector2(380f, 520f);

        private bool _showPanel;
        private Rect _panelRect;
        private float _manualTime;
        private GUIStyle _titleStyle;
        private GUIStyle _smallStyle;

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

        private void Awake()
        {
            if (!IsAllowedBuild)
            {
                enabled = false;
                return;
            }

            AutoFindMissingRigs();
            _showPanel = _showPanelOnStart;
            _panelRect = new Rect(_panelPosition.x, _panelPosition.y, _panelSize.x, _panelSize.y);
            _manualTime = GetActiveElapsed();

            if (_applyCaptureFrameRateSettings)
                ApplyCaptureFrameRateSettings();
        }

        private void OnEnable()
        {
            if (!IsAllowedBuild)
            {
                enabled = false;
                return;
            }

            AutoFindMissingRigs();
        }

        private void Update()
        {
            if (!IsAllowedBuild)
                return;

            if (UnityEngine.Input.GetKeyDown(_togglePanelKey))
                _showPanel = !_showPanel;

            if (UnityEngine.Input.GetKeyDown(_selectCinematicKey))
                _activeRig = ActiveRig.Cinematic;

            if (UnityEngine.Input.GetKeyDown(_selectOrbitKey))
                _activeRig = ActiveRig.Orbit;

            if (UnityEngine.Input.GetKeyDown(_playFromStartKey))
                PlayFromStart();

            if (UnityEngine.Input.GetKeyDown(_pauseKey))
                Pause();

            if (UnityEngine.Input.GetKeyDown(_resetKey))
                StopAndReset();

            if (UnityEngine.Input.GetKeyDown(_stepBackKey))
                SetTime(Mathf.Max(0f, GetActiveElapsed() - _timeStep));

            if (UnityEngine.Input.GetKeyDown(_stepForwardKey))
                SetTime(GetActiveElapsed() + _timeStep);

            if (_hideCursorDuringPlayback)
                Cursor.visible = !IsActiveRigPlaying();
        }

        private void OnGUI()
        {
            if (!IsAllowedBuild || !_showPanel)
                return;

            EnsureStyles();
            _panelRect = GUI.Window(GetInstanceID(), _panelRect, DrawWindow, "Trailer Camera");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Space(4f);
            GUILayout.Label("Development Build Trailer Controls", _titleStyle);
            GUILayout.Label("F1 hide/show · F2 camera · 1 main · 2 trailer · C cinematic · O orbit · P play", _smallStyle);

            GUILayout.Space(8f);
            DrawCameraSwitcher();
            DrawRigSelector();
            DrawStatus();
            DrawPlaybackButtons();
            DrawJumpButtons();
            DrawCaptureSettings();

            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }


        private void DrawCameraSwitcher()
        {
            GUILayout.Label("Camera Switch", _titleStyle);

            if (_cameraSwitcher == null)
            {
                GUILayout.Label("Camera switcher: NOT FOUND", _smallStyle);
                if (GUILayout.Button("Auto Find Camera Switcher"))
                    AutoFindMissingRigs();
                GUILayout.Space(8f);
                return;
            }

            GUILayout.Label($"Active camera: {_cameraSwitcher.ActiveCameraName}", _smallStyle);
            GUILayout.Label("F2 toggle · 1 main · 2 trailer", _smallStyle);

            using (new GUILayout.HorizontalScope())
            {
                bool mainActive = !_cameraSwitcher.IsUsingTrailerCamera;
                bool trailerActive = _cameraSwitcher.IsUsingTrailerCamera;

                if (GUILayout.Toggle(mainActive, "Main Camera", "Button", GUILayout.Height(28f)))
                    _cameraSwitcher.SwitchToMainCamera();

                if (GUILayout.Toggle(trailerActive, "Trailer Camera", "Button", GUILayout.Height(28f)))
                    _cameraSwitcher.SwitchToTrailerCamera();
            }

            if (GUILayout.Button("Toggle Camera", GUILayout.Height(26f)))
                _cameraSwitcher.ToggleCamera();

            GUILayout.Space(8f);
        }

        private void DrawRigSelector()
        {
            GUILayout.Label("Active Rig", _titleStyle);

            using (new GUILayout.HorizontalScope())
            {
                bool cinematicSelected = _activeRig == ActiveRig.Cinematic;
                bool orbitSelected = _activeRig == ActiveRig.Orbit;

                if (GUILayout.Toggle(cinematicSelected, "Cinematic", "Button"))
                    _activeRig = ActiveRig.Cinematic;

                if (GUILayout.Toggle(orbitSelected, "Orbit", "Button"))
                    _activeRig = ActiveRig.Orbit;
            }

            if (_cinematicRig == null || _orbitRig == null)
            {
                if (GUILayout.Button("Auto Find Missing Rigs"))
                    AutoFindMissingRigs();
            }
        }

        private void DrawStatus()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Status", _titleStyle);

            if (_activeRig == ActiveRig.Cinematic)
            {
                if (_cinematicRig == null)
                {
                    GUILayout.Label("Cinematic rig: NOT FOUND", _smallStyle);
                    return;
                }

                GUILayout.Label($"Cinematic: {(_cinematicRig.IsPlaying ? "Playing" : "Paused")}", _smallStyle);
                GUILayout.Label($"Elapsed: {_cinematicRig.Elapsed:0.00}s / {_cinematicRig.Duration:0.00}s", _smallStyle);
                GUILayout.Label($"Normalized: {_cinematicRig.NormalizedTime:0.000}", _smallStyle);
                GUILayout.Label($"Yaw: {_cinematicRig.CurrentYaw:0.00}°", _smallStyle);
            }
            else
            {
                if (_orbitRig == null)
                {
                    GUILayout.Label("Orbit rig: NOT FOUND", _smallStyle);
                    return;
                }

                GUILayout.Label($"Orbit: {(_orbitRig.IsPlaying ? "Playing" : "Paused")}", _smallStyle);
                GUILayout.Label($"Elapsed: {_orbitRig.Elapsed:0.00}s", _smallStyle);
                GUILayout.Label($"Yaw: {_orbitRig.CurrentYaw:0.00}°", _smallStyle);
            }
        }

        private void DrawPlaybackButtons()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Playback", _titleStyle);

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play 0s", GUILayout.Height(30f)))
                    PlayFromStart();

                if (GUILayout.Button("Pause", GUILayout.Height(30f)))
                    Pause();

                if (GUILayout.Button("Reset", GUILayout.Height(30f)))
                    StopAndReset();
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"-{_timeStep:0.##}s"))
                    SetTime(Mathf.Max(0f, GetActiveElapsed() - _timeStep));

                if (GUILayout.Button($"+{_timeStep:0.##}s"))
                    SetTime(GetActiveElapsed() + _timeStep);
            }
        }

        private void DrawJumpButtons()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Jump Time", _titleStyle);

            string text = GUILayout.TextField(_jumpTime.ToString("0.###"));
            if (float.TryParse(text, out float parsed))
                _jumpTime = Mathf.Max(0f, parsed);

            if (GUILayout.Button("Jump"))
                SetTime(_jumpTime);

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("0s")) SetTime(0f);
                if (GUILayout.Button("2s")) SetTime(2f);
                if (GUILayout.Button("4s")) SetTime(4f);
                if (GUILayout.Button("6s")) SetTime(6f);
                if (GUILayout.Button("8s")) SetTime(8f);
            }
        }

        private void DrawCaptureSettings()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Capture", _titleStyle);
            GUILayout.Label($"VSync: {QualitySettings.vSyncCount} · Target FPS: {Application.targetFrameRate}", _smallStyle);

            if (GUILayout.Button("Apply Capture FPS Settings"))
                ApplyCaptureFrameRateSettings();
        }

        private void PlayFromStart()
        {
            AutoFindMissingRigs();

            if (_activeRig == ActiveRig.Cinematic)
                _cinematicRig?.PlayFromStart();
            else
                _orbitRig?.PlayFromStart();

            _manualTime = 0f;
        }

        private void Pause()
        {
            if (_activeRig == ActiveRig.Cinematic)
                _cinematicRig?.Pause();
            else
                _orbitRig?.Pause();
        }

        private void StopAndReset()
        {
            if (_activeRig == ActiveRig.Cinematic)
                _cinematicRig?.StopAndReset();
            else
                _orbitRig?.Stop();

            _manualTime = 0f;
        }

        private void SetTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            _manualTime = seconds;

            if (_activeRig == ActiveRig.Cinematic)
                _cinematicRig?.SetTime(seconds);
            else
                _orbitRig?.SetTime(seconds);
        }

        private float GetActiveElapsed()
        {
            if (_activeRig == ActiveRig.Cinematic)
                return _cinematicRig != null ? _cinematicRig.Elapsed : _manualTime;

            return _orbitRig != null ? _orbitRig.Elapsed : _manualTime;
        }

        private bool IsActiveRigPlaying()
        {
            if (_activeRig == ActiveRig.Cinematic)
                return _cinematicRig != null && _cinematicRig.IsPlaying;

            return _orbitRig != null && _orbitRig.IsPlaying;
        }

        private void AutoFindMissingRigs()
        {
            if (_cinematicRig == null)
                _cinematicRig = FindFirstObjectByType<TrailerCinematicCameraRig>();

            if (_orbitRig == null)
                _orbitRig = FindFirstObjectByType<TrailerOrbitCameraRig>();

            if (_cameraSwitcher == null)
                _cameraSwitcher = FindFirstObjectByType<TrailerCameraSwitcher>();
        }

        private void ApplyCaptureFrameRateSettings()
        {
            if (_disableVSync)
                QualitySettings.vSyncCount = 0;

            if (_targetFrameRate > 0)
                Application.targetFrameRate = _targetFrameRate;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
                return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13
            };

            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true
            };
        }
    }
}
