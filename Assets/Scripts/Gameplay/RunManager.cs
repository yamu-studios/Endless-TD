// ============================================================================
// ETD.Gameplay - RunManager.cs  [UPDATED]
// Orchestrates a single run: XP, leveling, spec cards, gold, lives, score,
// relic drops, RunStatModifiers init. Matches ETD_FULL_SHEET data.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;
using ETD.Grid;
using ETD.Pathfinding;
using ETD.Enemies;
using ETD.Turrets;
using ETD.Waves;
using ETD.Meta;


namespace ETD.Gameplay
{
    public class RunManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridSystem _gridSystem;
        [SerializeField] private AStarPathfinder _pathfinder;
        [SerializeField] private EnemyManager _enemyManager;
        [SerializeField] private TurretManager _turretManager;
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private GameDatabase _database;

        private RunData _runData;
        public RunData RunData => _runData;

        private RunStatModifiers _statModifiers;
        public RunStatModifiers StatModifiers => _statModifiers;

        // Spec card selection
        [Header("Spec Card Pity")]
        [SerializeField] private SpecCardPitySettings _specCardPitySettings;

        private SpecCardPityState _specCardPityState;
        private SpecCardOfferGenerator _specCardOfferGenerator;

        private SpecCardData[] _currentSpecOptions;
        public SpecCardData[] CurrentSpecOptions => _currentSpecOptions;

        private bool _gameOverTriggered;

        private void Start()
        {
            _specCardPityState = new SpecCardPityState();

            if (_specCardPitySettings != null)
            {
                _specCardOfferGenerator = new SpecCardOfferGenerator(
                    _specCardPitySettings,
                    _specCardPityState
                );
            }
            else
            {
                Debug.LogError("[SpecCards] Missing SpecCardPitySettings.");
            }
            InitializeRun();
        }

