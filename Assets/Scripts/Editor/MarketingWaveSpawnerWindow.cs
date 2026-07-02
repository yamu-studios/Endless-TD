// ============================================================================
// ETD.EditorTools - MarketingWaveSpawnerWindow.cs
// Put in: Assets/Scripts/Editor/MarketingWaveSpawnerWindow.cs
//
// Purpose:
// You manually build your maze and place turrets during a normal run.
// Then you enter a wave number, e.g. 120, and this tool spawns enemies
// using WaveManager's real wave-generation logic for wave 120.
//
// Requires small patch in WaveManager:
// DebugSpawnSpecificWaveForMarketing(...)
// DebugStopMarketingWaveSpawn()
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ETD.Waves;
using ETD.Enemies;

namespace ETD.EditorTools
{
    public class MarketingWaveSpawnerWindow : EditorWindow
    {
        private WaveManager _waveManager;
        private EnemyManager _enemyManager;

        private int _waveNumber = 25;
        private bool _clearExistingEnemies = true;
        private bool _publishWaveStartedEvent = true;
        private bool _forceWaveActiveState = true;

        [MenuItem("Tools/ETD/Marketing/Wave Spawner")]
        public static void Open()
        {
            GetWindow<MarketingWaveSpawnerWindow>("Marketing Wave Spawner");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Marketing Wave Spawner", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Build your maze/turrets manually during a real run. Then enter a wave number and spawn enemies using your real WaveManager generation logic.",
                MessageType.Info);

            DrawReferences();
            DrawSettings();
            DrawControls();
        }

        private void DrawReferences()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);

            _waveManager = (WaveManager)EditorGUILayout.ObjectField("Wave Manager", _waveManager, typeof(WaveManager), true);
            _enemyManager = (EnemyManager)EditorGUILayout.ObjectField("Enemy Manager", _enemyManager, typeof(EnemyManager), true);

            if (GUILayout.Button("Auto Find References"))
            {
                _waveManager = Object.FindFirstObjectByType<WaveManager>();
                _enemyManager = Object.FindFirstObjectByType<EnemyManager>();
            }

            if (EditorApplication.isPlaying && _waveManager == null)
                EditorGUILayout.HelpBox("WaveManager missing. Start a run, then click Auto Find References.", MessageType.Warning);

            if (EditorApplication.isPlaying && _enemyManager == null)
                EditorGUILayout.HelpBox("EnemyManager missing. Start a run, then click Auto Find References.", MessageType.Warning);
        }

        private void DrawSettings()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Wave Settings", EditorStyles.boldLabel);

            _waveNumber = EditorGUILayout.IntField("Wave Number", _waveNumber);
            if (_waveNumber < 1)
                _waveNumber = 1;

            _clearExistingEnemies = EditorGUILayout.Toggle("Clear Existing Enemies", _clearExistingEnemies);
            _publishWaveStartedEvent = EditorGUILayout.Toggle("Publish Wave Started Event", _publishWaveStartedEvent);
            _forceWaveActiveState = EditorGUILayout.Toggle("Force Wave Active State", _forceWaveActiveState);

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Wave 10")) _waveNumber = 10;
                if (GUILayout.Button("Wave 25")) _waveNumber = 25;
                if (GUILayout.Button("Wave 50")) _waveNumber = 50;
                if (GUILayout.Button("Wave 75")) _waveNumber = 75;
                if (GUILayout.Button("Wave 120")) _waveNumber = 120;
            }
        }

        private void DrawControls()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || _waveManager == null))
            {
                if (GUILayout.Button($"Spawn Wave {_waveNumber}", GUILayout.Height(38)))
                {
                    _waveManager.DebugSpawnSpecificWaveForMarketing(
                        _waveNumber,
                        _clearExistingEnemies,
                        _publishWaveStartedEvent,
                        _forceWaveActiveState);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Stop Spawn Coroutine", GUILayout.Height(30)))
                        _waveManager.DebugStopMarketingWaveSpawn();

                    using (new EditorGUI.DisabledScope(_enemyManager == null))
                    {
                        if (GUILayout.Button("Clear Enemies", GUILayout.Height(30)))
                            _enemyManager.ClearAll();
                    }
                }
            }

            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode and start a run first.", MessageType.Warning);
        }
    }
}
#endif
