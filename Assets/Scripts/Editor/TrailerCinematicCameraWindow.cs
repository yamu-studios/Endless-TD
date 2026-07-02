// ============================================================================
// ETD.EditorTools - TrailerCinematicCameraWindow.cs
// Put in: Assets/Scripts/Editor/TrailerCinematicCameraWindow.cs
//
// Editor control panel for TrailerCinematicCameraRig.
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ETD.Marketing;

namespace ETD.EditorTools
{
    public class TrailerCinematicCameraWindow : EditorWindow
    {
        private TrailerCinematicCameraRig _rig;
        private float _jumpTime = 0f;

        [MenuItem("Tools/ETD/Marketing/Trailer Cinematic Camera")]
        public static void Open()
        {
            GetWindow<TrailerCinematicCameraWindow>("Trailer Cinematic Camera");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Trailer Cinematic Camera", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Modular trailer camera. Orbit, pan, target movement, zoom, height, and lens animation are optional. For seamless board-change edits, keep all settings identical and cut at matching timestamps.",
                MessageType.Info);

            DrawReference();
            DrawControls();
            DrawShotRecipes();
            DrawCutGuide();
        }

        private void DrawReference()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Reference", EditorStyles.boldLabel);

            _rig = (TrailerCinematicCameraRig)EditorGUILayout.ObjectField("Cinematic Rig", _rig, typeof(TrailerCinematicCameraRig), true);

            if (GUILayout.Button("Auto Find Rig"))
                _rig = Object.FindFirstObjectByType<TrailerCinematicCameraRig>();

            if (_rig == null)
                EditorGUILayout.HelpBox("No TrailerCinematicCameraRig found. Add it to an empty GameObject in your gameplay scene.", MessageType.Warning);
        }

        private void DrawControls()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(_rig == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Play From Start", GUILayout.Height(32)))
                        _rig.PlayFromStart();

                    if (GUILayout.Button("Pause", GUILayout.Height(32)))
                        _rig.Pause();

                    if (GUILayout.Button("Stop + Reset", GUILayout.Height(32)))
                        _rig.StopAndReset();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _jumpTime = EditorGUILayout.FloatField("Jump Time", _jumpTime);

                    if (GUILayout.Button("Jump"))
                        _rig.SetTime(_jumpTime);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("0s")) _rig.SetTime(0f);
                    if (GUILayout.Button("2s")) _rig.SetTime(2f);
                    if (GUILayout.Button("4s")) _rig.SetTime(4f);
                    if (GUILayout.Button("6s")) _rig.SetTime(6f);
                    if (GUILayout.Button("8s")) _rig.SetTime(8f);
                }

                if (_rig != null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField($"Elapsed: {_rig.Elapsed:0.00}s / {_rig.Duration:0.00}s");
                    EditorGUILayout.LabelField($"Normalized: {_rig.NormalizedTime:0.000}");
                    EditorGUILayout.LabelField($"Current Yaw: {_rig.CurrentYaw:0.00}°");
                }
            }
        }

        private void DrawShotRecipes()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Good Trailer Shot Recipes", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "1) Pure Pan: Orbit OFF, Target Pan ON. Great for scanning the 9x20 board.\n" +
                "2) Orbit Reveal: Orbit ON, Zoom ON. Great for hero shots.\n" +
                "3) Crane Push: Orbit OFF, Camera Pan ON, Height ON, Zoom ON. Good for dramatic late-game reveal.\n" +
                "4) Seamless Transform: Use exact same settings for each board state and cut at same timestamp.",
                MessageType.None);
        }

        private void DrawCutGuide()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Editing Guide", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "For no-cut illusion: record every board state from time 0 with identical camera settings. In the video editor, cut from Clip A to Clip B at the same timestamp, e.g. 3.0s to 3.0s.",
                MessageType.None);

            if (GUILayout.Button("Print Recommended Cut Points"))
            {
                Debug.Log("[TrailerCinematicCamera] Matching cut points:");
                Debug.Log("Clip A 2.0s -> Clip B 2.0s");
                Debug.Log("Clip A 4.0s -> Clip B 4.0s");
                Debug.Log("Clip A 6.0s -> Clip B 6.0s");
                Debug.Log("Keep target, duration, orbit, pan, zoom, height, and lens settings identical.");
            }
        }
    }
}
#endif