        private void InitializeRun()
        {
            var save = SaveSystem.Load();
            ResetSpecCardPityForNewRun();
            _runData = new RunData
            {
                Lives = GameConstants.STARTING_LIVES,
                MaxLives = GameConstants.STARTING_LIVES,
                Gold = GameConstants.STARTING_GOLD,
                Level = 1,
                CurrentXP = 0,
                XPToNextLevel = GameConstants.BASE_XP_REQUIRED
            };

            // Freeze the run's meta/shop bonuses at run start.
            // A resumed run must not receive bonuses bought after the snapshot was created.
            SetActivePermanentBonusesFromSave(save);
            _runData.ActiveRerollTokens = save.RerollTokens;
            ApplyPermanentBonuses();

            // Load persisted planning selections for a fresh run only.
            // Resume will overwrite this from the saved run snapshot.
            if (save.SelectedTraitIds != null && save.SelectedTraitIds.Length > 0)
            {
                _runData.ActiveTraitIds = new List<string>(save.SelectedTraitIds);
            }
           


            ServiceLocator.Register(this);

            // Initialize subsystems
            _gridSystem.Initialize();
            //_gridSystem.ApplyRandomSpecialties(new System.Random(), _database);
            _pathfinder.Initialize(_gridSystem);
            _enemyManager.Initialize(_gridSystem, _pathfinder);
            _turretManager.Initialize(_gridSystem, _pathfinder);
            _waveManager.Initialize(_database, _enemyManager);

           
            if (!save.HasSavedRun)  // Only apply random specialties on fresh runs
                _gridSystem.ApplyRandomSpecialties(new System.Random(), _database);


            // Initialize trait manager with selected traits
            var traitManager = gameObject.AddComponent<Traits.TraitManager>();
            traitManager.Initialize(_database, _runData.ActiveTraitIds);

            // Initialize stat modifiers (centralized trait/spec/wave scaling)
            _statModifiers = gameObject.AddComponent<RunStatModifiers>();
            _statModifiers.Initialize(this, _database);

            // Initialize end-game damage stats collector. Registered as IDamageStatsSink
            // so enemies can report effective damage without using EventBus per hit.
            // Prefer an existing scene tracker if one was placed in the scene; do not
            // blindly AddComponent because that creates duplicate ServiceLocator entries.
            var damageStats = GetComponent<RunDamageStatsTracker>();
            if (damageStats == null)
                ServiceLocator.TryGet(out damageStats);
            if (damageStats == null)
                damageStats = FindFirstObjectByType<RunDamageStatsTracker>();
            if (damageStats == null)
                damageStats = gameObject.AddComponent<RunDamageStatsTracker>();
            damageStats.Initialize();

            // Initialize challenge tracker if present in scene
            var challengeTracker = GetComponent<ChallengeTracker>();
            if (challengeTracker == null)
                challengeTracker = FindFirstObjectByType<ChallengeTracker>();
            if (challengeTracker != null)
                challengeTracker.Initialize();

            // Subscribe to events
            EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Subscribe<EnemyReachedEndEvent>(OnEnemyReachedEnd);
            EventBus.Subscribe<SpecCardChosenEvent>(OnSpecCardChosen);
            EventBus.Subscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Subscribe<TurretSoldEvent>(OnTurretSold);
            EventBus.Subscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Subscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Subscribe<PrepPhaseSkipedEvent>(OnPrepSkipped);

            // Start first prep phase
            EventBus.Publish(new GameStartedEvent());
            //_waveManager.StartPrepPhase();
            HubBadgeRegistry.ClearLastRunData();

            //bool shouldResume = PlayerPrefs.GetInt("ResumeRun", 0) == 1;
            //PlayerPrefs.DeleteKey("ResumeRun");

            //if (shouldResume)
            //{
            //    var save2 = SaveSystem.Load();
            //    if (save2.HasSavedRun && save2.SavedRun != null)
            //    {
            //        var snapMgr = GetComponent<RunSnapshotManager>()
            //                   ?? FindObjectOfType<RunSnapshotManager>();
            //        snapMgr?.RestoreFromSnapshot(save2.SavedRun);
            //    }
            //}

            //// ONE call to StartPrepPhase, always at the end
            //_waveManager.StartPrepPhase();

            bool shouldResume = PlayerPrefs.GetInt("ResumeRun", 0) == 1;
            PlayerPrefs.DeleteKey("ResumeRun");

            if (shouldResume)
            {
                var save2 = SaveSystem.Load();
                if (save2.HasSavedRun && save2.SavedRun != null)
                {
                    var snapMgr = GetComponent<RunSnapshotManager>();
                              
                    if (snapMgr != null)
                    {
                        snapMgr.RestoreFromSnapshot(save2.SavedRun);
                        return; // RestoreFromSnapshot calls SetWave; StartPrepPhase called next
                    }
                }
            }

            // Fresh run or no snapshot found: start at wave 0
            _waveManager.StartPrepPhase();
        }

        private void SetActivePermanentBonusesFromSave(SaveData save)
        {
            _runData.ActivePermanentBonuses = save.PermanentBonuses != null
                ? (float[])save.PermanentBonuses.Clone()
                : new float[10];
        }

        private float GetActivePermanentBonus(int index)
        {
            if (_runData?.ActivePermanentBonuses == null
                || index < 0
                || index >= _runData.ActivePermanentBonuses.Length)
                return 0f;

            return _runData.ActivePermanentBonuses[index];
        }

        private void ApplyPermanentBonuses()
        {
            // [0] MaxHP bonus
            float hpBonusValue = GetActivePermanentBonus(0);
            if (hpBonusValue > 0f)
            {
                int hpBonus = Mathf.RoundToInt(hpBonusValue);
                _runData.Lives += hpBonus;
                _runData.MaxLives += hpBonus;
            }

            // [4] Starting gold bonus
            float startingGoldBonus = GetActivePermanentBonus(4);
            if (startingGoldBonus > 0f)
            {
                _runData.Gold += Mathf.RoundToInt(startingGoldBonus);
            }
        }

