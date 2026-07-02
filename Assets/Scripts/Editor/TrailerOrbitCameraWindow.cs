// ============================================================================
// ETD.EditorTools - TrailerOrbitCameraWindow.cs
// Put in: Assets/Scripts/Editor/TrailerOrbitCameraWindow.cs
//
// Editor control panel for TrailerOrbitCameraRig.
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ETD.Marketing;

namespace ETD.EditorTools
{
    public class TrailerOrbitCameraWindow : EditorWindow
    {
        private TrailerOrbitCameraRig _rig;
        private float _jumpTime = 0f;

        [MenuItem("Tools/ETD/Marketing/Trailer Orbit Camera")]
        public static void Open()
        {
            GetWindow<TrailerOrbitCameraWindow>("Trailer Orbit Camera");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Trailer Orbit Camera", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Use identical orbit settings for every maze state. Start each recording from time 0, then cut between clips at matching timestamps.",
                MessageType.Info);

            DrawReference();
            DrawControls();
            DrawCutGuide();
        }

        private void DrawReference()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Reference", EditorStyles.boldLabel);

            _rig = (TrailerOrbitCameraRig)EditorGUILayout.ObjectField("Orbit Rig", _rig, typeof(TrailerOrbitCameraRig), true);

            if (GUILayout.Button("Auto Find Rig"))
                _rig = Object.FindFirstObjectByType<TrailerOrbitCameraRig>();

            if (_rig == null)
                EditorGUILayout.HelpBox("No TrailerOrbitCameraRig found. Add it to an empty GameObject in your gameplay scene.", MessageType.Warning);
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
                        _rig.Stop();
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
                    EditorGUILayout.LabelField($"Elapsed: {_rig.Elapsed:0.00}s");
                    EditorGUILayout.LabelField($"Current Yaw: {_rig.CurrentYaw:0.00}°");
                }
            }
        }

        private void DrawCutGuide()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Editing Guide", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Record every board state with the same orbit settings. Cut from Clip A to Clip B at the same timestamp, for example 3.0s to 3.0s. The camera angle will match.",
                MessageType.None);

            if (GUILayout.Button("Print Recommended Cut Points"))
            {
                Debug.Log("[TrailerOrbitCamera] Matching cut points:");
                Debug.Log("Clip A 2.0s -> Clip B 2.0s");
                Debug.Log("Clip A 4.0s -> Clip B 4.0s");
                Debug.Log("Clip A 6.0s -> Clip B 6.0s");
                Debug.Log("Keep start yaw, orbit speed, radius, height, and target identical.");
            }
        }
    }
}
#endif
