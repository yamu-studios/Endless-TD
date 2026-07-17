// ============================================================================
// ETD.Core - SteamAchievementManager.cs
// Tracks and unlocks Steam achievements.
//
// Important fixes in this version:
// - Uses the same Steam compile guard as the working leaderboard code.
// - Auto-creates itself before the first scene loads.
// - Does NOT use SteamUserStats.RequestCurrentStats.
// - Queues achievement unlocks until SteamManager is initialized.
// - Logs missing/wrong Steam achievement API names.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace ETD.Core
{
    public class SteamAchievementManager : MonoBehaviour
    {
        public static SteamAchievementManager Instance { get; private set; }

#if !DISABLESTEAMWORKS
        private Callback<UserStatsStored_t> _userStatsStored;
        private readonly HashSet<string> _pendingUnlocks = new HashSet<string>();
        private bool _steamReady;
        private float _nextSteamReadyCheckTime;
        private float _steamInitializedAt = -1f;
#endif

        private int _totalTurretsBuilt;
        private int _totalUpgrades;
        private float _totalBurnDamage;
        private float _totalSlowSeconds;
        private int _totalChainHits;
        private int _totalBossesKilled;
        private float _totalMeta;
        private int _totalShopBuys;
        private int _totalChallenges;

        private int _runTurretsBuilt;
        // Strategic Mind: bitmask of distinct turret types placed this run.
        // 7 types (Basic..Radar = 0..6) -> full mask 0x7F. Deliberately NOT extended
        // to cover the v1.0 Phase 3 turret types (Void/Toxin/Railgun, indices 7-9) —
        // this achievement already shipped as "place all 7", so widening it would
        // silently change an already-live achievement's contract. See
        // _runNewTurretTypesMask below for the Phase 3 equivalent.
        private int _runTurretTypesMask;
        private const int AllTurretTypesMask = (1 << 7) - 1;
        // Cutting Edge (v1.0 Phase 3): place all 3 new turret types (Void=7, Toxin=8,
        // Railgun=9) in a single run. Separate mask, bit 0 = Void .. bit 2 = Railgun.
        private int _runNewTurretTypesMask;
        private const int AllNewTurretTypesMask = (1 << 3) - 1;
        private int _runUpgrades;
        private bool _hasBurnThisRun;
        private bool _hasFrostThisRun;
        private bool _hasLightningThisRun;
        private bool _hasBasicThisRun;
        private bool _hasLaserThisRun;
        private bool _hasVoidThisRun;
        private bool _hasToxinThisRun;
        private int _totalWeakenApplied;
        private int _totalPoisonApplied;
        private int _totalExposeApplied;
        private bool _hasRailgunThisRun;
        private int _peakLivesLost;
        private bool _firstSpecCard;
        private bool _firstEvolution;
        private float _runLaserSeconds;
        private int _runDynamicTiles;
        private int _runGreedTiles;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
                return;

            SteamAchievementManager existing = FindFirstObjectByType<SteamAchievementManager>();
            if (existing != null)
                return;

            GameObject go = new GameObject("[SteamAchievementManager]");
            go.AddComponent<SteamAchievementManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

#if !DISABLESTEAMWORKS
            _userStatsStored = Callback<UserStatsStored_t>.Create(OnUserStatsStored);
#endif
        }

        private void Start()
        {
            LoadCumulativeCounters();
            Subscribe();
        }

        private void Update()
        {
#if !DISABLESTEAMWORKS
            EnsureSteamReady();
#endif
        }

        public void Unlock(string apiName)
        {
            if (string.IsNullOrWhiteSpace(apiName))
                return;

#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized)
            {
                QueueUnlock(apiName);
                return;
            }

            if (!_steamReady)
            {
                QueueUnlock(apiName);
                EnsureSteamReady();
                return;
            }

            UnlockNow(apiName);
#else
            Debug.Log($"[Achievement] Steamworks disabled. Would unlock: {apiName}");
#endif
        }

#if !DISABLESTEAMWORKS
        private void QueueUnlock(string apiName)
        {
            if (_pendingUnlocks.Add(apiName))
                Debug.Log($"[Achievement] Queued until Steam is ready: {apiName}");
        }

        private void EnsureSteamReady()
        {
            if (_steamReady || Time.unscaledTime < _nextSteamReadyCheckTime)
                return;

            _nextSteamReadyCheckTime = Time.unscaledTime + 0.5f;

            if (!SteamAPI.IsSteamRunning())
                return;

            if (!SteamManager.Initialized)
                return;

            if (_steamInitializedAt < 0f)
                _steamInitializedAt = Time.unscaledTime;

            if (Time.unscaledTime - _steamInitializedAt < 0.5f)
                return;

            _steamReady = true;
            Debug.Log("[Achievement] Steam initialized. Achievement unlocks are ready.");

            FlushPendingUnlocks();
        }

        private void OnUserStatsStored(UserStatsStored_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
                Debug.LogWarning($"[Achievement] StoreStats failed: {callback.m_eResult}");
        }

        private void FlushPendingUnlocks()
        {
            if (_pendingUnlocks.Count == 0)
                return;

            string[] unlocks = new string[_pendingUnlocks.Count];
            _pendingUnlocks.CopyTo(unlocks);
            _pendingUnlocks.Clear();

            for (int i = 0; i < unlocks.Length; i++)
                UnlockNow(unlocks[i]);
        }

        private void UnlockNow(string apiName)
        {
            bool getOk = SteamUserStats.GetAchievement(apiName, out bool achieved);
            if (!getOk)
            {
                Debug.LogWarning(
                    "[Achievement] GetAchievement failed for '" + apiName + "'. " +
                    "Check that this exact API Name exists in Steamworks App Admin > Achievements."
                );
                return;
            }

            if (achieved)
                return;

            bool setOk = SteamUserStats.SetAchievement(apiName);
            if (!setOk)
            {
                Debug.LogWarning(
                    "[Achievement] SetAchievement failed for '" + apiName + "'. " +
                    "Usually this means the API Name is wrong or Steam is not ready."
                );
                return;
            }

            SteamUserStats.StoreStats();
            Debug.Log($"[Achievement] Unlocked: {apiName}");
        }
#endif

        /// <summary>
        /// Editor/dev testing only: fires the normal unlock path for any API name so
        /// ID mismatches with the Steamworks panel surface as console warnings
        /// ("GetAchievement failed for ..."). Used by ETDDevToolsWindow.
        /// </summary>
        public void DebugUnlock(string apiName)
        {
            if (string.IsNullOrWhiteSpace(apiName)) return;
            Debug.Log($"[Achievement] DebugUnlock requested: {apiName}");
            Unlock(apiName);
        }

        private void Subscribe()
        {
            EventBus.Subscribe<GameOverEvent>(OnGameOver);
            EventBus.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
            EventBus.Subscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Subscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Subscribe<TurretEvolvedEvent>(OnTurretEvolved);
            EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Subscribe<EnemyStatusAppliedEvent>(OnStatusApplied);
            EventBus.Subscribe<ScoreChangedEvent>(OnScoreChanged);
            EventBus.Subscribe<EnemyReachedEndEvent>(OnEnemyReachedEnd);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<SpecCardChosenEvent>(OnSpecCardChosen);
            EventBus.Subscribe<LaserHitEvent>(OnLaserHit);
            EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<TileSpecialtyAppliedEvent>(OnTilePlaced);
            EventBus.Subscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Subscribe<MetaCurrencyChangedEvent>(OnMetaChanged);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ShopItemPurchasedEvent>(OnShopPurchase);
            // FIX: chain lightning is DAMAGE, not a status — EnemyStatusAppliedEvent
            // never fires with ChainLightning, so _hasLightningThisRun / chain-hit
            // counters could never progress. Count both the batched and single events.
            EventBus.Subscribe<ChainLightningHitBatchEvent>(OnChainHitBatch);
            EventBus.Subscribe<ChainLightningHitEvent>(OnChainHitSingle);
        }

        private void OnChainHitBatch(ChainLightningHitBatchEvent evt) => CountChainHits(evt.Count);
        private void OnChainHitSingle(ChainLightningHitEvent evt) => CountChainHits(Mathf.Max(1, evt.Count));

        private void CountChainHits(int count)
        {
            if (count <= 0) return;

            _hasLightningThisRun = true;
            _totalChainHits += count;
            SaveCounter("ach_chain_hits", _totalChainHits);

            if (_totalChainHits >= 1500)
                Unlock("ACH_LIGHTNING_NETWORK");

            CheckAllEffectsAchievement();
        }

        // ACH_ALL_EFFECTS ("Elemental Overlord"): originally required Burn+Frost+Lightning
        // specifically. Widened alongside the elements->turret-types taxonomy change to
        // "any 3 distinct damage-dealing turret-type families in one run" (same threshold,
        // wider pool: Basic/Frost/Inferno/Laser/Lightning). Add a flag+case here for every
        // future damage turret's signature status.
        private void CheckAllEffectsAchievement()
        {
            int count = (_hasBurnThisRun ? 1 : 0) + (_hasFrostThisRun ? 1 : 0)
                + (_hasLightningThisRun ? 1 : 0) + (_hasBasicThisRun ? 1 : 0)
                + (_hasLaserThisRun ? 1 : 0) + (_hasVoidThisRun ? 1 : 0) + (_hasToxinThisRun ? 1 : 0)
                + (_hasRailgunThisRun ? 1 : 0);

            if (count >= 3)
                Unlock("ACH_ALL_EFFECTS");
        }

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            var state = (GameState)evt.NewState;

            if (state == GameState.WaveActive)
            {
                var save = SaveSystem.Load();

                // NOTE: ACH_FIRST_TRAIT is "Strategic Mind" on Steam and is unlocked by
                // placing all 7 turret types in one run (see OnTurretPlaced) — it is
                // deliberately NOT unlocked by trait selection anymore.
                if (save.SelectedTraitIds != null && save.SelectedTraitIds.Length > 0)
                {
                    int slots = save.TraitSlotCount;
                    if (save.SelectedTraitIds.Length >= slots)
                        Unlock("ACH_FULL_TRAIT_LOADOUT");
                }
            }
        }

        private void OnWaveCompleted(WaveCompletedEvent evt)
        {
            int w = evt.WaveNumber;

            if (w >= 10) Unlock("ACH_FIRST_DEFENSE");
            if (w >= 25) Unlock("ACH_WAVE_25");
            if (w >= 50) Unlock("ACH_WAVE_50");
            if (w >= 100) Unlock("ACH_WAVE_100");
            if (w >= 150) Unlock("ACH_WAVE_150");

            if (_peakLivesLost == 0)
            {
                if (w >= 30) Unlock("ACH_NO_LEAK_WAVE_30");
                if (w >= 60) Unlock("ACH_NO_LEAK_WAVE_60");
            }

            if (_runLaserSeconds >= 15f)
                Unlock("ACH_LASER_FOCUS");
        }

        private void OnTurretPlaced(TurretPlacedEvent evt)
        {
            _runTurretsBuilt++;
            _totalTurretsBuilt++;
            SaveCounter("ach_total_turrets", _totalTurretsBuilt);

            Unlock("ACH_FIRST_TURRET");

            if (_runTurretsBuilt >= 25)
                Unlock("ACH_25_TURRETS");

            if (_totalTurretsBuilt >= 100)
                Unlock("ACH_100_TURRETS_TOTAL");

            // Strategic Mind (Steam API name: ACH_FIRST_TRAIT — confirmed by user):
            // place every turret type (all 7) in a single run. The mask is persisted
            // so save/resume keeps progress; it resets with the other run counters.
            // TurretType < 0 means the publisher couldn't resolve a type (debug events).
            if (evt.TurretType >= 0 && evt.TurretType < 7)
            {
                _runTurretTypesMask |= 1 << evt.TurretType;
                SaveCounter("ach_run_turret_types", _runTurretTypesMask);
                if (_runTurretTypesMask == AllTurretTypesMask)
                    Unlock("ACH_FIRST_TRAIT");
            }

            // Cutting Edge: place all 3 v1.0 Phase 3 turret types (Void/Toxin/Railgun,
            // indices 7-9) in a single run.
            if (evt.TurretType >= 7 && evt.TurretType < 10)
            {
                _runNewTurretTypesMask |= 1 << (evt.TurretType - 7);
                SaveCounter("ach_run_new_turret_types", _runNewTurretTypesMask);
                if (_runNewTurretTypesMask == AllNewTurretTypesMask)
                    Unlock("ACH_CUTTING_EDGE");
            }
        }

        private void OnTurretUpgraded(TurretUpgradedEvent evt)
        {
            _runUpgrades++;
            _totalUpgrades++;
            SaveCounter("ach_total_upgrades", _totalUpgrades);

            Unlock("ACH_FIRST_UPGRADE");

            if (_totalUpgrades >= 50)
                Unlock("ACH_50_UPGRADES");
        }

        private void OnTurretEvolved(TurretEvolvedEvent evt)
        {
            if (_firstEvolution)
                return;

            _firstEvolution = true;
            Unlock("ACH_FIRST_EVOLUTION");
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            if (evt.EnemyTier == 2)
            {
                _totalBossesKilled++;
                SaveCounter("ach_total_bosses", _totalBossesKilled);

                Unlock("ACH_BOSS_KILLER");

                if (_totalBossesKilled >= 10)
                    Unlock("ACH_10_BOSSES");
            }
        }

        private void OnStatusApplied(EnemyStatusAppliedEvent evt)
        {
            var type = (StatusEffectType)evt.StatusType;

            switch (type)
            {
                case StatusEffectType.Burn:
                    _hasBurnThisRun = true;
                    _totalBurnDamage += 500f;
                    SaveCounterF("ach_burn_damage", _totalBurnDamage);

                    if (_totalBurnDamage >= 75000f)
                        Unlock("ACH_BURN_MASTER");
                    break;

                case StatusEffectType.Slow:
                case StatusEffectType.Freeze:
                    _hasFrostThisRun = true;
                    _totalSlowSeconds += 1f;
                    SaveCounterF("ach_slow_seconds", _totalSlowSeconds);

                    if (_totalSlowSeconds >= 600f)
                        Unlock("ACH_FROST_CONTROL");
                    break;

                case StatusEffectType.ChainLightning:
                    _hasLightningThisRun = true;
                    _totalChainHits++;
                    SaveCounter("ach_chain_hits", _totalChainHits);

                    if (_totalChainHits >= 1500)
                        Unlock("ACH_LIGHTNING_NETWORK");
                    break;

                case StatusEffectType.ArmorBreak:
                    _hasBasicThisRun = true;
                    break;

                case StatusEffectType.HPPercentReduce:
                    _hasLaserThisRun = true;
                    break;

                case StatusEffectType.Weaken:
                    _hasVoidThisRun = true;
                    _totalWeakenApplied++;
                    SaveCounter("ach_weaken_applied", _totalWeakenApplied);
                    if (_totalWeakenApplied >= 1000)
                        Unlock("ACH_VOID_WALKER");
                    break;

                case StatusEffectType.Poison:
                    _hasToxinThisRun = true;
                    _totalPoisonApplied++;
                    SaveCounter("ach_poison_applied", _totalPoisonApplied);
                    if (_totalPoisonApplied >= 2000)
                        Unlock("ACH_TOXIC_TOUCH");
                    break;

                case StatusEffectType.Expose:
                    _hasRailgunThisRun = true;
                    _totalExposeApplied++;
                    SaveCounter("ach_expose_applied", _totalExposeApplied);
                    if (_totalExposeApplied >= 500)
                        Unlock("ACH_MARKED_FOR_DEATH");
                    break;
            }

            CheckAllEffectsAchievement();
        }

        private void OnScoreChanged(ScoreChangedEvent evt)
        {
            if (evt.Current >= 100000) Unlock("ACH_SCORE_100K");
            if (evt.Current >= 500000) Unlock("ACH_SCORE_500K");
            if (evt.Current >= 1000000) Unlock("ACH_SCORE_1M");
        }

        private void OnGameOver(GameOverEvent evt)
        {
            if (evt.Score >= 100000) Unlock("ACH_SCORE_100K");
            if (evt.Score >= 500000) Unlock("ACH_SCORE_500K");
            if (evt.Score >= 1000000) Unlock("ACH_SCORE_1M");

            if (_runLaserSeconds >= 15f)
                Unlock("ACH_LASER_FOCUS");

            ResetRunCounters();
        }

        private void OnEnemyReachedEnd(EnemyReachedEndEvent evt)
        {
            _peakLivesLost++;
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            // Reserved for future level-based achievements.
        }

        private void OnSpecCardChosen(SpecCardChosenEvent evt)
        {
            if (_firstSpecCard)
                return;

            _firstSpecCard = true;
            Unlock("ACH_FIRST_SPEC_CARD");
        }

        private void OnLaserHit(LaserHitEvent evt)
        {
            _runLaserSeconds += evt.DeltaTime;

            if (_runLaserSeconds >= 15f)
                Unlock("ACH_LASER_FOCUS");
        }

        private void OnTilePlaced(TileSpecialtyAppliedEvent evt)
        {
            _runDynamicTiles++;

            Unlock("ACH_DYNAMIC_TILE_FIRST");

            if (_runDynamicTiles >= 5)
                Unlock("ACH_DYNAMIC_TILE_5_RUN");

            if ((TileSpecialty)evt.Specialty == TileSpecialty.Greed)
            {
                _runGreedTiles++;

                if (_runGreedTiles >= 2)
                    Unlock("ACH_GREED_RUN");
            }
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            if (evt.Current >= 30000)
                Unlock("ACH_ECONOMIST");
        }

        private void OnMetaChanged(MetaCurrencyChangedEvent evt)
        {
            _totalMeta += Mathf.Max(evt.Delta, 0);
            SaveCounterF("ach_total_meta", _totalMeta);

            if (_totalMeta >= 1000f)
                Unlock("ACH_CRYSTAL_HARVEST");
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            _totalChallenges++;
            SaveCounter("ach_total_challenges", _totalChallenges);

            Unlock("ACH_CHALLENGE_FIRST");

            if (_totalChallenges >= 10)
                Unlock("ACH_CHALLENGE_10");
        }

        private void OnShopPurchase(ShopItemPurchasedEvent evt)
        {
            _totalShopBuys++;
            SaveCounter("ach_total_shop_buys", _totalShopBuys);

            Unlock("ACH_SHOP_BUY_FIRST");

            if (_totalShopBuys >= 10)
                Unlock("ACH_SHOP_10_UPGRADES");
        }

        private void ResetRunCounters()
        {
            _runTurretsBuilt = 0;
            _runTurretTypesMask = 0;
            SaveCounter("ach_run_turret_types", 0);
            _runNewTurretTypesMask = 0;
            SaveCounter("ach_run_new_turret_types", 0);
            _runUpgrades = 0;
            _hasBurnThisRun = false;
            _hasFrostThisRun = false;
            _hasLightningThisRun = false;
            _hasBasicThisRun = false;
            _hasLaserThisRun = false;
            _hasVoidThisRun = false;
            _hasToxinThisRun = false;
            _hasRailgunThisRun = false;
            _peakLivesLost = 0;
            _firstSpecCard = false;
            _runLaserSeconds = 0f;
            _runDynamicTiles = 0;
            _runGreedTiles = 0;
        }

        private void LoadCumulativeCounters()
        {
            _totalTurretsBuilt = LoadCounter("ach_total_turrets");
            _totalUpgrades = LoadCounter("ach_total_upgrades");
            _totalBurnDamage = LoadCounterF("ach_burn_damage");
            _totalSlowSeconds = LoadCounterF("ach_slow_seconds");
            _totalChainHits = LoadCounter("ach_chain_hits");
            _totalBossesKilled = LoadCounter("ach_total_bosses");
            _totalMeta = LoadCounterF("ach_total_meta");
            _totalShopBuys = LoadCounter("ach_total_shop_buys");
            _totalChallenges = LoadCounter("ach_total_challenges");
            _totalWeakenApplied = LoadCounter("ach_weaken_applied");
            _totalPoisonApplied = LoadCounter("ach_poison_applied");
            _totalExposeApplied = LoadCounter("ach_expose_applied");
            // Run-scoped Strategic Mind progress survives quit/resume: snapshot restore
            // re-places turrets without placement events, so this is the only record.
            _runTurretTypesMask = LoadCounter("ach_run_turret_types");
            _runNewTurretTypesMask = LoadCounter("ach_run_new_turret_types");
        }

        private static int LoadCounter(string key)
        {
            return PlayerPrefs.GetInt(key, 0);
        }

        private static float LoadCounterF(string key)
        {
            return PlayerPrefs.GetFloat(key, 0f);
        }

        private static void SaveCounter(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }

        private static void SaveCounterF(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            EventBus.Unsubscribe<ChainLightningHitBatchEvent>(OnChainHitBatch);
            EventBus.Unsubscribe<ChainLightningHitEvent>(OnChainHitSingle);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
            EventBus.Unsubscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Unsubscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Unsubscribe<TurretEvolvedEvent>(OnTurretEvolved);
            EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Unsubscribe<EnemyStatusAppliedEvent>(OnStatusApplied);
            EventBus.Unsubscribe<ScoreChangedEvent>(OnScoreChanged);
            EventBus.Unsubscribe<EnemyReachedEndEvent>(OnEnemyReachedEnd);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<SpecCardChosenEvent>(OnSpecCardChosen);
            EventBus.Unsubscribe<LaserHitEvent>(OnLaserHit);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<TileSpecialtyAppliedEvent>(OnTilePlaced);
            EventBus.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Unsubscribe<MetaCurrencyChangedEvent>(OnMetaChanged);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ShopItemPurchasedEvent>(OnShopPurchase);
        }
    }
}