        private void Update()
        {
            var state = GameManager.Instance != null
                ? GameManager.Instance.CurrentState
                : GameState.Preparation;

            // Count real elapsed player-run time. Do not multiply by game speed,
            // and do not count paused / modal / game-over time.
            if (state == GameState.Preparation || state == GameState.WaveActive)
                _runData.TotalTime += Time.unscaledDeltaTime;
        }

        // =================================================================
        // ECONOMY
        // =================================================================

        public bool SpendGold(int amount)
        {
            if (_runData.Gold < amount) return false;
            _runData.Gold -= amount;
            _runData.TotalGoldSpent += amount;
            EventBus.Publish(new GoldChangedEvent { Current = _runData.Gold, Delta = -amount });
            return true;
        }

        public void AddGold(int amount)
        {
            // Trait/spec card multiplier
            float mult = _statModifiers != null ? _statModifiers.GetGoldMultiplier() : 1f;

            // Permanent gold multiplier is frozen at run start / snapshot time.
            float permGoldMult = 1f + GetActivePermanentBonus(2);
            mult *= permGoldMult;

            int modified = Mathf.RoundToInt(amount * mult);
            _runData.Gold += modified;
            _runData.TotalGoldEarned += modified;
            EventBus.Publish(new GoldChangedEvent { Current = _runData.Gold, Delta = modified });
        }

        void OnPrepSkipped(PrepPhaseSkipedEvent evt)
        {
            AddGold(evt.Reward);
        }
        // =================================================================
        // XP & LEVELING
        // =================================================================

        public void AddXP(float amount)
        {
            // Permanent XP multiplier is frozen at run start / snapshot time.
            float xpMult = 1f + GetActivePermanentBonus(1);
            float modified = amount * xpMult;

            _runData.CurrentXP += modified;

            EventBus.Publish(new XPGainedEvent
            {
                Amount = modified,
                CurrentXP = _runData.CurrentXP,
                RequiredXP = _runData.XPToNextLevel,
                Level = _runData.Level
            });

            while (_runData.CurrentXP >= _runData.XPToNextLevel)
            {
                _runData.CurrentXP -= _runData.XPToNextLevel;
                _runData.Level++;
                _runData.XPToNextLevel = _runData.GetXPRequired(_runData.Level);

                PresentSpecCards();
                EventBus.Publish(new XPGainedEvent
                {
                    Amount = modified,
                    CurrentXP = _runData.CurrentXP,
                    RequiredXP = _runData.XPToNextLevel,
                    Level = _runData.Level
                });
                EventBus.Publish(new LevelUpEvent { NewLevel = _runData.Level });
            }
        }


        // =================================================================
        // SPEC CARDS
        // =================================================================
        private int GetCurrentWaveForPity()
        {
            return _waveManager != null ? _waveManager.CurrentWave : 0;
        }

        public void ResetSpecCardPityForNewRun()
        {
            _specCardPityState?.Reset();
        }
        //private void PresentSpecCards()
        //{
        //    GameManager.Instance.SetState(GameState.LevelUp);

        //    _currentSpecOptions = new SpecCardData[GameConstants.SPEC_CARDS_PER_LEVELUP];
        //    var allCards = _database.SpecCards;
        //    if (allCards == null || allCards.Length == 0) return;

        //    // Filter to unlocked cards only
        //    var available = new List<SpecCardData>();
        //    for (int i = 0; i < allCards.Length; i++)
        //    {
        //        if (allCards[i].IsUnlockedByDefault ||
        //            UnlockConditionChecker.IsSpecCardConditionMet(allCards[i], _runData))
        //        {
        //            available.Add(allCards[i]);
        //        }
        //    }

        //    if (available.Count == 0) return;

        //    var rng = new System.Random();
        //    var picked = new HashSet<int>();

