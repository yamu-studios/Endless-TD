using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using ETD.Core;
using ETD.Data;
using ETD.Turrets;
using ETD.Grid;

namespace ETD.Meta
{
    public class ChallengeTracker : MonoBehaviour, IProjectileSpawnTelemetrySink, IChainLightningHitTelemetrySink, ILaserHitTelemetrySink
    {
        [SerializeField] private GameDatabase _database;

        [Header("Performance")]
        [Tooltip("Projectile-fired challenge checks are batched instead of checked for every projectile.")]
        [SerializeField] private int _projectileCheckBatchSize = 25;

        [Tooltip("Maximum delay before checking projectile-fired challenge progress.")]
        [SerializeField] private float _projectileCheckInterval = 0.50f;

        [Tooltip("Dense lightning builds batch challenge completion checks. Progress is still counted every frame.")]
        [SerializeField] private float _chainChallengeCheckInterval = 0.75f;

        [Tooltip("Laser challenge/unlock checks are now wall-clock and threshold gated. Lower only while debugging unlock timing.")]
        [SerializeField] private float _laserChallengeCheckInterval = 0.75f;

        // FIX (profiler-confirmed, 2026-06-30 capture): OnSniperDamage, OnCriticalHit,
        // OnBurningEnemyKill, and OnFreezeEnemy were calling CheckAllChallenges()
        // directly and unconditionally on every single event — and CheckAllChallenges()
        // itself ends with an unconditional SaveProgress() call, which performs a full
        // SaveSystem.Load() (disk read + JSON deserialize) and, when progress changed,
        // a full atomic SaveSystem.Save() (WriteAllText + Exists + Copy + Exists +
        // Delete + Move — 5 real filesystem operations). At wave 55+ with many turrets
        // landing sniper/critical hits every frame, this put synchronous disk I/O
        // directly on the main thread combat path, confirmed in profiler as 82ms+ self
        // time in JsonSaveSystem.Save() alone in a single frame. This matches the
        // exact symptom players reported: "CPU usage didn't break even 20% when the
        // game completely froze" — blocked disk I/O does not show as CPU work.
        // OnChainLightningHit and laser hits already used a throttled-check pattern;
        // this extends the same proven pattern to the remaining hot per-hit handlers.
        [Tooltip("Throttle for sniper/critical/burning/freeze challenge checks, which previously ran unconditionally on every hit.")]
        [SerializeField] private float _miscCombatChallengeCheckInterval = 0.75f;

        [Tooltip("When enabled, laser telemetry checks only laser-related challenges/unlocks instead of scanning every challenge on each laser threshold.")]
        [SerializeField] private bool _useSpecializedLaserChallengeCheck = true;


        [Header("Profiling")]
        [Tooltip("Keep disabled while diagnosing chain-lightning spikes. When disabled, the smaller ChallengeTracker.ChainBatch.* markers appear directly in the Profiler instead of being hidden under ChallengeTracker.ChainBatch.Total.")]
        [SerializeField] private bool _profileOuterChainBatchTotal = false;

        [Tooltip("Enable only while profiling ChallengeTracker laser batches. Keeping this disabled removes profiler-sample overhead from every laser telemetry batch.")]
        [SerializeField] private bool _profileLaserBatchDetails = false;

        private readonly Dictionary<ChallengeConditionType, float> _progress = new();
        private readonly HashSet<TurretType> _usedTurretTypes = new();
        private readonly HashSet<string> _usedEffects = new();
        private int _turretCount;
        private bool _usedSpell;
        private bool _usedSupportTurret;
        private bool _hadLeak;

        private float _killSpreeTimer;
        private int _killSpreeCount;
        private int _simultaneousBuffCount;
        private float _runStartTime;

        // Burning simultaneous tracking
        private int _currentBurningCount = 0;

        // Chain hits per event
        private int _maxChainHitsPerEvent = 0;

        // Laser time on target
        private float _laserTimeAccumulated = 0f;

        // Turret-type families (formerly "element" families). Bit positions for
        // Inferno/Frost/Lightning are unchanged from the old element system since
        // this mask is transient per-run state, not persisted. Basic and Laser were
        // added when the "3 elements" mechanic was widened to "3 turret types" —
        // add one more bit here per damage-dealing turret type introduced later.
        private const int TurretFamilyInferno = 1 << 0;
        private const int TurretFamilyFrost = 1 << 1;
        private const int TurretFamilyLightning = 1 << 2;
        private const int TurretFamilyBasic = 1 << 3;
        private const int TurretFamilyLaser = 1 << 4;
        private const int TurretFamilyVoid = 1 << 5;
        private const int TurretFamilyToxin = 1 << 6;
        private const int TurretFamilyRailgun = 1 << 7;

        // enemy id -> bitmask of turret-type families currently seen on that enemy.
        // A bitmask avoids allocating one HashSet<string> per enemy during dense
        // chain-lightning frames.
        private readonly Dictionary<int, int> _elementFamilyMaskByEnemy = new();
        private int _usedEffectFamilyMask;
        private bool _comboEffectsComplete;
        private bool _elementalistComplete;
        private float _nextChainChallengeCheckTime;
        private float _nextLaserChallengeCheckTime;
        private float _nextMiscCombatChallengeCheckTime;
        private bool _hasPendingLaserChallengeChecks = true;
        private float _nextContinuousBeamChallengeTarget = float.PositiveInfinity;
        private float _nextLaserTimeChallengeTarget = float.PositiveInfinity;
        private int _pendingProjectileChallengeChecks;
        private float _nextProjectileChallengeCheckTime;


        public void Initialize()
        {
            _progress.Clear();
            _usedTurretTypes.Clear();
            _usedEffects.Clear();
            _turretCount = 0;
            _usedSpell = false;
            _usedSupportTurret = false;
            _hadLeak = false;
            _killSpreeTimer = 0f;
            _killSpreeCount = 0;
            _runStartTime = Time.time;
            _pendingDamageProgress = 0f;
            _currentBurningCount = 0;
            _maxChainHitsPerEvent = 0;
            _laserTimeAccumulated = 0f;

            _elementFamilyMaskByEnemy.Clear();
            _usedEffectFamilyMask = 0;
            _comboEffectsComplete = false;
            _elementalistComplete = false;
            _nextChainChallengeCheckTime = 0f;
            _nextLaserChallengeCheckTime = 0f;
            _nextMiscCombatChallengeCheckTime = 0f;
            _nextGlobalChallengeCheckTime = 0f;
            _hasPendingLaserChallengeChecks = true;
            _nextContinuousBeamChallengeTarget = float.PositiveInfinity;
            _nextLaserTimeChallengeTarget = float.PositiveInfinity;
            _pendingProjectileChallengeChecks = 0;
            _nextProjectileChallengeCheckTime = 0f;



            // Load existing best progress from save so we don't lose it
            LoadProgressFromSave();
            _comboEffectsComplete = GetProgressValue(ChallengeConditionType.ComboEffectsOnEnemy) >= 3f;
            _elementalistComplete = GetProgressValue(ChallengeConditionType.UseAllEffects) >= 3f;
            RebuildLaserChallengeTargetCache(SaveSystem.Load());
            ChainLightningHitBatcher.WantsEnemyIds = !_comboEffectsComplete;

            ServiceLocator.Register(this);
            ServiceLocator.Register<IProjectileSpawnTelemetrySink>(this);
            ServiceLocator.Register<IChainLightningHitTelemetrySink>(this);
            ServiceLocator.Register<ILaserHitTelemetrySink>(this);
            ChainLightningHitBatcher.InvalidateTelemetrySink();

            EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            EventBus.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
            EventBus.Subscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Subscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Subscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Subscribe<GameOverEvent>(OnGameOver);
            EventBus.Subscribe<EnemyReachedEndEvent>(OnEnemyReachedEnd);
            EventBus.Subscribe<EnemyStatusAppliedEvent>(OnStatusApplied);
            EventBus.Subscribe<EnemyBurnedKilledEvent>(OnBurningEnemyKill);
            EventBus.Subscribe<ProjectileSpawnedEvent>(OnProjectileFired);
            EventBus.Subscribe<ProjectileSpawnedBatchEvent>(OnProjectileFiredBatch);
            EventBus.Subscribe<BuffTurretsEvent>(OnBuffSimultaneous);
            EventBus.Subscribe<ChainLightningHitEvent>(OnChainLightningHit);
            // v4: chain-hit batches are delivered directly through IChainLightningHitTelemetrySink.
            // Do not subscribe here or heavy lightning frames can be counted twice if legacy publishing is enabled.
            EventBus.Subscribe<CriticalHitEvent>(OnCriticalHit);
            EventBus.Subscribe<EliteEnemyKilled>(OnEliteEnemyKilled);
            EventBus.Subscribe<SniperDamageEvent>(OnSniperDamage);
            EventBus.Subscribe<EnemyStatusExpiredEvent>(OnStatusExpired);   // NEW — see note below
            EventBus.Subscribe<ChainLightningFiredEvent>(OnChainFired);     // NEW
            EventBus.Subscribe<LaserHitEvent>(OnLaserHit);                  // NEW
            EventBus.Subscribe<PercentHPDamageEvent>(OnPercentHPDamage);
            EventBus.Subscribe<StealthEnemyRevealedEvent>(OnStealthEnemyRevealed);
        }

