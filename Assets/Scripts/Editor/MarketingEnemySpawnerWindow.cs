// ============================================================================
// ETD.EditorTools - MarketingEnemySpawnerWindow.cs
// Put in: Assets/Scripts/Editor/MarketingEnemySpawnerWindow.cs
//
// Purpose:
// You manually place turrets/build the maze during a real run.
// This tool only spawns controlled enemy groups for Steam screenshots/trailer clips.
//
// Uses your existing EnemyManager method:
// SpawnEnemy(EnemyData data, EnemyTier tier, int waveNumber, float spawnDelay,
//            bool hasPos = false, Vector3? fixedPos = null)
//
// Also uses EnemyManager.ClearAll().
// ============================================================================

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using ETD.Data;
using ETD.Enemies;

namespace ETD.EditorTools
{
    public class MarketingEnemySpawnerWindow : EditorWindow
    {
        private GameDatabase _database;
        private EnemyManager _enemyManager;

        private Vector2 _scroll;

        private EnemyData _enemyA;
        private EnemyData _enemyB;
        private EnemyData _enemyC;

        private EnemyTier _tierA = EnemyTier.Normal;
        private EnemyTier _tierB = EnemyTier.Normal;
        private EnemyTier _tierC = EnemyTier.Elite;

        private int _waveNumber = 25;

        private int _countA = 20;
        private int _countB = 10;
        private int _countC = 3;

        private float _intervalA = 0.08f;
        private float _intervalB = 0.14f;
        private float _intervalC = 0.45f;

        private float _delayA = 0f;
        private float _delayB = 1f;
        private float _delayC = 3f;

        private bool _useFixedSpawnPosition;
        private Vector3 _fixedSpawnPosition;

        private bool _isSpawning;
        private EditorCoroutine _spawnRoutine;

        [MenuItem("Tools/ETD/Marketing/Enemy Spawner")]
        public static void Open()
        {
            GetWindow<MarketingEnemySpawnerWindow>("Marketing Enemy Spawner");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Marketing Enemy Spawner", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Use this while in Play Mode. Build your maze/turrets manually, then spawn controlled enemy groups for screenshots and trailer clips.",
                MessageType.Info);

            DrawReferences();
            DrawWaveSettings();
            DrawEnemyGroups();
            DrawPresetButtons();
            DrawSpawnControls();
        }