        //    for (int i = 0; i < GameConstants.SPEC_CARDS_PER_LEVELUP; i++)
        //    {
        //        int idx = PickWeightedCardIndex(available, rng);
        //        // Avoid duplicate picks when possible
        //        int attempts = 0;
        //        while (picked.Contains(idx) && attempts < 20)
        //        {
        //            idx = PickWeightedCardIndex(available, rng);
        //            attempts++;
        //        }
        //        picked.Add(idx);
        //        _currentSpecOptions[i] = available[idx];
        //    }
        //}
        private void PresentSpecCards()
        {
            GameManager.Instance.SetState(GameState.LevelUp);

            _currentSpecOptions = new SpecCardData[GameConstants.SPEC_CARDS_PER_LEVELUP];

            var allCards = _database.SpecCards;
            if (allCards == null || allCards.Length == 0) return;

            // Filter to unlocked cards only
            var available = new List<SpecCardData>();

            for (int i = 0; i < allCards.Length; i++)
            {
                SpecCardData card = allCards[i];

                if (!IsSpecCardOfferable(card))
                    continue;

                available.Add(card);
            }

            if (available.Count == 0) return;

            if (_specCardOfferGenerator == null)
            {
                Debug.LogError("[SpecCards] SpecCardOfferGenerator is missing.");
                return;
            }

            float gradeBonus = _statModifiers != null ? _statModifiers.GetGradeBonus() : 0f;
            int currentWave = GetCurrentWaveForPity();

            var rng = new System.Random();

            List<SpecCardData> offer = _specCardOfferGenerator.GenerateOffer(
                available,
                currentWave,
                gradeBonus,
                rng
            );

            for (int i = 0; i < _currentSpecOptions.Length; i++)
            {
                _currentSpecOptions[i] = i < offer.Count ? offer[i] : null;
            }
        }

        /// <summary>
        /// Directly sets spec card options without generating new ones.
        /// Used when restoring a saved run where cards were already presented.
        /// </summary>
        public void SetSpecCardOptions(SpecCardData[] options)
        {
            _currentSpecOptions = options;
        }


        private bool IsSpecCardOfferable(SpecCardData card)
        {
            if (card == null)
                return false;

            bool unlocked = card.IsUnlockedByDefault ||
                UnlockConditionChecker.IsSpecCardConditionMet(card, _runData);

            if (!unlocked)
                return false;

            int currentStacks = 0;
            if (_runData != null && _runData.SpecStacks != null)
                _runData.SpecStacks.TryGetValue(card.EffectType, out currentStacks);

            if (!card.CanStack && currentStacks > 0)
                return false;

            if (card.MaxStacks > 0 && currentStacks >= card.MaxStacks)
                return false;

            return true;
        }

        private int PickWeightedCardIndex(List<SpecCardData> pool, System.Random rng)
        {
            float gradeBonus = _statModifiers != null ? _statModifiers.GetGradeBonus() : 0f;
            float totalWeight = 0f;

            for (int i = 0; i < pool.Count; i++)
                totalWeight += GetRarityWeight(pool[i].Rarity, gradeBonus);

            float roll = (float)(rng.NextDouble() * totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < pool.Count; i++)
            {
                cumulative += GetRarityWeight(pool[i].Rarity, gradeBonus);
                if (roll <= cumulative)
                    return i;
            }

            return pool.Count - 1;
        }

        private float GetRarityWeight(SpecCardRarity rarity, float gradeBonus)
        {
            float baseWeight = rarity switch
            {
                SpecCardRarity.Common => GameConstants.COMMON_WEIGHT,
                SpecCardRarity.Uncommon => GameConstants.UNCOMMON_WEIGHT,
                SpecCardRarity.Rare => GameConstants.RARE_WEIGHT,
                SpecCardRarity.Unique => GameConstants.UNIQUE_WEIGHT,
                SpecCardRarity.Legendary => GameConstants.LEGENDARY_WEIGHT,
                _ => GameConstants.COMMON_WEIGHT
            };

            // Lucky Charm trait: +4% better grade for higher rarities
            if (gradeBonus > 0 && rarity >= SpecCardRarity.Rare)
                baseWeight *= (1f + gradeBonus);

            return baseWeight;
        }

