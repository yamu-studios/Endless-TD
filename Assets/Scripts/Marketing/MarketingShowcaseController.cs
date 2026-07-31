// ============================================================================
// ETD.Marketing - MarketingShowcaseController.cs  [PROJECT-SPECIFIC]
// Put in: Assets/Scripts/Marketing/MarketingShowcaseController.cs
//
// This version is based on your actual uploaded scripts:
// - EnemyManager.SpawnEnemy(EnemyData, EnemyTier, int, float, bool, Vector3?)
// - EnemyManager.ClearAll()
// - TurretManager.PlaceTurret(TurretData, Vector2Int)
// - TurretManager.EvolveTurret(int, int)
// - TurretManager.ClearAll()
// - GridSystem.SetCellState(...), GridToWorld(...), GetCell(...)
// - AStarPathfinder.RecalculatePath()
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;
using ETD.Grid;
using ETD.Pathfinding;
using ETD.Enemies;
using ETD.Turrets;
using ETD.Waves;
using ETD.Gameplay;

namespace ETD.Marketing
{
    public class MarketingShowcaseController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameDatabase _database;
        [SerializeField] private RunManager _runManager;
        [SerializeField] private GridSystem _gridSystem;
        [SerializeField] private AStarPathfinder _pathfinder;
        [SerializeField] private TurretManager _turretManager;
        [SerializeField] private EnemyManager _enemyManager;
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private Camera _captureCamera;

        [Header("Capture Cleanup")]
        [SerializeField] private Canvas[] _debugCanvasesToHide;
        [SerializeField] private GameObject[] _objectsToHideForCapture;
        [SerializeField] private bool _verboseLogs = true;

        private Coroutine _spawnRoutine;