        private void DrawReferences()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);

            _database = (GameDatabase)EditorGUILayout.ObjectField("Game Database", _database, typeof(GameDatabase), false);
            _enemyManager = (EnemyManager)EditorGUILayout.ObjectField("Enemy Manager", _enemyManager, typeof(EnemyManager), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto Find References"))
                    AutoFindReferences();

                if (GUILayout.Button("Load Common Enemies"))
                    LoadCommonEnemies();
            }

            if (EditorApplication.isPlaying && _enemyManager == null)
                EditorGUILayout.HelpBox("EnemyManager is missing. Click Auto Find References while in a run.", MessageType.Warning);
        }

        private void DrawWaveSettings()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Wave / Spawn Settings", EditorStyles.boldLabel);

            _waveNumber = EditorGUILayout.IntField("Wave Number", _waveNumber);
            if (_waveNumber < 1) _waveNumber = 1;

            _useFixedSpawnPosition = EditorGUILayout.Toggle("Use Fixed Spawn Position", _useFixedSpawnPosition);

            using (new EditorGUI.DisabledScope(!_useFixedSpawnPosition))
            {
                _fixedSpawnPosition = EditorGUILayout.Vector3Field("Fixed Spawn Position", _fixedSpawnPosition);
            }
        }

        private void DrawEnemyGroups()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Enemy Groups", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(260));

            DrawGroup("Group A", ref _enemyA, ref _tierA, ref _countA, ref _intervalA, ref _delayA);
            DrawGroup("Group B", ref _enemyB, ref _tierB, ref _countB, ref _intervalB, ref _delayB);
            DrawGroup("Group C", ref _enemyC, ref _tierC, ref _countC, ref _intervalC, ref _delayC);

            EditorGUILayout.EndScrollView();
        }

        private void DrawGroup(
            string label,
            ref EnemyData enemy,
            ref EnemyTier tier,
            ref int count,
            ref float interval,
            ref float delay)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

                enemy = (EnemyData)EditorGUILayout.ObjectField("Enemy", enemy, typeof(EnemyData), false);
                tier = (EnemyTier)EditorGUILayout.EnumPopup("Tier", tier);
                count = EditorGUILayout.IntField("Count", count);
                interval = EditorGUILayout.FloatField("Interval", interval);
                delay = EditorGUILayout.FloatField("Initial Delay", delay);

                if (count < 0) count = 0;
                if (interval < 0f) interval = 0f;
                if (delay < 0f) delay = 0f;
            }
        }

        private void DrawPresetButtons()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Fast Marketing Presets", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clean Early Wave"))
                {
                    _waveNumber = 10;
                    _countA = 18; _intervalA = 0.16f; _delayA = 0f;
                    _countB = 6;  _intervalB = 0.25f; _delayB = 1.5f;
                    _countC = 0;
                }

                if (GUILayout.Button("Hero Maze Shot"))
                {
                    _waveNumber = 25;
                    _countA = 28; _intervalA = 0.08f; _delayA = 0f;
                    _countB = 14; _intervalB = 0.12f; _delayB = 1f;
                    _countC = 4;  _intervalC = 0.45f; _delayC = 3f;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Dense Trailer Wave"))
                {
                    _waveNumber = 55;
                    _countA = 55; _intervalA = 0.04f; _delayA = 0f;
                    _countB = 30; _intervalB = 0.06f; _delayB = 0.8f;
                    _countC = 8;  _intervalC = 0.25f; _delayC = 2.5f;
                }

                if (GUILayout.Button("Elite Pressure"))
                {
                    _waveNumber = 70;
                    _countA = 25; _intervalA = 0.08f; _delayA = 0f;
                    _countB = 16; _intervalB = 0.12f; _delayB = 1f;
                    _countC = 8;  _intervalC = 0.4f; _delayC = 2f;
                    _tierC = EnemyTier.Elite;
                }
            }
        }

        private void DrawSpawnControls()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || _enemyManager == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (!_isSpawning)
                    {
                        if (GUILayout.Button("Spawn Groups", GUILayout.Height(34)))
                            StartSpawning();
                    }
                    else
                    {
                        if (GUILayout.Button("Stop Spawning", GUILayout.Height(34)))
                            StopSpawning();
                    }

                    if (GUILayout.Button("Clear Enemies", GUILayout.Height(34)))
                        _enemyManager.ClearAll();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Spawn A Only"))
                        StartSingleGroup(_enemyA, _tierA, _countA, _intervalA, _delayA);

                    if (GUILayout.Button("Spawn B Only"))
                        StartSingleGroup(_enemyB, _tierB, _countB, _intervalB, _delayB);

                    if (GUILayout.Button("Spawn C Only"))
                        StartSingleGroup(_enemyC, _tierC, _countC, _intervalC, _delayC);
                }
            }

            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode and start a run before spawning enemies.", MessageType.Warning);
        }

        private void AutoFindReferences()
        {
            if (_enemyManager == null)
                _enemyManager = Object.FindFirstObjectByType<EnemyManager>();

            if (_database == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:GameDatabase", new[] { "Assets" });
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _database = AssetDatabase.LoadAssetAtPath<GameDatabase>(path);
                }
            }
        }

        private void LoadCommonEnemies()
        {
            if (_database == null)
                AutoFindReferences();

            if (_database == null || _database.Enemies == null)
            {
                Debug.LogWarning("[MarketingEnemySpawner] GameDatabase or Enemies list missing.");
                return;
            }

            _enemyA = FindEnemy("enemy_basic", "basic") ?? FirstEnemy();
            _enemyB = FindEnemy("enemy_fast", "fast") ?? FirstEnemy();
            _enemyC = FindEnemy("enemy_tank", "tank") ?? FirstEnemy();
        }

        private EnemyData FindEnemy(string idContains, string nameContains)
        {
            if (_database == null || _database.Enemies == null)
                return null;

            foreach (var e in _database.Enemies)
            {
                if (e == null) continue;

                string id = e.Id == null ? string.Empty : e.Id.ToLowerInvariant();
                string name = e.name == null ? string.Empty : e.name.ToLowerInvariant();

                if (id.Contains(idContains.ToLowerInvariant()) || name.Contains(nameContains.ToLowerInvariant()))
                    return e;
            }

            return null;
        }

        private EnemyData FirstEnemy()
        {
            if (_database == null || _database.Enemies == null)
                return null;

            foreach (var e in _database.Enemies)
            {
                if (e != null)
                    return e;
            }

            return null;
        }

        private void StartSpawning()
        {
            StopSpawning();
            _isSpawning = true;
            _spawnRoutine = EditorCoroutineUtility.StartCoroutineOwnerless(SpawnAllGroups());
        }

        private void StartSingleGroup(EnemyData enemy, EnemyTier tier, int count, float interval, float delay)
        {
            StopSpawning();
            _isSpawning = true;
            _spawnRoutine = EditorCoroutineUtility.StartCoroutineOwnerless(SpawnGroup(enemy, tier, count, interval, delay));
        }

        private void StopSpawning()
        {
            _isSpawning = false;

            if (_spawnRoutine != null)
            {
                EditorCoroutineUtility.StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
        }

        private IEnumerator SpawnAllGroups()
        {
            List<EditorCoroutine> routines = new();

            if (_enemyA != null && _countA > 0)
                routines.Add(EditorCoroutineUtility.StartCoroutineOwnerless(SpawnGroup(_enemyA, _tierA, _countA, _intervalA, _delayA)));

            if (_enemyB != null && _countB > 0)
                routines.Add(EditorCoroutineUtility.StartCoroutineOwnerless(SpawnGroup(_enemyB, _tierB, _countB, _intervalB, _delayB)));

            if (_enemyC != null && _countC > 0)
                routines.Add(EditorCoroutineUtility.StartCoroutineOwnerless(SpawnGroup(_enemyC, _tierC, _countC, _intervalC, _delayC)));

            float maxDuration =
                Mathf.Max(_delayA + _countA * _intervalA,
                Mathf.Max(_delayB + _countB * _intervalB,
                          _delayC + _countC * _intervalC));

            double start = EditorApplication.timeSinceStartup;
            while (_isSpawning && EditorApplication.timeSinceStartup - start < maxDuration + 0.5f)
                yield return null;

            _isSpawning = false;
            _spawnRoutine = null;
        }

        private IEnumerator SpawnGroup(EnemyData enemy, EnemyTier tier, int count, float interval, float delay)
        {
            if (enemy == null || _enemyManager == null)
            {
                _isSpawning = false;
                yield break;
            }

            if (delay > 0f)
            {
                double delayStart = EditorApplication.timeSinceStartup;
                while (_isSpawning && EditorApplication.timeSinceStartup - delayStart < delay)
                    yield return null;
            }

            for (int i = 0; i < count && _isSpawning; i++)
            {
                if (_useFixedSpawnPosition)
                {
                    _enemyManager.SpawnEnemy(enemy, tier, _waveNumber, 0f, true, _fixedSpawnPosition);
                }
                else
                {
                    _enemyManager.SpawnEnemy(enemy, tier, _waveNumber, 0f);
                }

                if (interval > 0f)
                {
                    double start = EditorApplication.timeSinceStartup;
                    while (_isSpawning && EditorApplication.timeSinceStartup - start < interval)
                        yield return null;
                }
            }
        }
    }
}
#endif
