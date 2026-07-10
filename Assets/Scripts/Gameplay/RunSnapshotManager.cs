using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;
using ETD.Turrets;
using ETD.Waves;
using ETD.Grid;
using ETD.Enemies;

namespace ETD.Gameplay
{
    public class RunSnapshotManager : MonoBehaviour
    {
        [SerializeField] private RunManager _runManager;
        [SerializeField] private TurretManager _turretManager;
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private GridSystem _gridSystem;
        [SerializeField] private GameDatabase _database;

        private void Awake()
        {
            EventBus.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
            EventBus.Subscribe<GameOverEvent>(OnGameOver);
            EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<RetryEvent>(OnRetry);
        }

        // =================================================================
        // SAVE
        // =================================================================

        public void SaveSnapshot()
        {
            var run = _runManager?.RunData;
            if (run == null) return;

            if (!CanCreateStableSnapshot())
                return;

            // Store the next wave to start. WaveManager.SetWave(x) stores x-1 internally,
            // then StartNextWave() increments back to x. After completing wave N, resume should start N+1.
            int currentWave = _waveManager != null ? _waveManager.CurrentWave : run.CurrentWave;
            int nextWave = Mathf.Max(1, currentWave + 1);

            var snap = new RunSnapshot
            {
                Wave = nextWave,
                Gold = run.Gold,
                Lives = run.Lives,
                MaxLives = run.MaxLives,
                Level = run.Level,
                XP = Mathf.RoundToInt(run.CurrentXP),
                XPToNextLevel = Mathf.RoundToInt(run.XPToNextLevel),
                Score = run.Score,
                TotalTime = run.TotalTime,                // FIX 4: save time
                TurretsPlaced = run.TurretsPlaced,        // FIX: preserve trait "per owned turret"
                TotalGoldSpent = run.TotalGoldSpent,      // FIX: preserve trait "per gold spent"
                RerollTokens = run.ActiveRerollTokens,
                ActiveTraitIds = run.ActiveTraitIds?.ToArray()
                              ?? System.Array.Empty<string>(),
                ActivePermanentBonuses = run.ActivePermanentBonuses != null
                              ? (float[])run.ActivePermanentBonuses.Clone()
                              : new float[10]
            };

            // FIX 2: Save level-up pending state + spec card options
            bool levelUpPending = GameManager.Instance != null
                && GameManager.Instance.CurrentState == GameState.LevelUp;
            snap.IsLevelUpPending = levelUpPending;

            if (levelUpPending && _runManager.CurrentSpecOptions != null)
            {
                var cardIds = new List<string>();
                foreach (var card in _runManager.CurrentSpecOptions)
                    cardIds.Add(card?.Id ?? "");
                snap.PendingSpecCardIds = cardIds.ToArray();
            }

            // Serialize spec bonuses AND their stack counts. Saving stacks is required
            // so stack-limited cards (and per-cap filtering) behave correctly on resume;
            // otherwise SpecStacks rebuilds as 1 each and capped cards could reappear.
            var types = new List<int>();
            var values = new List<float>();
            var stacks = new List<int>();
            if (run.SpecBonuses != null)
                foreach (var kvp in run.SpecBonuses)
                {
                    types.Add((int)kvp.Key);
                    values.Add(kvp.Value);
                    int stackCount = 1;
                    if (run.SpecStacks != null && run.SpecStacks.TryGetValue(kvp.Key, out int s))
                        stackCount = s;
                    stacks.Add(stackCount);
                }
            snap.SpecBonusTypes = types.ToArray();
            snap.SpecBonuses = values.ToArray();
            snap.SpecStacks = stacks.ToArray();

            // Serialize placed turrets
            var turretSnaps = new List<PlacedTurretSnapshot>();
            if (_turretManager != null)
            {
                foreach (var tc in _turretManager.AllTurrets)
                {
                    if (tc == null || tc.Data == null) continue;
                    turretSnaps.Add(new PlacedTurretSnapshot
                    {
                        TurretDataId = tc.Data.Id,
                        GridX = tc.GridPosition.x,
                        GridY = tc.GridPosition.y,
                        Level = tc.Level,
                        IsEvolved = tc.IsEvolved,
                        EvolutionPath = tc.IsEvolved ? tc.EvolutionPath : -1,
                        Gold = tc.TotalGoldInvested,
                        TargetingMode = (int)tc.CurrentTargetingMode
                    });
                }
            }
            snap.PlacedTurrets = turretSnaps.ToArray();

            // FIX 3: Save dynamic tile specialties exactly as placed
            var tileSnaps = new List<SavedTileSpecialty>();
            if (_gridSystem != null)
            {
                foreach (var pos in _gridSystem.AllPositions)
                {
                    var cell = _gridSystem.GetCell(pos);
                    if (cell == null || cell.Specialty == TileSpecialty.None) continue;

                    tileSnaps.Add(new SavedTileSpecialty
                    {
                        GridX = pos.x,
                        GridY = pos.y,
                        Specialty = cell.Specialty,
                        DynamicTileType = cell.DynamicTile != null
                ? (int)cell.DynamicTile.TileType  // FIX: save enum int, not string
                : 0
                    });
                }
            }
            snap.TileSpecialties = tileSnaps.ToArray();

            var save = SaveSystem.Load();
            save.HasSavedRun = true;
            save.SavedRun = snap;
            SaveSystem.Save(save);
        }

