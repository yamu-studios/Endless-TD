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
        private int _runUpgrades;
        private bool _hasBurnThisRun;
        private bool _hasFrostThisRun;
        private bool _hasLightningThisRun;
        private int _peakLivesLost;
        private bool _firstTraitUsed;
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
        }

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            var state = (GameState)evt.NewState;

            if (state == GameState.WaveActive)
            {
                var save = SaveSystem.Load();

                if (save.SelectedTraitIds != null && save.SelectedTraitIds.Length > 0)
                {
                    if (!_firstTraitUsed)
                    {
                        Unlock("ACH_FIRST_TRAIT");
                        _firstTraitUsed = true;
                    }

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
            }

            if (_hasBurnThisRun && _hasFrostThisRun && _hasLightningThisRun)
                Unlock("ACH_ALL_EFFECTS");
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
            _runUpgrades = 0;
            _hasBurnThisRun = false;
            _hasFrostThisRun = false;
            _hasLightningThisRun = false;
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