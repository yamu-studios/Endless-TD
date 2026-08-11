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
        private System.Random _specCardRng;

        // v1.0 Phase 4: non-blocking spec-card offers. Leveling up no longer pauses
        // the run (see [[etd-v1-full-release]]) — each level-up's offer is appended
        // here instead of overwriting a single slot, so players can keep playing and
        // resolve picks whenever they want, in order, without losing offers from
        // back-to-back level-ups.
        private readonly List<SpecCardData[]> _pendingOffers = new();
        public SpecCardData[] CurrentSpecOptions => _pendingOffers.Count > 0 ? _pendingOffers[0] : null;
        public int PendingOfferCount => _pendingOffers.Count;
        public IReadOnlyList<SpecCardData[]> PendingOffers => _pendingOffers;

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

            // Sandboxed tutorial run (see [[etd-v1-full-release]] tutorial redesign):
            // huge gold/lives buffer so nothing in the practice session is gated by
            // real economy or risks an accidental game over mid-lesson.
            if (GameManager.Instance != null && GameManager.Instance.IsTutorialMode)
            {
                _runData.Gold = 999999;
                _runData.MaxLives = 999999;
                _runData.Lives = 999999;

                // The tutorial's "cast a spell" objective needs SpellCastButton to be
                // visible, but it hides itself entirely when no spell is selected
                // (SelectedSpellId ""). A player who launches the tutorial without
                // ever picking one in Planning would otherwise get stuck on that step
                // with no button to press. In-memory only, like Gold/Lives above —
                // never persisted (IsTutorialMode skips SaveSystem.Save's disk write,
                // and LoadHub reloads from disk on exit). Real runs stay spell-less by
                // choice; this only auto-picks inside the sandbox.
                if (string.IsNullOrEmpty(save.SelectedSpellId) && _database.Spells != null && _database.Spells.Length > 0)
                    save.SelectedSpellId = _database.Spells[0].Id;
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

            // Initialize the v1.0 active spell system with the player's planning-tab selection.
            var spellManager = gameObject.AddComponent<SpellManager>();
            spellManager.Initialize(_database, save);

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

            // During the tutorial, hold XP just under the threshold until the
            // objective sequence actually reaches the LevelUp step — otherwise
            // real kill XP can level the player up before that objective appears.
            // Also hard-capped: the tutorial teaches exactly one level-up, so once the
            // player is at the cap the bar simply stops filling rather than queueing
            // spec-card offers behind the remaining objectives.
            bool tutorialLevelUpGated = GameManager.Instance != null
                && GameManager.Instance.IsTutorialMode
                && (!InGameObjectives.TutorialLevelUpGateOpen
                    || _runData.Level >= TutorialGates.PlayerLevelCap);
            if (tutorialLevelUpGated && _runData.CurrentXP >= _runData.XPToNextLevel)
                _runData.CurrentXP = _runData.XPToNextLevel - 1f;

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
        /// <summary>
        /// Leveling up no longer pauses the run (see [[etd-v1-full-release]]) — the
        /// offer is appended to the queue and the player resolves it whenever they
        /// open the spec-card picker, even hours later.
        /// </summary>
        private void PresentSpecCards()
        {
            var options = GenerateSpecCardOffer();
            if (options != null)
                _pendingOffers.Add(options);
        }

        /// <summary>
        /// Rerolls (free token or paid) replace the offer currently being viewed —
        /// the front of the queue — rather than appending a new one to the back.
        /// </summary>
        private void RerollCurrentOffer()
        {
            // Pass the offer being replaced so the new one cannot repeat any of its
            // cards. Without this the reroll just re-rolls the same weighted pool and
            // can legitimately hand back one or two identical cards, which reads to
            // players as "the reroll didn't work".
            SpecCardData[] previous = _pendingOffers.Count > 0 ? _pendingOffers[0] : null;

            var options = GenerateSpecCardOffer(previous);
            if (options == null) return;

            if (_pendingOffers.Count > 0)
                _pendingOffers[0] = options;
            else
                _pendingOffers.Add(options);
        }

        /// <summary>
        /// <paramref name="exclude"/> (optional) is the offer being replaced by a
        /// reroll; those cards are removed from the candidate pool so a reroll always
        /// visibly changes every slot. Ignored when honouring it would leave too few
        /// cards to fill an offer — a short offer would be worse than a repeat.
        /// </summary>
        private SpecCardData[] GenerateSpecCardOffer(SpecCardData[] exclude = null)
        {
            var allCards = _database.SpecCards;
            if (allCards == null || allCards.Length == 0) return null;

            // Filter to unlocked cards only
            var available = new List<SpecCardData>();

            for (int i = 0; i < allCards.Length; i++)
            {
                SpecCardData card = allCards[i];

                if (!IsSpecCardOfferable(card))
                    continue;

                available.Add(card);
            }

            if (available.Count == 0) return null;

            if (exclude != null)
            {
                int needed = GameConstants.SPEC_CARDS_PER_LEVELUP;
                int wouldRemain = available.Count;

                for (int i = 0; i < exclude.Length; i++)
                    if (exclude[i] != null && available.Contains(exclude[i]))
                        wouldRemain--;

                if (wouldRemain >= needed)
                {
                    for (int i = 0; i < exclude.Length; i++)
                        if (exclude[i] != null)
                            available.Remove(exclude[i]);
                }
                else
                {
                    Debug.LogWarning(
                        $"[SpecCards] Reroll pool too small to exclude the previous offer " +
                        $"({available.Count} offerable, need {needed}); repeats are possible.");
                }
            }

            if (_specCardOfferGenerator == null)
            {
                Debug.LogError("[SpecCards] SpecCardOfferGenerator is missing.");
                return null;
            }

            float gradeBonus = _statModifiers != null ? _statModifiers.GetGradeBonus() : 0f;
            int currentWave = GetCurrentWaveForPity();

            // Reused rather than constructed per call: a fresh System.Random seeded
            // from the clock can produce an identical sequence for two offers rolled
            // in the same tick, which is a second way a reroll appears to do nothing.
            _specCardRng ??= new System.Random();
            var rng = _specCardRng;

            List<SpecCardData> offer = _specCardOfferGenerator.GenerateOffer(
                available,
                currentWave,
                gradeBonus,
                rng
            );

            var options = new SpecCardData[GameConstants.SPEC_CARDS_PER_LEVELUP];
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = i < offer.Count ? offer[i] : null;
            }

            return options;
        }

        /// <summary>
        /// Restores one previously-offered (but unresolved) spec card offer onto the
        /// pending queue without generating a new one. Used when restoring a saved
        /// run — call once per queued offer, in original order.
        /// </summary>
        public void SetSpecCardOptions(SpecCardData[] options)
        {
            if (options != null)
                _pendingOffers.Add(options);
        }


        private bool IsSpecCardOfferable(SpecCardData card)
        {
            if (card == null)
                return false;

            bool unlocked = card.IsUnlockedByDefault ||
                UnlockConditionChecker.IsSpecCardConditionMet(card, _runData);

            if (!unlocked)
                return false;

            // Crit-chance cards provide no value once crit is already at the 100%
            // runtime cap (RunStatModifiers.GetCritChance clamps to 1). Filter both
            // variants from offers dynamically rather than deleting them from the DB.
            if (card.EffectType == SpecCardEffectType.CritChance ||
                card.EffectType == SpecCardEffectType.CritChanceStrong)
            {
                if (_statModifiers != null && _statModifiers.GetCritChance() >= 0.999f)
                    return false;
            }

            // Covenant offer biasing: while a Keystone Covenant is active, elemental
            // cards of the OTHER two elements stop appearing. Matching-element and
            // generic cards (damage, economy, utility, survival) remain, so builds
            // narrow without losing safety picks.
            var covenant = GetActiveCovenant();
            if (covenant.HasValue)
            {
                var cardElement = GetCardElement(card);
                if (cardElement.HasValue && cardElement.Value != covenant.Value)
                    return false;
            }

            // Turret-targeted cards stack per (effect, turret type), so e.g. the Frost
            // signature card reaching its cap must not also lock out the Void one.
            int currentStacks = 0;
            if (_runData != null)
            {
                if (card.TargetsTurretType)
                    currentStacks = _runData.GetSpecTurretStacks(card.EffectType, card.TargetTurretType);
                else if (_runData.SpecStacks != null)
                    _runData.SpecStacks.TryGetValue(card.EffectType, out currentStacks);
            }

            if (!card.CanStack && currentStacks > 0)
                return false;

            if (card.MaxStacks > 0 && currentStacks >= card.MaxStacks)
                return false;

            return true;
        }

        /// <summary>The run's active Keystone Covenant effect type, or null.</summary>
        private TraitEffectType? GetActiveCovenant()
        {
            if (_runData?.ActiveTraitIds == null || _database == null) return null;

            for (int i = 0; i < _runData.ActiveTraitIds.Count; i++)
            {
                var trait = _database.GetTrait(_runData.ActiveTraitIds[i]);
                if (trait != null && trait.IsKeystone)
                    return trait.EffectType;
            }
            return null;
        }

        /// <summary>Which covenant a spec card belongs to, or null for generic cards.</summary>
        private static TraitEffectType? GetCardElement(SpecCardData card)
        {
            if (card == null) return null;

            // Turret-type-targeted cards take their element from the turret they
            // specialise, not from the (shared) effect type.
            if (card.TargetsTurretType)
            {
                return card.TargetTurretType switch
                {
                    TurretType.Inferno => TraitEffectType.FlameCovenant,
                    TurretType.Frost => TraitEffectType.FrostCovenant,
                    TurretType.Lightning => TraitEffectType.StormCovenant,
                    TurretType.Void => TraitEffectType.VoidCovenant,
                    TurretType.Toxin => TraitEffectType.PlagueCovenant,
                    TurretType.Railgun => TraitEffectType.PrecisionCovenant,
                    _ => null // Basic/Laser/Support/Radar — generic, always offerable
                };
            }

            switch (card.EffectType)
            {
                case SpecCardEffectType.BurnDamage:
                case SpecCardEffectType.BurnDamageStrong:
                case SpecCardEffectType.DamageVsBurning:
                case SpecCardEffectType.BurnSpreadOnDeath:
                    return TraitEffectType.FlameCovenant;

                case SpecCardEffectType.SlowDuration:
                case SpecCardEffectType.SlowStrength:
                case SpecCardEffectType.DamageVsSlowedFrozen:
                case SpecCardEffectType.FreezeAmplifier:
                    return TraitEffectType.FrostCovenant;

                case SpecCardEffectType.ChainDamage:
                case SpecCardEffectType.ChainRange:
                case SpecCardEffectType.ChainTargetBonus:
                case SpecCardEffectType.ShockChance:
                    return TraitEffectType.StormCovenant;

                default:
                    return null; // generic — always offerable
            }
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
            var options = CurrentSpecOptions; // front of the pending-offer queue
            if (options == null || evt.CardIndex < 0 || evt.CardIndex >= options.Length)
                return;

            var card = options[evt.CardIndex];
            if (card == null)
                return;

            // Safety guard: cards should already be filtered before the offer is generated,
            // but keep this here to protect against direct event calls or stale UI.
            if (!IsSpecCardOfferable(card))
            {
                Debug.LogWarning($"[SpecCards] Ignored card '{card.name}' because it reached its stack limit.");
                return;
            }

            if (card.TargetsTurretType)
                _runData.AddSpecTurretBonus(card.EffectType, card.TargetTurretType, card.EffectValue);
            else
                _runData.AddSpecBonus(card.EffectType, card.EffectValue);
            _runData.SpecCardsChosen++;
            ResetPaidRerolls(); // offer consumed - next level-up starts at the cheapest paid reroll

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

            // Pop the resolved offer; any further queued offers (from back-to-back
            // level-ups) remain and CurrentSpecOptions now exposes the next one.
            if (_pendingOffers.Count > 0)
                _pendingOffers.RemoveAt(0);

            // v1.0: leveling up no longer pauses the run, so there's usually nothing
            // to "resume" here. Only force a state transition if something had
            // explicitly paused for this choice (e.g. a picker UI that chose to enter
            // GameState.LevelUp while open) — otherwise leave the run's state alone.
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.LevelUp)
            {
                if (_waveManager.IsSpawning || _enemyManager.ActiveCount > 0)
                {
                    GameManager.Instance.SetState(GameState.WaveActive);
                }
                else
                {
                    _waveManager.StartPrepPhase();
                    GameManager.Instance.SetState(GameState.Preparation);
                }
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
            RerollCurrentOffer();
        }

        // =================================================================
        // PAID CRYSTAL REROLLS (Fix 11)
        // Escalating crystal cost per offer, hard-capped per offer so build RNG
        // stays meaningful. Paid rerolls go through PresentSpecCards, so they
        // count as offers for pity (deliberate: prevents pity exploitation).
        // =================================================================

        [Header("Paid Crystal Rerolls")]
        [Tooltip("Crystal cost of the 1st/2nd/3rd paid reroll within one level-up offer. " +
                 "Array length = max paid rerolls per offer.")]
        [SerializeField] private int[] _crystalRerollCosts = { 10, 25, 60 };

        private int _paidRerollsThisOffer;

        public int MaxPaidRerollsPerOffer => _crystalRerollCosts != null ? _crystalRerollCosts.Length : 0;
        public int PaidRerollsUsedThisOffer => _paidRerollsThisOffer;

        /// <summary>Cost of the next paid reroll, or -1 when the per-offer cap is reached.</summary>
        public int GetNextPaidRerollCost()
        {
            if (_crystalRerollCosts == null || _paidRerollsThisOffer >= _crystalRerollCosts.Length)
                return -1;
            return Mathf.Max(0, _crystalRerollCosts[_paidRerollsThisOffer]);
        }

        /// <summary>Called when a card is chosen — the offer is consumed, costs reset.</summary>
        public void ResetPaidRerolls() => _paidRerollsThisOffer = 0;

        /// <summary>
        /// Spends persistent meta currency (crystals) for a fresh offer. Returns false
        /// when capped or unaffordable. UI should disable its button in those cases.
        /// </summary>
        public bool TryPaidCrystalReroll()
        {
            int cost = GetNextPaidRerollCost();
            if (cost < 0) return false;

            var save = SaveSystem.Load();
            if (save.MetaCurrency < cost) return false;

            save.MetaCurrency -= cost;
            SaveSystem.Save(save);
            EventBus.Publish(new MetaCurrencyChangedEvent
            {
                Current = save.MetaCurrency,
                Delta = -cost
            });

            _paidRerollsThisOffer++;
            RerollCurrentOffer();
            return true;
        }

        // Note (rerolls): do NOT publish LevelUpEvent again after a reroll;
        // SpecCardSelectionUI calls ShowCards() directly after calling it.
    }
}