        private bool CanCreateStableSnapshot()
        {
            if (_runManager == null || _runManager.RunData == null)
                return false;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.GameOver)
                return false;

            if (_waveManager != null && _waveManager.IsSpawning)
                return false;

            if (ServiceLocator.TryGet<EnemyManager>(out var enemyManager)
                && enemyManager != null
                && enemyManager.ActiveCount > 0)
            {
                // We intentionally do not save mid-wave because enemies/projectiles/spawn progress
                // are not serialized. The previous clean checkpoint remains valid.
                return false;
            }

            return true;
        }

        // =================================================================
        // CLEAR
        // =================================================================

        public static void ClearSnapshot()
        {
            var save = SaveSystem.Load();
            save.HasSavedRun = false;
            save.SavedRun = null;
            SaveSystem.Save(save);
        }

        // =================================================================
        // RESTORE
        // =================================================================

        public void RestoreFromSnapshot(RunSnapshot snap)
        {
            if (snap == null) return;

            var run = _runManager?.RunData;
            if (run == null) return;

            // Restore run data
            run.Gold = snap.Gold;
            run.Lives = snap.Lives;
            run.MaxLives = snap.MaxLives;
            run.Level = snap.Level;
            run.CurrentXP = snap.XP;
            run.XPToNextLevel = snap.XPToNextLevel;
            run.Score = snap.Score;
            run.TotalTime = snap.TotalTime;    // FIX 4: restore time
            run.CurrentWave = snap.Wave;       // trait wave-scaling (Infinite Scaling) needs the wave before turrets recalc
            run.TotalGoldSpent = snap.TotalGoldSpent;  // keeps "damage per gold spent" trait correct on resume
            run.ActiveTraitIds = snap.ActiveTraitIds != null
                ? new List<string>(snap.ActiveTraitIds)
                : new List<string>();
            run.ActivePermanentBonuses = snap.ActivePermanentBonuses != null
                ? (float[])snap.ActivePermanentBonuses.Clone()
                : new float[10];

            if (ServiceLocator.TryGet<ETD.Traits.TraitManager>(out var traitManager))
                traitManager.Initialize(_database, run.ActiveTraitIds);

            // Restore run-frozen re-roll tokens. Do not overwrite the global shop save;
            // purchases made after this snapshot should remain for future fresh runs,
            // but must not affect this continued run.
            run.ActiveRerollTokens = snap.RerollTokens;

            // Restore spec bonuses AND stack counts directly. We must NOT use AddSpecBonus
            // here: it would set every stack count to 1, so a card taken to its MaxStacks
            // (or a crit card at cap) could be offered again after resume. Old saves have
            // no SpecStacks — fall back to 1 (previous behavior).
            run.SpecBonuses?.Clear();
            run.SpecStacks?.Clear();
            if (snap.SpecBonusTypes != null && snap.SpecBonuses != null)
                for (int i = 0; i < snap.SpecBonusTypes.Length && i < snap.SpecBonuses.Length; i++)
                {
                    var type = (SpecCardEffectType)snap.SpecBonusTypes[i];
                    run.SpecBonuses[type] = snap.SpecBonuses[i];
                    int stackCount = (snap.SpecStacks != null && i < snap.SpecStacks.Length)
                        ? snap.SpecStacks[i]
                        : 1;
                    run.SpecStacks[type] = Mathf.Max(1, stackCount);
                }

            // FIX 3: Restore tile specialties EXACTLY â skip ApplyRandomSpecialties
            RestoreTileSpecialties(snap.TileSpecialties);

            // Restore turrets. PlaceAndRestoreTurret does NOT raise TurretPlacedEvent,
            // so it never increments run.TurretsPlaced — that is why resumed runs
            // showed "damage per owned turret" as 0%. Restore the counter explicitly.
            int restoredTurretCount = 0;
            if (snap.PlacedTurrets != null && _database != null && _turretManager != null)
            {
                foreach (var ts in snap.PlacedTurrets)
                {
                    var data = _database.GetTurret(ts.TurretDataId);
                    if (data == null) continue;
                    var pos = new Vector2Int(ts.GridX, ts.GridY);
                    if (_turretManager.PlaceAndRestoreTurret(data, pos, ts) != null)
                        restoredTurretCount++;
                }
            }

            // Prefer the saved cumulative count; fall back to the number of turrets
            // actually restored (covers snapshots saved before this field existed).
            run.TurretsPlaced = Mathf.Max(snap.TurretsPlaced, restoredTurretCount);

            // Set wave counter (SetWave does wave-1 so StartNextWave lands on correct wave)
            _waveManager?.SetWave(snap.Wave);
            _waveManager?.StartPrepPhase();

            // Everything (traits, spec bonuses, permanent bonuses, wave, TurretsPlaced,
            // TotalGoldSpent) is now restored. Force a full turret recalc + aura refresh so
            // wave-scaling and per-owned-turret traits are correct immediately, instead of
            // appearing "off" until the next upgrade/wave.
            _turretManager?.RecalculateAllTurretsAndRefreshAuras();

            // FIX 2: If level-up was pending, restore and re-show spec cards
            if (snap.IsLevelUpPending && snap.PendingSpecCardIds != null
                && snap.PendingSpecCardIds.Length > 0)
            {
                var cards = new List<SpecCardData>();
                foreach (var id in snap.PendingSpecCardIds)
                {
                    var card = _database?.GetSpecCard(id);
                    if (card != null) cards.Add(card);
                }

                if (cards.Count > 0)
                {
                    _runManager.SetSpecCardOptions(cards.ToArray());
                    // Delay one frame so all systems are initialized
                    StartCoroutine(DelayedLevelUpEvent(run.Level));
                }
            }
        }