        // =================================================================
        // PROGRESS ACCESS
        // =================================================================

        public float GetProgress(ChallengeData ch)
        {
            if (_progress.TryGetValue(ch.ConditionType, out float val))
                return Mathf.Min(val, ch.TargetValue);
            return 0f;
        }

        public float GetProgressNormalized(ChallengeData ch)
        {
            if (ch.TargetValue <= 0) return 0f;
            return Mathf.Clamp01(GetProgress(ch) / ch.TargetValue);
        }

        private float GetProgressValue(ChallengeConditionType type)
        {
            return _progress.TryGetValue(type, out float value) ? value : 0f;
        }

        // =================================================================
        // PROGRESS INTERNAL
        // =================================================================

        private void AddProgress(ChallengeConditionType type, float amount)
        {
            if (!_progress.ContainsKey(type)) _progress[type] = 0;
            _progress[type] += amount;
        }

        private void SetProgress(ChallengeConditionType type, float value)
        {
            if (!_progress.ContainsKey(type) || value > _progress[type])
                _progress[type] = value;

            if (type == ChallengeConditionType.ComboEffectsOnEnemy && value >= 3f)
            {
                _comboEffectsComplete = true;
                ChainLightningHitBatcher.WantsEnemyIds = false;
            }
            else if (type == ChallengeConditionType.UseAllEffects && value >= 3f)
            {
                _elementalistComplete = true;
            }
        }

        // =================================================================
        // SAVE / LOAD PROGRESS
        // =================================================================

        private void LoadProgressFromSave()
        {
            var save = SaveSystem.Load();
            if (save.ChallengeProgressTypes == null) return;

            for (int i = 0; i < save.ChallengeProgressTypes.Length; i++)
            {
                var type = (ChallengeConditionType)save.ChallengeProgressTypes[i];
                float val = save.ChallengeProgressValues != null && i < save.ChallengeProgressValues.Length
                    ? save.ChallengeProgressValues[i] : 0f;
                _progress[type] = val;
            }

            // Sync lifetime gold from SaveData into progress dict
            var lifetimeSave = SaveSystem.Load();
            if (lifetimeSave.LifetimeGoldSpent > 0)
                _progress[ChallengeConditionType.SpendGoldLifetime] = lifetimeSave.LifetimeGoldSpent;
        }

        /// <summary>
        /// Save all current progress values to SaveData.
        /// Called after each meaningful event and on game over.
        /// Only updates if new value is greater (best-ever tracking).
        /// </summary>
        private void SaveProgress()
        {
            var save = SaveSystem.Load();
            bool changed = false;

            foreach (var kvp in _progress)
            {
                float saved = SaveSystem.GetChallengeProgress(save, (int)kvp.Key);
                if (kvp.Value > saved)
                {
                    SaveSystem.SetChallengeProgress(save, (int)kvp.Key, kvp.Value);
                    changed = true;
                }
            }

            if (changed) SaveSystem.Save(save);
        }

        // =================================================================
        // CHECK & COMPLETE
        // =================================================================

        // FIX (systemic, profiler-confirmed): Individually throttling call sites as
        // they show up in profiler captures was reactive and incomplete — this file
        // has 18 separate EventBus subscriptions, and OnStatusApplied (fires on every
        // burn/slow/freeze application, i.e. constantly in dense combat) still called
        // raw, unthrottled CheckAllChallenges() -> SaveProgress() -> synchronous disk
        // I/O, confirmed costing 12.77ms self in JsonSaveSystem.Save() in a live
        // capture, despite four OTHER handlers already being fixed the same way.
        //
        // Rather than keep patching individual handlers one profiler screenshot at a
        // time, the throttle now lives inside CheckAllChallenges() itself. Every
        // caller — including any handler not yet audited, and any added in the
        // future — is protected by default. Genuinely rare, correctness-critical
        // callers (game over, wave completed) pass forceImmediate: true to bypass it,
        // since those must never silently skip a save.
        [Tooltip("Global floor between CheckAllChallenges() disk-touching scans, regardless of which handler triggers it. Progress values (AddProgress/SetProgress) are unaffected and still recorded every call.")]
        [SerializeField] private float _globalChallengeCheckInterval = 0.35f;
        private float _nextGlobalChallengeCheckTime;

        private void CheckAllChallenges(bool forceImmediate = false)
        {
            if (!forceImmediate && Time.time < _nextGlobalChallengeCheckTime)
                return;
            _nextGlobalChallengeCheckTime = Time.time + Mathf.Max(0.05f, _globalChallengeCheckInterval);

            if (_database?.Challenges == null) return;

            var save = SaveSystem.Load();
            var completed = new HashSet<string>(save.CompletedChallengeIds ?? System.Array.Empty<string>());
            bool anyNew = false;

            for (int i = 0; i < _database.Challenges.Length; i++)
            {
                var ch = _database.Challenges[i];
                if (completed.Contains(ch.Id)) continue;

                if (GetProgress(ch) >= ch.TargetValue)
                {
                    var list = new List<string>(save.CompletedChallengeIds ?? System.Array.Empty<string>());
                    list.Add(ch.Id);
                    save.CompletedChallengeIds = list.ToArray();
                    anyNew = true;

                    //ApplyReward(ch, save);
                    CheckUnlockConditions(ch, save);
                    HubBadgeRegistry.RegisterNew(HubBadgeType.Challenge, ch.Id);
                    EventBus.Publish(new ChallengeCompletedEvent
                    {
                        ChallengeId = ch.Id
                    });

                    EventBus.Publish(new UnlockNotificationEvent
                    {
                        UnlockType = (int)UnlockType.Challenge,
                        DisplayName = SOLocalization.GetName("challenge_"+ch.LocalizationKey,ch.DisplayName),
                        Description = ch.GetRewardDescription()
                    });
                }

                //if (GetProgress(ch) >= ch.TargetValue)
                //{
                //    var list = new List<string>(
                //        save.CompletedChallengeIds ?? System.Array.Empty<string>());
                //    list.Add(ch.Id);
                //    save.CompletedChallengeIds = list.ToArray();
                //    anyNew = true;

                //    // DO NOT call ApplyReward here — reward claimed in hub UI
                //    HubBadgeRegistry.RegisterNew(HubBadgeType.Challenge, ch.Id);

                //    EventBus.Publish(new UnlockNotificationEvent
                //    {
                //        UnlockType = (int)UnlockType.Challenge,
                //        DisplayName = ch.DisplayName,
                //        Description = ch.GetRewardDescription()
                //    });
                //}
            }

            bool unlockedAnyTrait = CheckTraitUnlocks(save);
            bool unlockedAnyTurret = CheckTurretUnlocks(save);   // ← ADD
            if (anyNew || unlockedAnyTrait || unlockedAnyTurret) SaveSystem.Save(save);
            //if (anyNew) SaveSystem.Save(save);
            //if (anyNew) SaveSystem.Save(save);

            // Always persist progress after checking
            SaveProgress();


        }

