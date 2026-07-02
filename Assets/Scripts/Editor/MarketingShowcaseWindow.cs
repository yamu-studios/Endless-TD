// ============================================================================
// ETD.EditorTools - MarketingShowcaseWindow.cs
// Put in: Assets/Scripts/Editor/MarketingShowcaseWindow.cs
// Purpose: Big-button window for jumping to screenshot/trailer scenarios.
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ETD.Marketing;

namespace ETD.EditorTools
{
    public class MarketingShowcaseWindow : EditorWindow
    {
        private Vector2 _scroll;
        private List<ShowcaseScenarioData> _scenarios = new();
        private MarketingShowcaseController _controller;

        [MenuItem("Tools/ETD/Marketing/Showcase Window")]
        public static void Open()
        {
            GetWindow<MarketingShowcaseWindow>("Marketing Showcase");
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Steam Marketing Showcase", EditorStyles.boldLabel);

                if (GUILayout.Button("Refresh", GUILayout.Width(90)))
                    Refresh();
            }

            EditorGUILayout.HelpBox(
                "Use this in Play Mode. Create ShowcaseScenarioData assets, then click a scenario to prepare a screenshot/trailer state.",
                MessageType.Info);

            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode first.", MessageType.Warning);

            _controller = FindFirstObjectByType<MarketingShowcaseController>();

            if (EditorApplication.isPlaying && _controller == null)
            {
                EditorGUILayout.HelpBox(
                    "No MarketingShowcaseController found in scene. Add it to a GameObject and assign references.",
                    MessageType.Error);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawQuickChecklist();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Scenarios", EditorStyles.boldLabel);

            if (_scenarios.Count == 0)
                EditorGUILayout.HelpBox("No ShowcaseScenarioData assets found. Create them via Assets > Create > ETD > Marketing > Showcase Scenario.", MessageType.Warning);

            foreach (var scenario in _scenarios)
            {
                if (scenario == null) continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(scenario.DisplayName, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Type: {scenario.ScenarioType}");
                    EditorGUILayout.LabelField($"Wave: {scenario.TargetWave} | Gold: {scenario.StartingGold} | Lives: {scenario.StartingLives}");

                    if (!string.IsNullOrWhiteSpace(scenario.Notes))
                        EditorGUILayout.HelpBox(scenario.Notes, MessageType.None);

                    using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || _controller == null))
                    {
                        if (GUILayout.Button($"Apply: {scenario.DisplayName}", GUILayout.Height(32)))
                            _controller.ApplyScenario(scenario);
                    }

                    if (GUILayout.Button("Ping Asset"))
                        EditorGUIUtility.PingObject(scenario);
                }
            }

            EditorGUILayout.Space(10);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || _controller == null))
            {
                if (GUILayout.Button("Stop Active Showcase / Reset Time", GUILayout.Height(28)))
                    _controller.StopActiveScenario();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawQuickChecklist()
        {
            EditorGUILayout.LabelField("Capture Checklist", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("✓ 1920×1080 or higher");
            EditorGUILayout.LabelField("✓ No debug UI");
            EditorGUILayout.LabelField("✓ Clear maze shape visible");
            EditorGUILayout.LabelField("✓ Best VFX quality");
            EditorGUILayout.LabelField("✓ Trailer clips: 5–8 seconds each");
            EditorGUILayout.LabelField("✓ Screenshots: hero, maze, evolution, choices, late-game");
        }

        private void Refresh()
        {
            _scenarios.Clear();

            string[] guids = AssetDatabase.FindAssets("t:ShowcaseScenarioData", new[] { "Assets" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scenario = AssetDatabase.LoadAssetAtPath<ShowcaseScenarioData>(path);
                if (scenario != null)
                    _scenarios.Add(scenario);
            }

            _scenarios.Sort((a, b) =>
            {
                int type = a.ScenarioType.CompareTo(b.ScenarioType);
                if (type != 0) return type;
                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
            });

            Repaint();
        }
    }
}
#endif