        public static MarketingShowcaseController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            ResolveMissingReferences();
        }

        private void ResolveMissingReferences()
        {
            if (_captureCamera == null) _captureCamera = Camera.main;
            if (_runManager == null) _runManager = FindFirstObjectByType<RunManager>();
            if (_gridSystem == null) _gridSystem = FindFirstObjectByType<GridSystem>();
            if (_pathfinder == null) _pathfinder = FindFirstObjectByType<AStarPathfinder>();
            if (_turretManager == null) _turretManager = FindFirstObjectByType<TurretManager>();
            if (_enemyManager == null) _enemyManager = FindFirstObjectByType<EnemyManager>();
            if (_waveManager == null) _waveManager = FindFirstObjectByType<WaveManager>();
        }

        public void ApplyScenario(ShowcaseScenarioData scenario)
        {
            if (scenario == null)
            {
                Debug.LogError("[MarketingShowcase] Null scenario.", this);
                return;
            }

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            Debug.LogWarning("[MarketingShowcase] Disabled outside Editor/Development builds.", this);
            return;
#endif

            ResolveMissingReferences();
            StopActiveScenario();

            Log($"Applying: {scenario.DisplayName}");

            Time.timeScale = scenario.TimeScale <= 0f ? 1f : scenario.TimeScale;
            Cursor.visible = !scenario.HideCursor;

            ApplyCamera(scenario);
            ApplyCaptureVisibility(scenario);
            ApplyRunState(scenario);

            if (scenario.ClearExistingEnemies)
                _enemyManager?.ClearAll();

            // Clear turrets before board blockers, so GridSystem cells are released.
            if (_turretManager != null)
                _turretManager.ClearAll();

            ApplyBoardBlocks(scenario);
            ApplyDynamicTiles(scenario);
            ApplyTurrets(scenario);
            RecalculatePath();
            ApplyEnemySpawns(scenario);

            if (scenario.PauseAfterSetup)
                Time.timeScale = 0f;

            Log($"Applied: {scenario.DisplayName}");
        }

        public void StopActiveScenario()
        {
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }

            Time.timeScale = 1f;
            Cursor.visible = true;
        }

        private void ApplyCamera(ShowcaseScenarioData scenario)
        {
            if (_captureCamera == null) return;

            _captureCamera.transform.position = scenario.CameraPosition;
            _captureCamera.transform.rotation = Quaternion.Euler(scenario.CameraEulerAngles);

            if (_captureCamera.orthographic)
                _captureCamera.orthographicSize = scenario.CameraOrthographicSize;
        }

        private void ApplyCaptureVisibility(ShowcaseScenarioData scenario)
        {
            if (_debugCanvasesToHide != null)
            {
                foreach (var canvas in _debugCanvasesToHide)
                    if (canvas != null) canvas.enabled = !scenario.HideDebugUI;
            }

            if (_objectsToHideForCapture != null)
            {
                foreach (var go in _objectsToHideForCapture)
                    if (go != null) go.SetActive(!scenario.HideDebugUI);
            }
        }

        private void ApplyRunState(ShowcaseScenarioData scenario)
        {
            if (_runManager?.RunData == null)
                return;

            var runData = _runManager.RunData;
            runData.CurrentWave = Mathf.Max(0, scenario.TargetWave);
            runData.Gold = Mathf.Max(0, scenario.StartingGold);
            runData.Lives = Mathf.Max(1, scenario.StartingLives);
            runData.MaxLives = Mathf.Max(runData.MaxLives, runData.Lives);
            runData.Level = Mathf.Max(1, scenario.StartingLevel);
            runData.CurrentXP = Mathf.Max(0f, scenario.StartingXP);

            if (scenario.SelectedTraitIds != null && scenario.SelectedTraitIds.Length > 0)
            {
                runData.ActiveTraitIds.Clear();
                runData.ActiveTraitIds.AddRange(scenario.SelectedTraitIds);
            }

            EventBus.Publish(new GoldChangedEvent { Current = runData.Gold, Delta = 0 });
            EventBus.Publish(new LivesChangedEvent { Current = runData.Lives, Max = runData.MaxLives });
            EventBus.Publish(new WaveStartedEvent { WaveNumber = runData.CurrentWave });

            // For WaveManager.CurrentWave, add the project-specific debug method from the patch file.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_waveManager != null)
                _waveManager.DebugSetCurrentWaveForShowcase(runData.CurrentWave);
#endif
        }

        private void ApplyBoardBlocks(ShowcaseScenarioData scenario)
        {
            if (_gridSystem == null || scenario.BlockedTiles == null)
                return;

            foreach (var tile in scenario.BlockedTiles)
            {
                if (!_gridSystem.HasCell(tile.GridPosition))
                    continue;

                _gridSystem.SetCellState(
                    tile.GridPosition,
                    tile.IsBlocked ? CellState.Blocked : CellState.Empty
                );
            }
        }

        private void ApplyDynamicTiles(ShowcaseScenarioData scenario)
        {
            if (_gridSystem == null || _database == null || scenario.DynamicTiles == null)
                return;

            foreach (var placement in scenario.DynamicTiles)
            {
                var data = FindDynamicTileById(placement.DynamicTileId);
                if (data == null)
                {
                    LogWarning($"Dynamic tile not found: {placement.DynamicTileId}");
                    continue;
                }

                var cell = _gridSystem.GetCell(placement.GridPosition);
                if (cell == null)
                {
                    LogWarning($"No grid cell at {placement.GridPosition} for dynamic tile {placement.DynamicTileId}");
                    continue;
                }

                cell.Specialty = data.Category;
                cell.DynamicTile = data;

                ETD.Grid.GridSystem.ApplyTileVisual(cell, data);

                EventBus.Publish(new TileSpecialtyAppliedEvent
                {
                    GridPos = placement.GridPosition,
                    Specialty = (int)data.Category
                });
            }
        }

        private void ApplyTurrets(ShowcaseScenarioData scenario)
        {
            if (_turretManager == null || _database == null || scenario.Turrets == null)
                return;

            foreach (var placement in scenario.Turrets)
            {
                TurretData data = _database.GetTurret(placement.TurretId);
                if (data == null)
                {
                    LogWarning($"Turret not found: {placement.TurretId}");
                    continue;
                }

                TurretController turret = _turretManager.PlaceTurret(data, placement.GridPosition);
                if (turret == null)
                {
                    LogWarning($"Could not place {placement.TurretId} at {placement.GridPosition}. It may block the path or cell is invalid.");
                    continue;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                turret.DebugSetLevelForShowcase(Mathf.Max(1, placement.Level));
#endif

                if (placement.IsEvolved && data.Type != TurretType.Radar)
                {
                    int path = Mathf.Clamp(placement.EvolutionPath, 0, 1);
                    TurretController evolved = _turretManager.EvolveTurret(turret.InstanceId, path);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (evolved != null)
                        evolved.DebugSetLevelForShowcase(Mathf.Max(1, placement.Level));
#endif
                }
            }
        }

        private void RecalculatePath()
        {
            if (_pathfinder != null)
                _pathfinder.RecalculatePath();
        }

        private void ApplyEnemySpawns(ShowcaseScenarioData scenario)
        {
            if (_enemyManager == null || _database == null || scenario.EnemySpawns == null || scenario.EnemySpawns.Count == 0)
                return;

            _spawnRoutine = StartCoroutine(SpawnEnemiesRoutine(scenario));
        }

        private IEnumerator SpawnEnemiesRoutine(ShowcaseScenarioData scenario)
        {
            foreach (var group in scenario.EnemySpawns)
            {
                EnemyData data = _database.GetEnemy(group.EnemyId);
                if (data == null)
                {
                    LogWarning($"Enemy not found: {group.EnemyId}");
                    continue;
                }

                if (group.InitialDelay > 0f)
                    yield return new WaitForSeconds(group.InitialDelay);

                int count = Mathf.Max(0, group.Count);
                float interval = Mathf.Max(0f, group.SpawnInterval);
                int wave = Mathf.Max(1, scenario.TargetWave);

                for (int i = 0; i < count; i++)
                {
                    _enemyManager.SpawnEnemy(data, group.Tier, wave, interval);

                    if (interval > 0f)
                        yield return new WaitForSeconds(interval);
                }
            }

            _spawnRoutine = null;
        }

        private DynamicTileData FindDynamicTileById(string id)
        {
            if (_database?.DynamicTiles == null || string.IsNullOrWhiteSpace(id))
                return null;

            for (int i = 0; i < _database.DynamicTiles.Length; i++)
            {
                var tile = _database.DynamicTiles[i];
                if (tile != null && tile.Id == id)
                    return tile;
            }

            return null;
        }

        private void Log(string msg)
        {
            if (_verboseLogs)
                Debug.Log($"[MarketingShowcase] {msg}", this);
        }

        private void LogWarning(string msg)
        {
            Debug.LogWarning($"[MarketingShowcase] {msg}", this);
        }
    }
}