        private void ApplyReward(ChallengeData ch, SaveData save)
        {
            switch (ch.RewardType)
            {
                case ChallengeRewardType.FlatCurrency:
                    save.MetaCurrency += Mathf.RoundToInt(ch.RewardValue);
                    break;
                case ChallengeRewardType.MaxHPBonus:
                    save.PermanentBonuses[0] += ch.RewardValue;
                    break;
                case ChallengeRewardType.XPMultiplier:
                    if (ch.StackType == ChallengeStackType.Additive)
                        save.PermanentBonuses[1] += (ch.RewardValue - 1f);
                    else
                        save.PermanentBonuses[1] = (1f + save.PermanentBonuses[1]) * ch.RewardValue - 1f;
                    break;
                case ChallengeRewardType.GoldMultiplier:
                    if (ch.StackType == ChallengeStackType.Additive)
                        save.PermanentBonuses[2] += (ch.RewardValue - 1f);
                    else
                        save.PermanentBonuses[2] = (1f + save.PermanentBonuses[2]) * ch.RewardValue - 1f;
                    break;
                case ChallengeRewardType.DamageBonus:
                    if (ch.StackType == ChallengeStackType.Additive)
                        save.PermanentBonuses[3] += (ch.RewardValue - 1f);
                    else
                        save.PermanentBonuses[3] = (1f + save.PermanentBonuses[3]) * ch.RewardValue - 1f;
                    break;
                case ChallengeRewardType.MetaCurrencyMultiplier:
                    if (ch.StackType == ChallengeStackType.Additive)
                        save.PermanentBonuses[5] += (ch.RewardValue - 1f);
                    else
                    {
                        float cur = save.PermanentBonuses[6] == 0 ? 1f : save.PermanentBonuses[6];
                        save.PermanentBonuses[6] = cur * ch.RewardValue;
                    }
                    break;
            }
        }

        //private void CheckUnlockConditions(ChallengeData completed, SaveData save)
        //{
        //    if (_database.Traits != null)
        //    {
        //        var unlocked = new HashSet<string>(save.UnlockedTraitIds ?? System.Array.Empty<string>());
        //        foreach (var trait in _database.Traits)
        //        {
        //            if (trait.IsUnlockedByDefault || unlocked.Contains(trait.Id)) continue;
        //            if (!string.IsNullOrEmpty(trait.UnlockCondition)
        //                && trait.UnlockCondition.Contains(completed.DisplayName))
        //            {
        //                var list = new List<string>(save.UnlockedTraitIds ?? System.Array.Empty<string>());
        //                list.Add(trait.Id);
        //                save.UnlockedTraitIds = list.ToArray();
        //                EventBus.Publish(new UnlockNotificationEvent
        //                {
        //                    UnlockType = (int)UnlockType.Trait,
        //                    DisplayName = trait.DisplayName,
        //                    Description = "New trait available in Planning!"
        //                });
        //            }
        //        }
        //    }
        //}

        private void CheckUnlockConditions(ChallengeData completed, SaveData save)
        {
            if (_database?.Traits == null) return;

            var unlocked = new HashSet<string>(save.UnlockedTraitIds ?? System.Array.Empty<string>());

            foreach (var trait in _database.Traits)
            {
                if (trait.IsUnlockedByDefault || unlocked.Contains(trait.Id)) continue;

                // Check if this trait's unlock condition is now met
                if (trait.UnlockConditionTarget > 0)
                {
                    float progress = SaveSystem.GetChallengeProgress(save, (int)trait.UnlockConditionType);
                    if (progress >= trait.UnlockConditionTarget)
                    {
                        var list = new List<string>(save.UnlockedTraitIds ?? System.Array.Empty<string>());
                        list.Add(trait.Id);
                        save.UnlockedTraitIds = list.ToArray();

                        EventBus.Publish(new UnlockNotificationEvent
                        {
                            UnlockType = (int)UnlockType.Trait,
                            DisplayName = SOLocalization.GetName("trait_" + trait.LocalizationKey, trait.DisplayName),
                            Description = LocalizationManager.Get("notification_new_trait_available_body", "New trait available in Planning!")
                        });
                    }
                }
            }
        }

        public void OnBuffSimultaneous(BuffTurretsEvent evt)
        {
            SetProgress(ChallengeConditionType.BuffTurretsSimultaneous, evt.Count);
            // No CheckAll here — runs every frame, save on wave complete
        }

        // =================================================================
        // EVENT HANDLERS
        // =================================================================

        //private void OnEnemyKilled(EnemyKilledEvent evt)
        //{
        //    AddProgress(ChallengeConditionType.DealTotalDamage, evt.GoldReward * 10f);
        //    CheckAllChallenges();
        //}

        
        // DealTotalDamage now tracks REAL damage dealt via EnemyDamagedEvent.
        // The old proxy (kill gold * 10) drifted badly once kill gold started
        // scaling with wave, and never matched the challenge targets, which were
        // authored in damage units (e.g. "Overkill" = 1,000,000).
        // Accumulated locally per hit (cheap) and flushed into persistent
        // progress on wave complete / game over instead of per event.
        private float _pendingDamageProgress;

        private void OnEnemyDamaged(EnemyDamagedEvent evt)
        {
            if (evt.Amount > 0f)
                _pendingDamageProgress += evt.Amount;
        }

        private void FlushPendingDamageProgress()
        {
            if (_pendingDamageProgress <= 0f)
                return;

            AddProgress(ChallengeConditionType.DealTotalDamage, _pendingDamageProgress);
            _pendingDamageProgress = 0f;
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            // Kill spree: 50 kills within 5 seconds
            if (Time.time - _killSpreeTimer <= 5f)
            {
                _killSpreeCount++;
                SetProgress(ChallengeConditionType.KillSpree, _killSpreeCount);
            }
            else
            {
                _killSpreeTimer = Time.time;
                _killSpreeCount = 1;
                SetProgress(ChallengeConditionType.KillSpree, 1);
            }

            // Elite kills
            if (evt.EnemyTier == (int)EnemyTier.Elite || evt.EnemyTier == (int)EnemyTier.Boss)
                AddProgress(ChallengeConditionType.KillEliteEnemies, 1);

            CheckAllChallenges();
        }

        private void OnWaveCompleted(WaveCompletedEvent evt)
        {
            FlushPendingDamageProgress();

            float minutesSurvived = (Time.time - _runStartTime) / 60f;
            SetProgress(ChallengeConditionType.SurviveMinutes, minutesSurvived);
            SetProgress(ChallengeConditionType.ReachWave, evt.WaveNumber);

            if (_usedTurretTypes.Count == 1)
                SetProgress(ChallengeConditionType.SingleTurretTypeWave, evt.WaveNumber);
            if (!_usedSupportTurret)
                SetProgress(ChallengeConditionType.NoSupportWave, evt.WaveNumber);
            if (!_usedSpell)
                SetProgress(ChallengeConditionType.NoSpellWave, evt.WaveNumber);
            if (!_hadLeak)
                SetProgress(ChallengeConditionType.NoLeaksUntilWave, evt.WaveNumber);
            if (CountElementBits(_usedEffectFamilyMask) <= 1)
                SetProgress(ChallengeConditionType.SingleDamageTypeWave, evt.WaveNumber);

            SetProgress(ChallengeConditionType.MaxTurretsWave, evt.WaveNumber);
            _pendingProjectileChallengeChecks = 0;
            // FIX: wave-complete is a rare (once per wave), correctness-sensitive
            // checkpoint — force it through even if the global throttle window
            // hasn't elapsed, so wave-completion rewards/challenges never feel
            // delayed or silently skipped.
            CheckAllChallenges(forceImmediate: true);
        }

