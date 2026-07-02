// ============================================================================
// ETD.Marketing - ShowcaseScenarioData.cs  [PROJECT-SPECIFIC]
// Put in: Assets/Scripts/Marketing/ShowcaseScenarioData.cs
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ETD.Data;

namespace ETD.Marketing
{
    public enum ShowcaseScenarioType
    {
        EarlyMaze,
        MazeKillZone,
        MidGameEvolutions,
        LateGameBoard,
        DenseEnemyWave,
        TraitBuild,
        DynamicTiles,
        TrailerHeroShot
    }

    [CreateAssetMenu(menuName = "ETD/Marketing/Showcase Scenario", fileName = "ShowcaseScenario_")]
    public class ShowcaseScenarioData : ScriptableObject
    {
        [Header("Identity")]
        public string Id = "showcase_late_game";
        public string DisplayName = "Late Game Showcase";
        [TextArea(2, 5)] public string Notes;

        [Header("Run State")]
        public ShowcaseScenarioType ScenarioType;
        public int TargetWave = 40;
        public int StartingGold = 10000;
        public int StartingLives = 20;
        public int StartingXP = 0;
        public int StartingLevel = 1;

        [Header("Camera")]
        public Vector3 CameraPosition = new Vector3(0f, 18f, -12f);
        public Vector3 CameraEulerAngles = new Vector3(65f, 0f, 0f);
        public float CameraOrthographicSize = 10f;

        [Header("Traits / Loadout")]
        public string[] SelectedTurretIds;
        public string[] SelectedTraitIds;
        public string[] ForcedSpecCardIds;

        [Header("Board / Maze")]
        public List<ShowcaseTilePlacement> BlockedTiles = new();
        public List<ShowcaseDynamicTilePlacement> DynamicTiles = new();
        public List<ShowcaseTurretPlacement> Turrets = new();

        [Header("Enemies / Density")]
        public bool ClearExistingEnemies = true;
        public List<ShowcaseEnemySpawn> EnemySpawns = new();

        [Header("Capture")]
        public bool HideDebugUI = true;
        public bool HideCursor = true;
        public bool PauseAfterSetup = false;
        public float TimeScale = 1f;
    }

    [Serializable]
    public struct ShowcaseTilePlacement
    {
        public Vector2Int GridPosition;
        public bool IsBlocked;
    }

    [Serializable]
    public struct ShowcaseTurretPlacement
    {
        public string TurretId;
        public Vector2Int GridPosition;
        public int Level;
        public bool IsEvolved;
        [Tooltip("0 = Path A, 1 = Path B")]
        public int EvolutionPath;
    }

    [Serializable]
    public struct ShowcaseDynamicTilePlacement
    {
        public string DynamicTileId;
        public Vector2Int GridPosition;
    }

    [Serializable]
    public struct ShowcaseEnemySpawn
    {
        public string EnemyId;
        public EnemyTier Tier;
        public int Count;
        public float SpawnInterval;
        public float InitialDelay;
    }
}