        private void RefreshWaveScore()
        {
            _runData.CurrentWave = _waveManager != null ? _waveManager.CurrentWave : _runData.CurrentWave;
            _runData.Score = ScoreCalculator.CalculateFinalScore(_runData);
            EventBus.Publish(new ScoreChangedEvent { Current = _runData.Score });
        }

        // =================================================================
        // EVENT HANDLERS
        // =================================================================

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            _runData.EnemiesKilled++;
            AddGold(evt.GoldReward);
            AddXP(evt.XPReward);
            //_runData.Score += evt.GoldReward + (int)evt.XPReward;
            //EventBus.Publish(new ScoreChangedEvent { Current = _runData.Score });

            RefreshWaveScore();


            // Meta currency drop chance
            if (Random.value < GameConstants.META_CURRENCY_DROP_CHANCE)
            {
                float baseMeta = 1f;
                // Meta bonuses are frozen at run start / snapshot time.
                float additive = 1f + GetActivePermanentBonus(5);
                float multiplicative = GetActivePermanentBonus(6) > 0f
                    ? GetActivePermanentBonus(6) : 1f;

                int meta = Mathf.Max(1, Mathf.RoundToInt(baseMeta * additive * multiplicative));
                _runData.MetaCurrencyEarned += meta;
                SaveSystem.AddMetaCurrency(meta);
            }

        }

      
        private void OnEnemyReachedEnd(EnemyReachedEndEvent evt)
        {
            if (_gameOverTriggered || GameManager.Instance.CurrentState == GameState.GameOver)
                return;

            _runData.Lives -= evt.Damage;
            EventBus.Publish(new LivesChangedEvent
            {
                Current = _runData.Lives,
                Max = _runData.MaxLives
            });

            if (_runData.Lives <= 0)
            {
                _gameOverTriggered = true;
                // Game over is handled once below after final score is calculated.

    //            LeaderboardStorage.AddRun(
    //survivedWave: _waveManager.CurrentWave,
    //runDurationSeconds: _runData.TotalTime,
    //earnedCrystals: _runData.MetaCurrencyEarned
//);


                //                SaveSystem.UpdateLeaderboard(_runData.Score);


                //var result = new ScoreAttackResult
                //{
                //    Score = _runData.Score,
                //    SurvivedWave = _waveManager.CurrentWave,
                //    DurationSeconds = Mathf.FloorToInt(_runData.TotalTime),
                //    EnemiesKilled = _runData.EnemiesKilled,
                //    EarnedCrystals = _runData.MetaCurrencyEarned
                //};

                RefreshWaveScore();

                GameManager.Instance.TriggerGameOver(_runData.Score, _runData.CurrentWave);
                // Meta currency is awarded immediately through SaveSystem.AddMetaCurrency during the run.
                // Do not add _runData.MetaCurrencyEarned again here, or crystals are double-counted.
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.RecordRunEnded(_runData.CurrentWave, _runData.EnemiesKilled);
                }
                else
                {
                    SaveSystem.RecordRunEnded(_runData.CurrentWave, _runData.EnemiesKilled);
                }

                if (ServiceLocator.TryGet<ILeaderboardService>(out var leaderboard))
                {
                    leaderboard.SubmitScore(new LeaderboardResult
                    {
                        Score = _runData.CurrentWave,
                        Wave = _runData.CurrentWave,
                        DurationSeconds = Mathf.FloorToInt(_runData.TotalTime),
                        EnemiesKilled = _runData.EnemiesKilled,
                        BossesDefeated = _runData.BossesDefeated,
                        TotalGoldEarned = _runData.TotalGoldEarned,
                        EarnedCrystals = _runData.MetaCurrencyEarned,
                        TurretsEvolved = _runData.TurretsEvolved,
                        LivesRemaining = Mathf.Max(0, _runData.Lives)
                    });
                }
                else
                {
                    Debug.LogWarning("[RunManager] No leaderboard service registered.");
                }


            }
        }

        private void OnSpecCardChosen(SpecCardChosenEvent evt)
        {
            if (_currentSpecOptions == null || evt.CardIndex < 0
                || evt.CardIndex >= _currentSpecOptions.Length)
                return;

            var card = _currentSpecOptions[evt.CardIndex];
            if (card == null)
                return;

            // Safety guard: cards should already be filtered before the offer is generated,
            // but keep this here to protect against direct event calls or stale UI.
            if (!IsSpecCardOfferable(card))
            {
                Debug.LogWarning($"[SpecCards] Ignored card '{card.name}' because it reached its stack limit.");
                return;
            }

            _runData.AddSpecBonus(card.EffectType, card.EffectValue);
            _runData.SpecCardsChosen++;

            // Special: heal
            if (card.EffectType == SpecCardEffectType.HealHealth)
            {
                _runData.Lives = Mathf.Min(_runData.Lives + (int)card.EffectValue, _runData.MaxLives);
                EventBus.Publish(new LivesChangedEvent
                {
                    Current = _runData.Lives,
                    Max = _runData.MaxLives
                });
            }

            _currentSpecOptions = null;

            // Resume game
            if(_waveManager.IsSpawning || _enemyManager.ActiveCount > 0)
            {
                GameManager.Instance.SetState(
                   GameState.WaveActive
               );
            }
            else
            {
                _waveManager.StartPrepPhase();
                GameManager.Instance.SetState(
                 GameState.Preparation
             );
               
            }

            EventBus.Publish(new SpecCardChosenAfterEvent { });
            //GameManager.Instance.SetState(
            //    _waveManager.IsSpawning || _enemyManager.ActiveCount > 0
            //        ? GameState.WaveActive
            //        : GameState.Preparation
            //);
        }

        private void OnTurretPlaced(TurretPlacedEvent evt)
        {
            _runData.TurretsPlaced++;
        }

        private void OnTurretSold(TurretSoldEvent evt)
        {
            AddGold(evt.RefundAmount);
        }

        private void OnTurretUpgraded(TurretUpgradedEvent evt)
        {
            // Track gold spent for Singularity Core trait
            // (upgrade cost already deducted via SpendGold which tracks TotalGoldSpent)
        }

        private void OnWaveStarted(WaveStartedEvent evt)
        {
            _runData.CurrentWave = evt.WaveNumber;
            _runData.Score = ScoreCalculator.CalculateFinalScore(_runData);
            EventBus.Publish(new ScoreChangedEvent
            {
                Current = _runData.Score
            });
        }

        // =================================================================
        // UTILITY
        // =================================================================

        public void SkipPrepTime()
        {
            float reward = _waveManager.SkipPrepTime();
           
            AddGold(Mathf.RoundToInt(reward));


            // Notify tutorial
            FindObjectOfType<InGameObjectives>()?.OnSkipButtonPressed();
        }

        /// <summary>
        /// Get trait bonus by type. Used by systems that don't go through RunStatModifiers.
        /// </summary>
        public float GetTraitBonus(TraitEffectType type)
        {
            float bonus = 0f;
            foreach (var traitId in _runData.ActiveTraitIds)
            {
                var trait = _database.GetTrait(traitId);
                if (trait != null && trait.EffectType == type)
                    bonus += trait.EffectValue;
            }
            return bonus;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Unsubscribe<EnemyReachedEndEvent>(OnEnemyReachedEnd);
            EventBus.Unsubscribe<SpecCardChosenEvent>(OnSpecCardChosen);
            EventBus.Unsubscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Unsubscribe<TurretSoldEvent>(OnTurretSold);
            EventBus.Unsubscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Unsubscribe<PrepPhaseSkipedEvent>(OnPrepSkipped);
            ServiceLocator.Unregister<RunManager>();
        }

        /// <summary>
        /// Pick a new set of spec card options. Called when player uses a re-roll token.
        /// </summary>
        public void RerollSpecCards()
        {
            PresentSpecCards();
            // Note: do NOT publish LevelUpEvent again  SpecCardSelectionUI
            // calls ShowCards() directly after calling this.
        }
    }
}