        private void OnTurretPlaced(TurretPlacedEvent evt)
        {
            _turretCount++;
            AddProgress(ChallengeConditionType.BuildTurrets, 1);

            if (ServiceLocator.TryGet<TurretManager>(out var mgr))
            {
                var turret = mgr.GetTurret(evt.TurretId);
                if (turret != null)
                {
                    _usedTurretTypes.Add(turret.Data.Type);
                    if (turret.Data.Type == TurretType.Support)
                        _usedSupportTurret = true;
                }
            }

            if (ServiceLocator.TryGet<GridSystem>(out var grid))
            {
                var cell = grid.GetCell(evt.GridPos);
                if (cell != null && cell.Specialty != TileSpecialty.None)
                {
                    AddProgress(ChallengeConditionType.UseSpecialTiles, 1);
                    if (cell.Specialty == TileSpecialty.Greed)
                        AddProgress(ChallengeConditionType.UseGreedTiles, 1);
                }
            }

            CheckAllChallenges();
        }

        private void OnTurretUpgraded(TurretUpgradedEvent evt)
        {
            AddProgress(ChallengeConditionType.UpgradeTimes, 1);
            CheckAllChallenges();
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            if (evt.Delta > 0)
            {
                float cur = _progress.TryGetValue(ChallengeConditionType.EarnGoldInRun, out float v) ? v : 0f;
                if (evt.Current > cur) SetProgress(ChallengeConditionType.EarnGoldInRun, evt.Current);
            }
            else if (evt.Delta < 0)
            {
                float spent = Mathf.Abs(evt.Delta);

                // Per-run gold spent
                AddProgress(ChallengeConditionType.SpendGoldInRun, spent);

                // Lifetime gold spent — persisted directly to SaveData
                var save = SaveSystem.Load();
                save.LifetimeGoldSpent += spent;
                SaveSystem.Save(save);
                // Sync to progress dict so it can unlock traits
                SetProgress(ChallengeConditionType.SpendGoldLifetime, save.LifetimeGoldSpent);
            }

            float held = _progress.TryGetValue(ChallengeConditionType.HoldGoldAtOnce, out float h) ? h : 0f;
            if (evt.Current > held) SetProgress(ChallengeConditionType.HoldGoldAtOnce, evt.Current);

            CheckAllChallenges();
        }

        // Radar unlock progress (Signals Mastery). Already latched per enemy in
        // EnemyController.AddRadarReveal, so this is a plain +1 per distinct enemy.
        private void OnStealthEnemyRevealed(StealthEnemyRevealedEvent evt)
        {
            AddProgress(ChallengeConditionType.RevealStealthEnemies, 1);
        }

        private void OnPercentHPDamage(PercentHPDamageEvent evt)
        {
            AddProgress(ChallengeConditionType.PercentHPDamage, evt.DamageAmount);
            CheckAllChallenges();
        }
        //private void OnGoldChanged(GoldChangedEvent evt)
        //{
        //    if (evt.Delta > 0)
        //    {
        //        // Gold earned
        //        float cur = _progress.TryGetValue(ChallengeConditionType.EarnGoldInRun, out float v) ? v : 0f;
        //        if (evt.Current > cur) SetProgress(ChallengeConditionType.EarnGoldInRun, evt.Current);
        //    }
        //    else if (evt.Delta < 0)
        //    {
        //        // Gold spent
        //        AddProgress(ChallengeConditionType.SpendGoldInRun, Mathf.Abs(evt.Delta));

        //    }

        //    // Hold gold: track highest amount held at once
        //    float held = _progress.TryGetValue(ChallengeConditionType.HoldGoldAtOnce, out float h) ? h : 0f;
        //    if (evt.Current > held) SetProgress(ChallengeConditionType.HoldGoldAtOnce, evt.Current);

        //    CheckAllChallenges();
        //}

        public void OnProjectileFired(ProjectileSpawnedEvent evt)
        {
            AddProjectileFiredProgress(1);
        }

        public void OnProjectileFiredBatch(ProjectileSpawnedBatchEvent evt)
        {
            ReportProjectileSpawns(evt.Count);
        }

        /// <summary>
        /// Direct high-frequency telemetry path used by ProjectileSpawnEventBatcher.
        /// Progress is exact, while expensive challenge evaluation is time-throttled.
        /// </summary>
        public void ReportProjectileSpawns(int count)
        {
            AddProjectileFiredProgress(Mathf.Max(1, count));
        }

        private void AddProjectileFiredProgress(int count)
        {
            AddProgress(ChallengeConditionType.TotalProjectilesFired, count);
            _pendingProjectileChallengeChecks = _pendingProjectileChallengeChecks > int.MaxValue - count
                ? int.MaxValue
                : _pendingProjectileChallengeChecks + count;

            // Dense projectile builds can fire thousands of shots per frame. Checking
            // every challenge whenever an arbitrary count threshold is crossed turns
            // the telemetry batch back into a per-frame spike. Keep progress exact,
            // but evaluate at the configured time cadence (and always on wave/game end).
            if (Time.unscaledTime >= _nextProjectileChallengeCheckTime)
                FlushProjectileChallengeCheck();
        }

        private void FlushProjectileChallengeCheck()
        {
            if (_pendingProjectileChallengeChecks <= 0) return;

            _pendingProjectileChallengeChecks = 0;
            _nextProjectileChallengeCheckTime = Time.unscaledTime + Mathf.Max(0.05f, _projectileCheckInterval);
            CheckAllChallenges();
        }
        private void OnEnemyReachedEnd(EnemyReachedEndEvent evt)
        {
            _hadLeak = true;
        }

        //private void OnStatusApplied(EnemyStatusAppliedEvent evt)
        //{
        //    var status = (StatusEffectType)evt.StatusType;
        //    switch (status)
        //    {
        //        case StatusEffectType.Burn:
        //            _usedEffects.Add("burn");
        //            AddProgress(ChallengeConditionType.DealBurnDamage, 100);
        //            // Update simultaneous burning peak
        //            SetProgress(ChallengeConditionType.BurningSimultaneous, _currentBurningCount);
        //            break;
        //        case StatusEffectType.Slow:
        //            AddProgress(ChallengeConditionType.SlowTotalSeconds, evt.Duration);
        //            AddProgress(ChallengeConditionType.DamageSlovedEnemies, 1);
        //            break;
        //        case StatusEffectType.Freeze:
        //            _usedEffects.Add("slow");
        //            AddProgress(ChallengeConditionType.FreezeEnemiesTotal, 1);
        //            break;

        //    }

        //    if (_usedEffects.Contains("burn") && _usedEffects.Contains("slow"))
        //        SetProgress(ChallengeConditionType.UseAllEffects, 1);

        //    CheckAllChallenges();
        //}
        private void OnStatusApplied(EnemyStatusAppliedEvent evt)
        {
            var status = (StatusEffectType)evt.StatusType;

            TrackElementFamilyFromStatus(evt.EnemyId, status);

            switch (status)
            {
                case StatusEffectType.Burn:
                    AddProgress(ChallengeConditionType.DealBurnDamage, 100);

                    _currentBurningCount++;
                    SetProgress(ChallengeConditionType.BurningSimultaneous, _currentBurningCount);
                    break;

                case StatusEffectType.Slow:
                    AddProgress(ChallengeConditionType.SlowTotalSeconds, evt.Duration);
                    AddProgress(ChallengeConditionType.DamageSlovedEnemies, 1);
                    break;

                case StatusEffectType.Freeze:
                    AddProgress(ChallengeConditionType.FreezeEnemiesTotal, 1);
                    break;
            }

            CheckAllChallenges();
        }


        //private void OnStatusExpired(EnemyStatusExpiredEvent evt)
        //{
        //    if ((StatusEffectType)evt.StatusType == StatusEffectType.Burn)
        //    {
        //        _currentBurningCount = Mathf.Max(0, _currentBurningCount - 1);
        //        // Note: we do NOT decrease the peak progress — it tracks best-ever
        //    }
        //}