        private void RestoreTileSpecialties(SavedTileSpecialty[] tiles)
        {

            if (tiles == null || _gridSystem == null || _database == null) return;

            foreach (var ts in tiles)
            {
                var pos = new Vector2Int(ts.GridX, ts.GridY);
                var cell = _gridSystem.GetCell(pos);
                if (cell == null) continue;

                cell.Specialty = ts.Specialty;

                // FIX: use DynamicTileType enum overload
                var tileData = _database.GetDynamicTile((DynamicTileType)ts.DynamicTileType);
                if (tileData != null)
                {
                    cell.DynamicTile = tileData;
                    if (tileData.TileMaterial != null && cell.TileRenderer != null)
                        cell.TileRenderer.material = tileData.TileMaterial;
                    EventBus.Publish(new TileSpecialtyAppliedEvent
                    {
                        GridPos = pos,
                        Specialty = (int)ts.Specialty
                    });
                }
            }
        }


        private System.Collections.IEnumerator DelayedLevelUpEvent(int level)
        {
            yield return null; // wait one frame
            GameManager.Instance.SetState(GameState.LevelUp);
            EventBus.Publish(new LevelUpEvent { NewLevel = level });
        }

        // =================================================================
        // EVENTS
        // =================================================================

        // Save on wave complete â uses CurrentWave+1 so resume starts next wave
        private void OnWaveCompleted(WaveCompletedEvent evt) => SaveSnapshot();

        // Clear on clean game over â no resume
        private void OnGameOver(GameOverEvent evt) => ClearSnapshot();

        private void OnRetry(RetryEvent evt)=> ClearSnapshot();

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            // Pausing mid-wave should not overwrite the previous clean checkpoint.
            // If the player pauses during preparation/level-up with no active enemies, this is safe.
            if ((GameState)evt.NewState == GameState.Paused)
                SaveSnapshot();
        }

        private void OnApplicationPause(bool paused) { if (paused) SaveSnapshot(); }
        private void OnApplicationQuit() => SaveSnapshot();

        private void OnDestroy()
        {
            EventBus.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<RetryEvent>(OnRetry);
        }
    }
}