        private void OnStatusExpired(EnemyStatusExpiredEvent evt)
        {
            var status = (StatusEffectType)evt.StatusType;

            if (status == StatusEffectType.Burn)
            {
                _currentBurningCount = Mathf.Max(0, _currentBurningCount - 1);
                // Do not lower BurningSimultaneous progress. It tracks best-ever peak.
            }

            if (_elementFamilyMaskByEnemy.TryGetValue(evt.EnemyId, out int mask))
            {
                switch (status)
                {
                    case StatusEffectType.Burn:
                        mask &= ~TurretFamilyInferno;
                        break;

                    case StatusEffectType.Slow:
                    case StatusEffectType.Freeze:
                        mask &= ~TurretFamilyFrost;
                        break;

                    case StatusEffectType.ArmorBreak:
                        mask &= ~TurretFamilyBasic;
                        break;

                    case StatusEffectType.HPPercentReduce:
                        mask &= ~TurretFamilyLaser;
                        break;

                    case StatusEffectType.Weaken:
                        mask &= ~TurretFamilyVoid;
                        break;

                    case StatusEffectType.Poison:
                        mask &= ~TurretFamilyToxin;
                        break;

                    case StatusEffectType.Expose:
                        mask &= ~TurretFamilyRailgun;
                        break;
                }

                if (mask == 0)
                    _elementFamilyMaskByEnemy.Remove(evt.EnemyId);
                else
                    _elementFamilyMaskByEnemy[evt.EnemyId] = mask;
            }
        }


        private void OnChainFired(ChainLightningFiredEvent evt)
        {
            // evt.HitCount = how many enemies were hit in this single chain event
            if (evt.HitCount > _maxChainHitsPerEvent)
            {
                _maxChainHitsPerEvent = evt.HitCount;
                SetProgress(ChallengeConditionType.ChainHitsPerEvent, _maxChainHitsPerEvent);
                CheckAllChallenges();
            }
        }

        //private void OnLaserHit(LaserHitEvent evt)
        //{
        //    // Accumulate laser time on any target
        //    _laserTimeAccumulated += evt.DeltaTime;
        //    SetProgress(ChallengeConditionType.LaserTimeOnTarget, _laserTimeAccumulated);

        //    // Don't call CheckAll every frame — too expensive
        //    // Instead check on a timer, or on wave complete
        //}

        private void OnLaserHit(LaserHitEvent evt)
        {
            // Legacy fallback path. Normal laser telemetry now uses ILaserHitTelemetrySink.
            OnLaserHitBatch(evt.DeltaTime, evt.TargetEnemyId);
        }

        public void OnLaserHitBatch(float deltaTime, int targetEnemyId)
        {
            if (deltaTime <= 0f)
                return;

            if (_profileLaserBatchDetails)
                Profiler.BeginSample("ChallengeTracker.LaserBatch.01.AddProgress");

            _laserTimeAccumulated += deltaTime;

            _progress.TryGetValue(ChallengeConditionType.ContinuousBeamSeconds, out float continuousBeamProgress);
            continuousBeamProgress += deltaTime;
            _progress[ChallengeConditionType.ContinuousBeamSeconds] = continuousBeamProgress;

            // Optional compatibility if other unlocks use it. Existing game logic treated this as
            // cumulative laser time, so keep that behavior but avoid the heavier SetProgress path.
            if (!_progress.TryGetValue(ChallengeConditionType.LaserTimeOnTarget, out float savedLaserTime)
                || _laserTimeAccumulated > savedLaserTime)
            {
                _progress[ChallengeConditionType.LaserTimeOnTarget] = _laserTimeAccumulated;
            }

            if (_profileLaserBatchDetails)
                Profiler.EndSample();

            if (!_hasPendingLaserChallengeChecks)
                return;

            // v6: the old code used accumulated laser time as the throttle timer. With many laser
            // turrets, that made CheckAllChallenges run almost every rendered frame. Now the hot
            // path is threshold gated: no challenge/save scan happens until laser progress reaches
            // the next relevant challenge, trait, or turret unlock target.
            if (continuousBeamProgress < _nextContinuousBeamChallengeTarget
                && _laserTimeAccumulated < _nextLaserTimeChallengeTarget)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now < _nextLaserChallengeCheckTime)
                return;

            _nextLaserChallengeCheckTime = now + Mathf.Max(0.10f, _laserChallengeCheckInterval);

            if (_profileLaserBatchDetails)
                Profiler.BeginSample("ChallengeTracker.LaserBatch.02.CheckDue");

            if (_useSpecializedLaserChallengeCheck)
            {
                CheckLaserRelatedChallengesOnly();
            }
            else
            {
                CheckAllChallenges();
                RebuildLaserChallengeTargetCache(SaveSystem.Load());
            }

            if (_profileLaserBatchDetails)
                Profiler.EndSample();
        }


        private void CheckLaserRelatedChallengesOnly()
        {
            SaveData save;

            if (_profileLaserBatchDetails)
                Profiler.BeginSample("ChallengeTracker.LaserBatch.Check.01.LoadCachedSave");
            save = SaveSystem.Load();
            if (_profileLaserBatchDetails)
                Profiler.EndSample();

            bool changed = false;
            bool anyNewChallenge = false;

            if (_profileLaserBatchDetails)
                Profiler.BeginSample("ChallengeTracker.LaserBatch.Check.02.SaveLaserProgress");
            changed |= SaveProgressType(save, ChallengeConditionType.ContinuousBeamSeconds);
            changed |= SaveProgressType(save, ChallengeConditionType.LaserTimeOnTarget);
            if (_profileLaserBatchDetails)
                Profiler.EndSample();

            if (_profileLaserBatchDetails)
                Profiler.BeginSample("ChallengeTracker.LaserBatch.Check.03.CompleteLaserChallenges");
            anyNewChallenge = CompleteChallengesForConditions(save,
                ChallengeConditionType.ContinuousBeamSeconds,
                ChallengeConditionType.LaserTimeOnTarget);
            if (_profileLaserBatchDetails)
                Profiler.EndSample();

            if (_profileLaserBatchDetails)
                Profiler.BeginSample("ChallengeTracker.LaserBatch.Check.04.UnlockLaserTraits");
            bool unlockedAnyTrait = CheckTraitUnlocksForConditions(save,
                ChallengeConditionType.ContinuousBeamSeconds,
                ChallengeConditionType.LaserTimeOnTarget);
            if (_profileLaserBatchDetails)
                Profiler.EndSample();

            if (_profileLaserBatchDetails)
                Profiler.BeginSample("ChallengeTracker.LaserBatch.Check.05.UnlockLaserTurrets");
            bool unlockedAnyTurret = CheckTurretUnlocksForConditions(save,
                ChallengeConditionType.ContinuousBeamSeconds,
                ChallengeConditionType.LaserTimeOnTarget);
            if (_profileLaserBatchDetails)
                Profiler.EndSample();

            if (changed || anyNewChallenge || unlockedAnyTrait || unlockedAnyTurret)
            {
                if (_profileLaserBatchDetails)
                    Profiler.BeginSample("ChallengeTracker.LaserBatch.Check.06.SaveIfChanged");
                SaveSystem.Save(save);
                if (_profileLaserBatchDetails)
                    Profiler.EndSample();
            }

            if (_profileLaserBatchDetails)
                Profiler.BeginSample("ChallengeTracker.LaserBatch.Check.07.RebuildTargets");
            RebuildLaserChallengeTargetCache(save);
            if (_profileLaserBatchDetails)
                Profiler.EndSample();
        }

        private bool SaveProgressType(SaveData save, ChallengeConditionType type)
        {
            if (!_progress.TryGetValue(type, out float current))
                return false;

            float saved = SaveSystem.GetChallengeProgress(save, (int)type);
            if (current <= saved)
                return false;

            SaveSystem.SetChallengeProgress(save, (int)type, current);
            return true;
        }

        private bool CompleteChallengesForConditions(SaveData save, ChallengeConditionType a, ChallengeConditionType b)
        {
            if (_database?.Challenges == null)
                return false;

            bool anyNew = false;
            for (int i = 0; i < _database.Challenges.Length; i++)
            {
                ChallengeData ch = _database.Challenges[i];
                if (ch == null)
                    continue;

                if (ch.ConditionType != a && ch.ConditionType != b)
                    continue;

                if (IsStringInArray(save.CompletedChallengeIds, ch.Id))
                    continue;

                if (GetProgress(ch) < ch.TargetValue)
                    continue;

                AddStringUnique(ref save.CompletedChallengeIds, ch.Id);
                anyNew = true;

                // Keep the same side effects as the full checker, but this path runs only when a
                // laser-related threshold is crossed instead of scanning all challenges repeatedly.
                CheckUnlockConditions(ch, save);
                HubBadgeRegistry.RegisterNew(HubBadgeType.Challenge, ch.Id);
                EventBus.Publish(new ChallengeCompletedEvent
                {
                    ChallengeId = ch.Id
                });

                EventBus.Publish(new UnlockNotificationEvent
                {
                    UnlockType = (int)UnlockType.Challenge,
                    DisplayName = SOLocalization.GetName("challenge_" + ch.LocalizationKey, ch.DisplayName),
                    Description = ch.GetRewardDescription()
                });
            }

            return anyNew;
        }

        private bool CheckTraitUnlocksForConditions(SaveData save, ChallengeConditionType a, ChallengeConditionType b)
        {
            if (_database?.Traits == null)
                return false;

            bool any = false;
            for (int i = 0; i < _database.Traits.Length; i++)
            {
                TraitData trait = _database.Traits[i];
                if (trait == null)
                    continue;

                if (trait.IsUnlockedByDefault || trait.UnlockConditionTarget <= 0f)
                    continue;

                if (trait.UnlockConditionType != a && trait.UnlockConditionType != b)
                    continue;

                if (IsStringInArray(save.UnlockedTraitIds, trait.Id))
                    continue;

                float progress = SaveSystem.GetChallengeProgress(save, (int)trait.UnlockConditionType);
                if (progress < trait.UnlockConditionTarget)
                    continue;

                AddStringUnique(ref save.UnlockedTraitIds, trait.Id);
                any = true;
                HubBadgeRegistry.RegisterNew(HubBadgeType.Trait, trait.Id);

                EventBus.Publish(new UnlockNotificationEvent
                {
                    UnlockType = (int)UnlockType.Trait,
                    DisplayName = SOLocalization.GetName("trait_" + trait.LocalizationKey, trait.DisplayName),
                    Description = LocalizationManager.Get("notification_new_trait_available_body", "New trait available in Planning!")
                });
            }

            return any;
        }

        private bool CheckTurretUnlocksForConditions(SaveData save, ChallengeConditionType a, ChallengeConditionType b)
        {
            if (_database?.Turrets == null)
                return false;

            bool any = false;
            for (int i = 0; i < _database.Turrets.Length; i++)
            {
                TurretData turret = _database.Turrets[i];
                if (turret == null)
                    continue;

                if (turret.IsUnlockedByDefault || turret.UnlockConditionTarget <= 0f)
                    continue;

                if (turret.UnlockConditionType != a && turret.UnlockConditionType != b)
                    continue;

                if (IsStringInArray(save.UnlockedTurretIds, turret.Id))
                    continue;

                float progress = SaveSystem.GetChallengeProgress(save, (int)turret.UnlockConditionType);
                if (progress < turret.UnlockConditionTarget)
                    continue;

                AddStringUnique(ref save.UnlockedTurretIds, turret.Id);
                any = true;
                HubBadgeRegistry.RegisterNew(HubBadgeType.Turret, turret.Id);

                EventBus.Publish(new UnlockNotificationEvent
                {
                    UnlockType = (int)UnlockType.Turret,
                    DisplayName = SOLocalization.GetName("turret_" + turret.LocalizationKey, turret.DisplayName),
                    Description = LocalizationManager.Get("notification_new_turret_available_body", "New turret available!")
                });
            }

            return any;
        }

        private void RebuildLaserChallengeTargetCache(SaveData save)
        {
            float continuousProgress = GetProgressValue(ChallengeConditionType.ContinuousBeamSeconds);
            float laserTimeProgress = GetProgressValue(ChallengeConditionType.LaserTimeOnTarget);

            float nextContinuous = float.PositiveInfinity;
            float nextLaserTime = float.PositiveInfinity;
            bool hasPending = false;

            if (_database?.Challenges != null)
            {
                for (int i = 0; i < _database.Challenges.Length; i++)
                {
                    ChallengeData ch = _database.Challenges[i];
                    if (ch == null || IsStringInArray(save.CompletedChallengeIds, ch.Id))
                        continue;

                    AddLaserTargetCandidate(ch.ConditionType, ch.TargetValue,
                        continuousProgress, laserTimeProgress,
                        ref nextContinuous, ref nextLaserTime, ref hasPending);
                }
            }

            if (_database?.Traits != null)
            {
                for (int i = 0; i < _database.Traits.Length; i++)
                {
                    TraitData trait = _database.Traits[i];
                    if (trait == null || trait.IsUnlockedByDefault || trait.UnlockConditionTarget <= 0f)
                        continue;

                    if (IsStringInArray(save.UnlockedTraitIds, trait.Id))
                        continue;

                    AddLaserTargetCandidate(trait.UnlockConditionType, trait.UnlockConditionTarget,
                        continuousProgress, laserTimeProgress,
                        ref nextContinuous, ref nextLaserTime, ref hasPending);
                }
            }

            if (_database?.Turrets != null)
            {
                for (int i = 0; i < _database.Turrets.Length; i++)
                {
                    TurretData turret = _database.Turrets[i];
                    if (turret == null || turret.IsUnlockedByDefault || turret.UnlockConditionTarget <= 0f)
                        continue;

                    if (IsStringInArray(save.UnlockedTurretIds, turret.Id))
                        continue;

                    AddLaserTargetCandidate(turret.UnlockConditionType, turret.UnlockConditionTarget,
                        continuousProgress, laserTimeProgress,
                        ref nextContinuous, ref nextLaserTime, ref hasPending);
                }
            }

            _hasPendingLaserChallengeChecks = hasPending;
            _nextContinuousBeamChallengeTarget = nextContinuous;
            _nextLaserTimeChallengeTarget = nextLaserTime;
        }

        private static void AddLaserTargetCandidate(
            ChallengeConditionType type,
            float target,
            float continuousProgress,
            float laserTimeProgress,
            ref float nextContinuous,
            ref float nextLaserTime,
            ref bool hasPending)
        {
            if (target <= 0f)
                return;

            if (type == ChallengeConditionType.ContinuousBeamSeconds)
            {
                hasPending = true;
                if (target <= continuousProgress)
                    nextContinuous = Mathf.Min(nextContinuous, continuousProgress);
                else
                    nextContinuous = Mathf.Min(nextContinuous, target);
            }
            else if (type == ChallengeConditionType.LaserTimeOnTarget)
            {
                hasPending = true;
                if (target <= laserTimeProgress)
                    nextLaserTime = Mathf.Min(nextLaserTime, laserTimeProgress);
                else
                    nextLaserTime = Mathf.Min(nextLaserTime, target);
            }
        }

        private static bool IsStringInArray(string[] array, string value)
        {
            if (array == null || string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == value)
                    return true;
            }

            return false;
        }

        private static void AddStringUnique(ref string[] array, string value)
        {
            if (string.IsNullOrEmpty(value) || IsStringInArray(array, value))
                return;

            int oldLength = array != null ? array.Length : 0;
            System.Array.Resize(ref array, oldLength + 1);
            array[oldLength] = value;
        }



        public void OnEliteEnemyKilled(EliteEnemyKilled evt)
        {
            AddProgress(ChallengeConditionType.KillEliteEnemies, 1);
        }

        public void OnDamageSlovedEnemy(float damage)
        {
            AddProgress(ChallengeConditionType.DamageSlovedEnemies, damage);
            // Don't CheckAll every frame — only on wave complete or periodically
        }
        public void OnSniperDamage(SniperDamageEvent evt)
        {
            AddProgress(ChallengeConditionType.SniperDamage, evt.Damage);
            CheckMiscCombatChallengesThrottled();
        }

        public void OnBurningEnemyKill(EnemyBurnedKilledEvent evt)
        {
            AddProgress(ChallengeConditionType.KillBurningEnemies, 1);
            CheckMiscCombatChallengesThrottled();
        }

        private void OnChainLightningHit(ChainLightningHitEvent evt)
        {
            AddProgress(ChallengeConditionType.ChainHits, evt.Count);

            TrackElementalistFamilyBit(TurretFamilyLightning);
            if (!_comboEffectsComplete)
                TrackComboFamilyOnEnemyBit(evt.EnemyId, TurretFamilyLightning);

            CheckChainChallengesThrottled();
        }

        public void OnChainLightningHitBatch(int count, List<int> enemyIds)
        {
            if (count <= 0)
                return;

            bool profileOuterTotal = _profileOuterChainBatchTotal;
            if (profileOuterTotal)
                Profiler.BeginSample("ChallengeTracker.ChainBatch.Total");

            Profiler.BeginSample("ChallengeTracker.ChainBatch.01.AddProgress");
            AddProgress(ChallengeConditionType.ChainHits, count);
            Profiler.EndSample();

            Profiler.BeginSample("ChallengeTracker.ChainBatch.02.Elementalist");
            TrackElementalistFamilyBit(TurretFamilyLightning);
            Profiler.EndSample();

            if (!_comboEffectsComplete && enemyIds != null && enemyIds.Count > 0)
            {
                Profiler.BeginSample("ChallengeTracker.ChainBatch.03.ComboEnemyIds");
                for (int i = 0; i < enemyIds.Count; i++)
                    TrackComboFamilyOnEnemyBit(enemyIds[i], TurretFamilyLightning);
                Profiler.EndSample();
            }

            Profiler.BeginSample("ChallengeTracker.ChainBatch.04.CheckThrottled");
            CheckChainChallengesThrottled();
            Profiler.EndSample();

            if (profileOuterTotal)
                Profiler.EndSample();
        }

        private void OnChainLightningHitBatch(ChainLightningHitBatchEvent evt)
        {
            // Legacy fallback path only. Normal gameplay uses the direct sink above.
            OnChainLightningHitBatch(evt.Count, evt.EnemyIds);
        }


        public void OnFreezeEnemy()
        {
            AddProgress(ChallengeConditionType.FreezeEnemiesTotal, 1);
            CheckMiscCombatChallengesThrottled();
        }
        public void OnCriticalHit(CriticalHitEvent evt)
        {
            AddProgress(ChallengeConditionType.CriticalHits, 1);
            CheckMiscCombatChallengesThrottled();
        }
        private bool CheckTraitUnlocks(SaveData save)
        {
            if (_database?.Traits == null) return false;
            var unlocked = new HashSet<string>(save.UnlockedTraitIds ?? System.Array.Empty<string>());
            bool any = false;

            foreach (var trait in _database.Traits)
            {
                if (trait.IsUnlockedByDefault || unlocked.Contains(trait.Id)) continue;
                if (trait.UnlockConditionTarget <= 0) continue;

                float progress = SaveSystem.GetChallengeProgress(save, (int)trait.UnlockConditionType);
                if (progress >= trait.UnlockConditionTarget)
                {
                    var list = new List<string>(save.UnlockedTraitIds ?? System.Array.Empty<string>());
                    list.Add(trait.Id);
                    save.UnlockedTraitIds = list.ToArray();
                    any = true;
                    HubBadgeRegistry.RegisterNew(HubBadgeType.Trait, trait.Id);

                    EventBus.Publish(new UnlockNotificationEvent
                    {
                        UnlockType = (int)UnlockType.Trait,
                        DisplayName = SOLocalization.GetName("trait_" + trait.LocalizationKey, trait.DisplayName),
                       // Description = "New trait available in Planning!"
                    });
                }
            }
            return any;
        }
        private bool CheckTurretUnlocks(SaveData save)
        {
            if (_database?.Turrets == null) return false;
            var unlocked = new HashSet<string>(save.UnlockedTurretIds ?? System.Array.Empty<string>());
            bool any = false;

            foreach (var turret in _database.Turrets)
            {
                if (turret.IsUnlockedByDefault || unlocked.Contains(turret.Id)) continue;
                if (turret.UnlockConditionTarget <= 0) continue;

                float progress = SaveSystem.GetChallengeProgress(save, (int)turret.UnlockConditionType);
                if (progress >= turret.UnlockConditionTarget)
                {
                    var list = new List<string>(save.UnlockedTurretIds ?? System.Array.Empty<string>());
                    list.Add(turret.Id);
                    save.UnlockedTurretIds = list.ToArray();
                    any = true;
                    HubBadgeRegistry.RegisterNew(HubBadgeType.Turret, turret.Id);
                    EventBus.Publish(new UnlockNotificationEvent
                    {
                        UnlockType = (int)UnlockType.Turret,
                        DisplayName = SOLocalization.GetName("turret_" + turret.LocalizationKey, turret.DisplayName) ,
                        Description = LocalizationManager.Get("notification_new_turret_available_body", "New turret available!")
                    });
                }
            }
            return any;
        }

        private void TrackElementalistFamily(string family)
        {
            TrackElementalistFamilyBit(FamilyToMaskBit(family));
        }

        private void TrackElementalistFamilyBit(int bit)
        {
            if (bit == 0 || _elementalistComplete)
                return;

            int newMask = _usedEffectFamilyMask | bit;
            if (newMask == _usedEffectFamilyMask)
                return;

            _usedEffectFamilyMask = newMask;
            SetProgress(ChallengeConditionType.UseAllEffects, CountElementBits(newMask));
        }

        private void TrackComboFamilyOnEnemy(int enemyId, string family)
        {
            TrackComboFamilyOnEnemyBit(enemyId, FamilyToMaskBit(family));
        }

        private void TrackComboFamilyOnEnemyBit(int enemyId, int bit)
        {
            if (_comboEffectsComplete || enemyId < 0 || bit == 0)
                return;

            _elementFamilyMaskByEnemy.TryGetValue(enemyId, out int mask);
            int newMask = mask | bit;
            if (newMask == mask)
                return;

            _elementFamilyMaskByEnemy[enemyId] = newMask;

            // Combo Master target should be 3:
            // same enemy affected by inferno + frost + lightning.
            SetProgress(ChallengeConditionType.ComboEffectsOnEnemy, CountElementBits(newMask));
        }

        private static int FamilyToMaskBit(string family)
        {
            return family switch
            {
                "inferno" => TurretFamilyInferno,
                "frost" => TurretFamilyFrost,
                "lightning" => TurretFamilyLightning,
                "basic" => TurretFamilyBasic,
                "laser" => TurretFamilyLaser,
                "void" => TurretFamilyVoid,
                "toxin" => TurretFamilyToxin,
                "railgun" => TurretFamilyRailgun,
                _ => 0
            };
        }

        private void CheckChainChallengesThrottled()
        {
            float now = Time.time;
            if (now < _nextChainChallengeCheckTime)
                return;

            _nextChainChallengeCheckTime = now + Mathf.Max(0.25f, _chainChallengeCheckInterval);
            Profiler.BeginSample("ChallengeTracker.ChainBatch.CheckAllChallenges");
            CheckAllChallenges();
            Profiler.EndSample();
        }

        // FIX: Same throttle pattern as CheckChainChallengesThrottled, applied to
        // OnSniperDamage / OnCriticalHit / OnBurningEnemyKill / OnFreezeEnemy, which
        // previously called CheckAllChallenges() — and its unconditional trailing
        // SaveProgress() disk write — on every single qualifying hit. Progress values
        // (AddProgress calls) are still recorded every time; only the disk-touching
        // challenge/save scan is throttled to wall-clock cadence.
        private void CheckMiscCombatChallengesThrottled()
        {
            float now = Time.time;
            if (now < _nextMiscCombatChallengeCheckTime)
                return;

            _nextMiscCombatChallengeCheckTime = now + Mathf.Max(0.25f, _miscCombatChallengeCheckInterval);
            Profiler.BeginSample("ChallengeTracker.MiscCombat.CheckAllChallenges");
            CheckAllChallenges();
            Profiler.EndSample();
        }

        private static int CountElementBits(int mask)
        {
            int count = 0;
            if ((mask & TurretFamilyInferno) != 0) count++;
            if ((mask & TurretFamilyFrost) != 0) count++;
            if ((mask & TurretFamilyLightning) != 0) count++;
            if ((mask & TurretFamilyBasic) != 0) count++;
            if ((mask & TurretFamilyLaser) != 0) count++;
            if ((mask & TurretFamilyVoid) != 0) count++;
            if ((mask & TurretFamilyToxin) != 0) count++;
            if ((mask & TurretFamilyRailgun) != 0) count++;
            return count;
        }

        private void TrackElementFamilyFromStatus(int enemyId, StatusEffectType status)
        {
            switch (status)
            {
                case StatusEffectType.Burn:
                    TrackElementalistFamilyBit(TurretFamilyInferno);
                    TrackComboFamilyOnEnemyBit(enemyId, TurretFamilyInferno);
                    break;

                case StatusEffectType.Slow:
                    TrackElementalistFamilyBit(TurretFamilyFrost);
                    TrackComboFamilyOnEnemyBit(enemyId, TurretFamilyFrost);
                    break;

                // Keep this if you also have Freeze later.
                // Frost turret family should count whether the actual CC is Slow or Freeze.
                case StatusEffectType.Freeze:
                    TrackElementalistFamilyBit(TurretFamilyFrost);
                    TrackComboFamilyOnEnemyBit(enemyId, TurretFamilyFrost);
                    break;

                case StatusEffectType.ArmorBreak:
                    TrackElementalistFamilyBit(TurretFamilyBasic);
                    TrackComboFamilyOnEnemyBit(enemyId, TurretFamilyBasic);
                    break;

                case StatusEffectType.HPPercentReduce:
                    TrackElementalistFamilyBit(TurretFamilyLaser);
                    TrackComboFamilyOnEnemyBit(enemyId, TurretFamilyLaser);
                    break;

                case StatusEffectType.Weaken:
                    TrackElementalistFamilyBit(TurretFamilyVoid);
                    TrackComboFamilyOnEnemyBit(enemyId, TurretFamilyVoid);
                    break;

                case StatusEffectType.Poison:
                    TrackElementalistFamilyBit(TurretFamilyToxin);
                    TrackComboFamilyOnEnemyBit(enemyId, TurretFamilyToxin);
                    break;

                case StatusEffectType.Expose:
                    TrackElementalistFamilyBit(TurretFamilyRailgun);
                    TrackComboFamilyOnEnemyBit(enemyId, TurretFamilyRailgun);
                    break;
            }
        }




        private void OnGameOver(GameOverEvent evt)
        {
            FlushPendingDamageProgress();
            FlushProjectileChallengeCheck();
            // FIX: game-over is the final save of the run — must never be silently
            // skipped by the global throttle, regardless of how recently the last
            // check ran.
            CheckAllChallenges(forceImmediate: true);
            // Final save on game over to capture last-moment progress
            SaveProgress();
        }

        private void OnDestroy()
        {
            FlushProjectileChallengeCheck();
            SaveProgress(); // safety save on destroy
            ChainLightningHitBatcher.WantsEnemyIds = true;

            EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            EventBus.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
            EventBus.Unsubscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Unsubscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Unsubscribe<EnemyReachedEndEvent>(OnEnemyReachedEnd);
            EventBus.Unsubscribe<EnemyStatusAppliedEvent>(OnStatusApplied);
            EventBus.Unsubscribe<EnemyStatusExpiredEvent>(OnStatusExpired);   // NEW — see note below
            EventBus.Unsubscribe<ChainLightningFiredEvent>(OnChainFired);     // NEW
            EventBus.Unsubscribe<LaserHitEvent>(OnLaserHit);                  // NEW
            EventBus.Unsubscribe<PercentHPDamageEvent>(OnPercentHPDamage);
            EventBus.Unsubscribe<StealthEnemyRevealedEvent>(OnStealthEnemyRevealed);
            EventBus.Unsubscribe<EnemyBurnedKilledEvent>(OnBurningEnemyKill);
            EventBus.Unsubscribe<ProjectileSpawnedEvent>(OnProjectileFired);
            EventBus.Unsubscribe<ProjectileSpawnedBatchEvent>(OnProjectileFiredBatch);
            EventBus.Unsubscribe<BuffTurretsEvent>(OnBuffSimultaneous);
            EventBus.Unsubscribe<ChainLightningHitEvent>(OnChainLightningHit);
            EventBus.Unsubscribe<ChainLightningHitBatchEvent>(OnChainLightningHitBatch);
            ServiceLocator.Unregister<IChainLightningHitTelemetrySink>();
            ServiceLocator.Unregister<ILaserHitTelemetrySink>();
            ChainLightningHitBatcher.InvalidateTelemetrySink();
            EventBus.Unsubscribe<CriticalHitEvent>(OnCriticalHit);
            EventBus.Unsubscribe<EliteEnemyKilled>(OnEliteEnemyKilled);
            EventBus.Unsubscribe<SniperDamageEvent>(OnSniperDamage);
            ServiceLocator.Unregister<IProjectileSpawnTelemetrySink>();
            ServiceLocator.Unregister<ChallengeTracker>();
        }


#if UNITY_EDITOR
        public void DebugAddProgress(ChallengeConditionType type, float amount)
        {
            AddProgress(type, amount);
            CheckAllChallenges();
            SaveProgress();
        }

        public void DebugSetProgress(ChallengeConditionType type, float value)
        {
            SetProgress(type, value);
            CheckAllChallenges();
            SaveProgress();
        }

        public void DebugSaveProgress()
        {
            SaveProgress();
            Debug.Log("[ChallengeTracker] Debug saved current challenge progress.", this);
        }

        public void DebugClearChallengeProgressAndCompletions()
        {
            _progress.Clear();
            _usedTurretTypes.Clear();
            _usedEffects.Clear();
            _usedEffectFamilyMask = 0;
            _comboEffectsComplete = false;
            _elementalistComplete = false;
            ChainLightningHitBatcher.WantsEnemyIds = true;

            _turretCount = 0;
            _usedSpell = false;
            _usedSupportTurret = false;
            _hadLeak = false;

            _killSpreeTimer = 0f;
            _killSpreeCount = 0;
            _simultaneousBuffCount = 0;
            _runStartTime = Time.time;

            _currentBurningCount = 0;
            _maxChainHitsPerEvent = 0;
            _laserTimeAccumulated = 0f;

            _elementFamilyMaskByEnemy.Clear();
            _usedEffectFamilyMask = 0;
            _comboEffectsComplete = false;
            _elementalistComplete = false;
            _nextChainChallengeCheckTime = 0f;
            _nextLaserChallengeCheckTime = 0f;
            _nextMiscCombatChallengeCheckTime = 0f;
            _nextGlobalChallengeCheckTime = 0f;
            _hasPendingLaserChallengeChecks = true;
            _nextContinuousBeamChallengeTarget = float.PositiveInfinity;
            _nextLaserTimeChallengeTarget = float.PositiveInfinity;


            var save = SaveSystem.Load();

            save.ChallengeProgressTypes = System.Array.Empty<int>();
            save.ChallengeProgressValues = System.Array.Empty<float>();
            save.CompletedChallengeIds = System.Array.Empty<string>();

            SaveSystem.Save(save);

            Debug.Log("[ChallengeTracker] Debug cleared challenge progress and completed challenges.", this);
        }
#endif

    }
}

