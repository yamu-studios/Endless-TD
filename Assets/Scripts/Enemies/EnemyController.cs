// ============================================================================
// ETD.Enemies - EnemyController.cs  [UPDATED]
// Fix: Sub-step movement Ã¢ÂÂ enemies consume full movement budget per frame,
//      correctly crossing multiple waypoints without overshoot or jitter.
//      Works correctly at any time scale (1x, 2x, 3x).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;

namespace ETD.Enemies
{
    public class EnemyController : MonoBehaviour
    {
        [Header("Runtime Ã¢ÂÂ Set by spawner")]
        public int InstanceId;
        public EnemyData Data;
        public EnemyTier Tier;
        public Transform hitTransform;
        public int WaveNumber;

        // Stats
        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public float MoveSpeed { get; private set; }
        public float BaseMoveSpeed { get; private set; }
        public float Armor { get; private set; }
        public bool IsStealth { get; set; }
        public bool IsRevealed { get; set; }
        public bool IsDead { get; private set; }

        // Tracks the most recent hit regardless of source (direct/projectile, laser
        // tick, chain lightning, burn/DoT tick, evolved area splash). Death Explosion
        // reads this at the moment of death so it can proc off ANY kill type, not just
        // primary projectile hits.
        public float LastHitDamage { get; private set; }
        public int LastHitSourceTurretType { get; private set; } = -1;
        public int LastHitSourceTurretId { get; private set; } = -1;

        public EnemyData splitChildData;

        // Path
        private List<Vector3> _worldPath = new();
        private int _pathIndex;
        private Grid.GridSystem _grid;

        // Cached from Data.Type on Initialize: flying enemies use a straight
        // entry -> exit path and ignore walking-route recalculations.
        private bool _isFlying;

        // Remaining path distance (world units) from each waypoint to the exit.
        // Precomputed on path assignment so DistanceToExit stays O(1) per query.
        private readonly List<float> _remainingToExit = new();

        // Centralized enemy modifiers/statuses.
        private readonly EnemyModifierStack _modifiers = new();
        private int _radarRevealCount;

        // Latches once this enemy has ever been radar-revealed, so
        // StealthEnemyRevealedEvent fires once per enemy rather than per re-entry
        // into radar range. Reset in Initialize because enemies are pooled.
        private bool _hasBeenRadarRevealed;

        // Radar Path A "Fire Control Array" mark. Unlike the reveal refcount above this
        // is a timed value rather than an enter/exit refcount: Tier 2 lets the mark
        // linger after the enemy leaves radar range, and a timer expresses that without
        // a second bookkeeping list on the turret. Strongest radar wins (values are
        // max'd on refresh), so stacking radars on one corner does not stack the amp.
        private float _radarMarkTimer;
        private float _radarMarkAmp;
        private float _radarMarkArmorShred;

        public bool IsRadarMarked => _radarMarkTimer > 0f;

        private static IDamageStatsSink _damageStatsSink;
        private static bool _damageStatsSinkResolved;

        // Type-specific
        private float _berserkMaxSpeedMult;
        private bool _hasSplit;
        private int _splitCount;

        // VFX
        private EnemyVFXConfig _vfxConfig;
        private EnemyStatusVFX _statusVFX;

        // Hot-path cache. EnemyManager drives movement/status updates centrally,
        // so every active enemy no longer pays a separate Unity Update callback.
        private Transform _cachedTransform;
        private float _cachedYOffset;

        [Header("Crowd Control")]
        [Tooltip("Fallback per-enemy freeze immunity (seconds) applied after a freeze ends " +
                 "when the freeze source does not specify its own. Prevents permanent " +
                 "freeze-lock from fast re-application.")]
        [SerializeField] private float _defaultFreezeImmunity = BalanceConstants.DefaultFreezeImmunity;

        [Tooltip("While an enemy is freeze-immune, an incoming freeze is downgraded to a " +
                 "slow of this strength for its duration, so Frost still contributes.")]
        [SerializeField, Range(0f, 1f)] private float _freezeLockoutSlow = BalanceConstants.FreezeLockoutSlow;

        // Time (Time.time) until which this enemy cannot be re-frozen.
        private float _freezeImmuneUntil;

        public System.Action<EnemyController> OnDeath;
        public System.Action<EnemyController> OnReachedEnd;

        private static int _nextRuntimeId = 1;

        public int RuntimeId { get; private set; }

        private List<Vector2Int> _currentPath = new();

        private void Awake()
        {
            _cachedTransform = transform;
            _vfxConfig = GetComponent<EnemyVFXConfig>();
            _statusVFX = GetComponent<EnemyStatusVFX>();
        }

        // =================================================================
        // INITIALIZE
        // =================================================================

        public void Initialize(EnemyData data, EnemyTier tier, int waveNumber,
            List<Vector2Int> gridPath, Grid.GridSystem grid, float spawnOffset = 0f,bool hasPos = false,Vector3? fixedPos = null)
        {
            _cachedTransform ??= transform;

            Data = data;
            Tier = tier;
            WaveNumber = waveNumber;
            _grid = grid;
            MaxHealth = data.GetScaledHealth(waveNumber, tier);
            CurrentHealth = MaxHealth;
            BaseMoveSpeed = data.GetScaledSpeed(waveNumber);
            MoveSpeed = BaseMoveSpeed;
            Armor = data.Armor;
            _cachedYOffset = data.yOffset;
            IsStealth = data.Type == EnemyType.Stealth;
            _radarRevealCount = 0;
            IsRevealed = false;
            _hasBeenRadarRevealed = false; // enemies are pooled; clear the latch on reuse
            _radarMarkTimer = 0f;
            _radarMarkAmp = 0f;
            _radarMarkArmorShred = 0f;
            IsDead = false;
            _hasSplit = false;
            _splitCount = data.SplitCount;
            _berserkMaxSpeedMult = data.BerserkMaxSpeedMultiplier;
            RuntimeId = _nextRuntimeId++;

            _isFlying = data.Type == EnemyType.Flying;

            _worldPath.Clear();
            for (int i = 0; i < gridPath.Count; i++)
                _worldPath.Add(grid.GridToWorld(gridPath[i]));

            // Flying enemies ignore the walking route: collapse the path to a single
            // entry -> exit leg so Move() flies them straight across the map. Done
            // before BuildRemainingToExit so "first/last" turret targeting priority
            // measures the real (much shorter) remaining distance.
            if (_isFlying && _worldPath.Count > 2)
            {
                Vector3 exit = _worldPath[_worldPath.Count - 1];
                _worldPath.RemoveRange(1, _worldPath.Count - 2);
                _worldPath[1] = exit;
            }

            BuildRemainingToExit();

            _pathIndex = 0;
            _modifiers.Clear();
            _freezeImmuneUntil = 0f;
            _statusVFX?.ClearAll();

            // Stagger spawn position behind entry
            if (_worldPath.Count >= 2)
            {
                Vector3 entry = _worldPath[0];
                Vector3 second = _worldPath[1];
                Vector3 entryDir = (second - entry).normalized;
                float lateral = Random.Range(-0.25f, 0.25f);
                Vector3 right = Vector3.Cross(entryDir, Vector3.up).normalized;
                float behind = 0.5f + spawnOffset;
                Vector3 newPos = entry - entryDir * behind + right * lateral;
                newPos.y = data.yOffset;
                _cachedTransform.position = newPos;
                if (!hasPos)
                    return;
                _cachedTransform.position = fixedPos ?? Vector3.zero;
            }
            else
            {
                _cachedTransform.position = _worldPath.Count > 0 ? _worldPath[0] : Vector3.zero;
                if (!hasPos)
                    return;
                _cachedTransform.position = fixedPos ?? Vector3.zero;
            }

         
        }

        // =================================================================
        // UPDATE PATH (called when path recalculates)
        // =================================================================

        public void UpdatePath(List<Vector2Int> newGridPath)
        {
            if (newGridPath == null || newGridPath.Count == 0) return;

            // Flying enemies are not on the walking route, so a turret-induced path
            // recalculation must not drag them back onto it mid-flight.
            if (_isFlying) return;

            // FIX (memory-leak #1b): Reuse the existing _worldPath list instead of
            // allocating a new List<Vector3> for every enemy on every path-recalc event.
            // At wave 100+ with 80–120 enemies alive, the old code produced 80–120
            // List<Vector3> allocations per turret placement/sale, driving the observed
            // 3 GB memory growth and GC freeze spikes.
            _worldPath.Clear();
            for (int i = 0; i < newGridPath.Count; i++)
                _worldPath.Add(_grid.GridToWorld(newGridPath[i]));

            BuildRemainingToExit();

            Vector3 currentPos = (_cachedTransform != null ? _cachedTransform : transform).position;
            int bestIndex = 0;
            float bestDist = float.MaxValue;

            for (int i = 0; i < _worldPath.Count; i++)
            {
                float d = Vector3.SqrMagnitude(_worldPath[i] - currentPos);
                if (d < bestDist) { bestDist = d; bestIndex = i; }
            }

            if (bestIndex < _worldPath.Count - 1)
            {
                Vector3 segStart = _worldPath[bestIndex];
                Vector3 segEnd = _worldPath[bestIndex + 1];
                Vector3 segDir = segEnd - segStart;
                if (segDir.sqrMagnitude > 0.001f)
                {
                    float t = Vector3.Dot(currentPos - segStart, segDir) / segDir.sqrMagnitude;
                    if (t > 0.5f)
                        bestIndex = Mathf.Min(bestIndex + 1, _worldPath.Count - 1);
                }
            }

            _pathIndex = bestIndex;
        }

        // =================================================================
        // PATH PROGRESS & TARGETABILITY (used by turret targeting)
        // =================================================================

        // remaining[i] = world distance from waypoint i to the exit along the path.
        private void BuildRemainingToExit()
        {
            _remainingToExit.Clear();
            int count = _worldPath.Count;
            if (count == 0) return;

            for (int i = 0; i < count; i++)
                _remainingToExit.Add(0f);

            for (int i = count - 2; i >= 0; i--)
            {
                Vector3 a = _worldPath[i];
                Vector3 b = _worldPath[i + 1];
                float dx = b.x - a.x, dz = b.z - a.z;
                _remainingToExit[i] = _remainingToExit[i + 1] + Mathf.Sqrt((dx * dx) + (dz * dz));
            }
        }

        /// <summary>
        /// Approximate world distance still to travel to the exit. Lower = closer to
        /// the exit ("First"); higher = further from it ("Last"). Works across paths of
        /// different lengths (e.g. splitter children), unlike raw index or spawn order.
        /// </summary>
        public float DistanceToExit
        {
            get
            {
                int count = _worldPath.Count;
                if (count == 0) return 0f;

                int idx = _pathIndex;
                if (idx >= count) return 0f;   // already at/after the exit
                if (idx < 0) idx = 0;

                Vector3 pos = (_cachedTransform != null ? _cachedTransform : transform).position;
                Vector3 wp = _worldPath[idx];
                float dx = wp.x - pos.x, dz = wp.z - pos.z;
                float toWaypoint = Mathf.Sqrt((dx * dx) + (dz * dz));
                float after = idx < _remainingToExit.Count ? _remainingToExit[idx] : 0f;
                return toWaypoint + after;
            }
        }

        // =================================================================
        // UPDATE
        // =================================================================

        internal void ManagedUpdate(float deltaTime)
        {
            if (IsDead || _worldPath.Count == 0) return;

            UpdateStatusEffects(deltaTime);
            UpdateRadarMark(deltaTime);
            Move(deltaTime);

            if (Data != null && Data.Type == EnemyType.Regenerator && Data.RegenPercentPerSecond > 0f
                && CurrentHealth < MaxHealth)
            {
                CurrentHealth = Mathf.Min(MaxHealth,
                    CurrentHealth + MaxHealth * Data.RegenPercentPerSecond * deltaTime);
            }
        }

        // =================================================================
        // MOVE Ã¢ÂÂ sub-step approach: no overshoot at any speed multiplier
        // =================================================================

        private void Move(float deltaTime)
        {
            Transform tr = _cachedTransform != null ? _cachedTransform : transform;
            float budget = GetEffectiveSpeed() * Mathf.Max(0f, deltaTime);

            while (budget > 0.0001f)
            {
                if (_pathIndex >= _worldPath.Count)
                {
                    ReachEnd();
                    return;
                }

                Vector3 target = _worldPath[_pathIndex];
                Vector3 currentPosition = tr.position;
                Vector3 dir = target - currentPosition;
                dir.y = 0f;
                float dist = dir.magnitude;
                target.y = _cachedYOffset;
                if (dist < 0.001f)
                {
                    // Already at waypoint Ã¢ÂÂ snap and advance
                    tr.position = target;
                    _pathIndex++;
                    continue;
                }

                Vector3 normalized = dir / dist;

                if (dist <= budget)
                {
                    // Can reach this waypoint within budget Ã¢ÂÂ arrive exactly
                    target.y = _cachedYOffset;
                    tr.position = target;
                    budget -= dist;
                    _pathIndex++;
                    tr.forward = normalized;
                }
                else
                {
                    // Move as far as budget allows
                    tr.position = currentPosition + normalized * budget;
                    tr.forward = normalized;
                    budget = 0f;
                }
            }
        }

        private float GetEffectiveSpeed()
        {
            float speed = MoveSpeed;

            if (Data.Type == EnemyType.Berserk)
            {
                float hpRatio = 1f - (CurrentHealth / MaxHealth);
                speed *= Mathf.Lerp(1f, _berserkMaxSpeedMult, hpRatio);
            }

            speed *= _modifiers.GetMoveSpeedMultiplier();

            return Mathf.Max(speed, 0f);
        }

        // =================================================================
        // DAMAGE & STATUS
        // =================================================================

        public void TakeDamage(float damage, StatusEffectType statusType = StatusEffectType.None,
            float statusValue = 0f, float statusDuration = 0f, bool playHitVFX = true,
            bool isCritical = false, DamageNumberKind damageKind = DamageNumberKind.Normal,
            bool showDamageNumber = true, int sourceTurretType = -1, int sourceTurretId = -1,
            float bonusArmorPierce = 0f)
        {
            if (IsDead)
                return;

            float effectiveDamage = Mathf.Max(ApplyMitigation(damage, sourceTurretType, bonusArmorPierce),
                BalanceConstants.MinDamagePerHit);
            CurrentHealth -= effectiveDamage;
            LastHitDamage = effectiveDamage;
            LastHitSourceTurretType = sourceTurretType;
            LastHitSourceTurretId = sourceTurretId;
            ReportDamageStats(effectiveDamage, damageKind, sourceTurretType, sourceTurretId);

            if (showDamageNumber)
                PublishDamageNumber(effectiveDamage, damageKind, isCritical, CurrentHealth <= 0f);

            if (playHitVFX)
                _vfxConfig?.FlashOnHit();

            if (statusType != StatusEffectType.None && statusDuration > 0f)
                ApplyStatus(statusType, statusValue, statusDuration, sourceTurretType, sourceTurretId);

            ResolveDeathAfterDamage();
        }

        public void TakePureDamage(float damage, bool playHitVFX = true,
            bool isCritical = false, bool showDamageNumber = true, int sourceTurretType = -1, int sourceTurretId = -1)
        {
            if (IsDead)
                return;

            float effectiveDamage = Mathf.Max(0f, damage);
            CurrentHealth -= effectiveDamage;
            LastHitDamage = effectiveDamage;
            LastHitSourceTurretType = sourceTurretType;
            LastHitSourceTurretId = sourceTurretId;
            ReportDamageStats(effectiveDamage, DamageNumberKind.Pure, sourceTurretType, sourceTurretId);

            if (showDamageNumber)
                PublishDamageNumber(effectiveDamage, DamageNumberKind.Pure, isCritical, CurrentHealth <= 0f);

            if (playHitVFX)
                _vfxConfig?.FlashOnHit();

            ResolveDeathAfterDamage();
        }

        /// <summary>
        /// Entropy Engine decay. Removes a fraction of CURRENT health, but is strictly
        /// NON-LETHAL: it never reduces the enemy below the floor
        /// (max(1, minHpFloorFraction * MaxHealth)). Intentionally spawns NO floating
        /// damage number — this ticks every frame and would otherwise spam the screen —
        /// and never triggers death. The caller still counts the removed HP toward
        /// percent-HP damage tracking. Returns the HP actually removed this call.
        /// </summary>
        public float ApplyNonLethalDecay(float fraction, float minHpFloorFraction)
        {
            if (IsDead || fraction <= 0f)
                return 0f;

            float floor = Mathf.Max(1f, MaxHealth * Mathf.Max(0f, minHpFloorFraction));
            if (CurrentHealth <= floor)
                return 0f;

            float damage = CurrentHealth * fraction;
            float newHealth = CurrentHealth - damage;
            if (newHealth < floor)
            {
                damage = CurrentHealth - floor;
                newHealth = floor;
            }

            CurrentHealth = newHealth;
            return damage;
        }

        /// <summary>
        /// Fast chain-lightning damage path. It applies the normal armor-reduced hit
        /// and optional current-HP percent damage in one enemy state transition, so a
        /// secondary chain target does not pay for two hit VFX/death checks.
        /// </summary>
        public void TakeChainDamage(float damage, float remainingHpPercentDamage, bool playHitVFX = false,
            bool isCritical = false, bool showDamageNumber = true, int sourceTurretType = -1, int sourceTurretId = -1)
        {
            if (IsDead)
                return;

            float effectiveDamage = Mathf.Max(ApplyMitigation(damage, sourceTurretType),
                BalanceConstants.MinDamagePerHit) * GetExposureMultiplier();
            CurrentHealth -= effectiveDamage;

            if (CurrentHealth > 0f && remainingHpPercentDamage > 0f)
            {
                float percentDamage = CurrentHealth * Mathf.Max(0f, remainingHpPercentDamage);
                CurrentHealth -= percentDamage;
                effectiveDamage += percentDamage;

                // FIX: report the percent-HP portion so the "percent-HP damage"
                // tracker advances. Previously only the Entropy Engine trait's own
                // effect published this event, so its unlock (Deal 50,000 percent-HP
                // damage) could never progress from turret sources like Volt Reaper.
                if (percentDamage > 0f)
                    EventBus.Publish(new PercentHPDamageEvent { DamageAmount = percentDamage });
            }

            LastHitDamage = effectiveDamage;
            LastHitSourceTurretType = sourceTurretType;
            LastHitSourceTurretId = sourceTurretId;
            ReportDamageStats(effectiveDamage, DamageNumberKind.Chain, sourceTurretType, sourceTurretId);

            if (showDamageNumber)
                PublishDamageNumber(effectiveDamage, DamageNumberKind.Chain, isCritical, CurrentHealth <= 0f);

            if (playHitVFX)
                _vfxConfig?.FlashOnHit();

            ResolveDeathAfterDamage();
        }


        private void ReportDamageStats(float amount, DamageNumberKind kind, int sourceTurretType, int sourceTurretId)
        {
            if (amount <= 0f || sourceTurretType < 0)
                return;

            if (!_damageStatsSinkResolved)
            {
                ServiceLocator.TryGet(out _damageStatsSink);
                _damageStatsSinkResolved = true;
            }

            _damageStatsSink?.OnDamageDealt(sourceTurretType, sourceTurretId, (int)kind, amount);
        }

        public static void InvalidateDamageStatsSink()
        {
            _damageStatsSink = null;
            _damageStatsSinkResolved = false;
        }

        private void PublishDamageNumber(float amount, DamageNumberKind kind, bool isCritical, bool isKillingBlow)
        {
            if (amount <= 0f)
                return;

            Transform anchor = hitTransform != null ? hitTransform : transform;
            Vector3 position = anchor.position;
            if (hitTransform == null)
                position.y += 0.85f;

            EventBus.Publish(new EnemyDamagedEvent
            {
                EnemyId = InstanceId,
                RuntimeId = RuntimeId,
                Amount = amount,
                WorldPosition = position,
                IsCritical = isCritical,
                IsKillingBlow = isKillingBlow,
                DamageKind = (int)(isCritical ? DamageNumberKind.Critical : kind)
            });
        }

        private void ResolveDeathAfterDamage()
        {
            if (CurrentHealth > 0f)
                return;

            if (Data.Type == EnemyType.Splitter && !_hasSplit)
            {
                // Spawn the children, then fall through to Die() below — Die() publishes
                // the one real EnemyKilledEvent (gold/XP). The old code also published a
                // bare EnemyKilledEvent here AND restored health that Die() immediately
                // discarded, so every splitter death was counted twice.
                _hasSplit = true;

                for (int i = 0; i < _splitCount; i++)
                {
                    EnemyManager.Instance.SpawnEnemy(
                        splitChildData,
                        EnemyTier.Normal,
                        WaveNumber,
                        0,
                        true,
                        transform.position + transform.forward * -0.5f * i);
                }
            }

            Die();
        }

        public void ApplyStatus(StatusEffectType type, float value, float duration,
            int sourceTurretType = -1, int sourceTurretId = -1, float freezeImmunitySeconds = -1f,
            int maxBurnStacks = -1)
        {
            if (type == StatusEffectType.Burn)
            {
                // Burn stacks per applying hit (capped); -1 uses the default cap.
                _modifiers.ApplyBurnStack(value, duration, maxBurnStacks,
                    OnStatusApplied, sourceTurretType, sourceTurretId);
                return;
            }

            if (type == StatusEffectType.Freeze)
            {
                // Per-enemy freeze immunity: prevents permanent freeze-lock when high
                // attack speed re-applies a short freeze every hit. While immune, the
                // freeze is downgraded to a slow so Frost still contributes meaningfully.
                if (Time.time < _freezeImmuneUntil)
                {
                    _modifiers.ApplyStatus(StatusEffectType.Slow, _freezeLockoutSlow, duration,
                        OnStatusApplied, sourceTurretType, sourceTurretId);
                    return;
                }

                float immunity = freezeImmunitySeconds >= 0f ? freezeImmunitySeconds : _defaultFreezeImmunity;
                _freezeImmuneUntil = Time.time + Mathf.Max(0f, duration) + Mathf.Max(0f, immunity);
            }

            _modifiers.ApplyStatus(type, value, duration, OnStatusApplied, sourceTurretType, sourceTurretId);
        }

        public void ApplySpeedMultiplier(string sourceId, float multiplier, float duration)
        {
            _modifiers.ApplyMoveSpeedMultiplier(sourceId, multiplier, duration);
        }

        /// <summary>
        /// Change-driven slow supplied by SupportAuraSystem. This is deliberately
        /// separate from timed projectile/status slows: a support aura remains
        /// active until the enemy leaves every debuffer radius, so it does not need
        /// to be re-applied every refresh cycle.
        /// </summary>
        public bool SetSupportAuraSlow(float slow, float appliedDurationForTelemetry = 0f)
        {
            bool hadSlow = _modifiers.HasStatus(StatusEffectType.Slow);
            if (!_modifiers.SetSupportAuraSlow(slow))
                return false;

            bool hasSlow = _modifiers.HasStatus(StatusEffectType.Slow);
            if (!hadSlow && hasSlow)
                OnStatusApplied(StatusEffectType.Slow, Mathf.Max(0f, appliedDurationForTelemetry));
            else if (hadSlow && !hasSlow)
                OnStatusExpired(StatusEffectType.Slow);

            return true;
        }

        private void OnStatusApplied(StatusEffectType type, float duration)
        {
            _statusVFX?.OnStatusApplied(type);
            EventBus.Publish(new EnemyStatusAppliedEvent
            {
                EnemyId = InstanceId,
                StatusType = (int)type,
                Duration = duration
            });
        }

        private void UpdateStatusEffects(float deltaTime)
        {
            if (!_modifiers.HasTimedModifiers)
                return;

            _modifiers.Update(deltaTime, ApplyBurnDamage, OnStatusExpired);
        }

        /// <summary>
        /// Elemental Exposure (Elemental Relay card): while this enemy is inside a
        /// Support debuff aura, burn ticks and chain damage are amplified. Capped at
        /// +25% by RunStatModifiers. Cheap: one dictionary TryGet per elemental hit.
        /// </summary>
        private float GetExposureMultiplier()
        {
            if (!_modifiers.HasSupportAuraSlow)
                return 1f;
            if (!ServiceLocator.TryGet<IRunStatModifiers>(out var mods))
                return 1f;
            return 1f + mods.GetSupportExposure();
        }

        /// <summary>
        /// Turret-type affinity resistance/weakness this enemy has against the given
        /// source turret type. Positive = damage reduction, negative = bonus damage
        /// taken (weakness). Linear scan is fine — Affinities is a handful of entries
        /// at most, set once per enemy in the inspector.
        /// </summary>
        private float GetAffinityResistance(int sourceTurretType)
        {
            var affinities = Data != null ? Data.Affinities : null;
            if (affinities == null || sourceTurretType < 0)
                return 0f;

            for (int i = 0; i < affinities.Length; i++)
            {
                if ((int)affinities[i].Type == sourceTurretType)
                    return affinities[i].ResistancePercent;
            }

            return 0f;
        }

        /// <summary>
        /// v1.0 mitigation pipeline: base damage -> turret-type affinity resistance ->
        /// Armor reduction. Each layer is independently reduced by its own pierce stat
        /// (AffinityPierce / ArmorPierce) before being applied, so armor-piercing
        /// content bypasses Armor without touching affinity and vice versa. Weakness
        /// (negative affinity) is never pierced away since piercing only reduces
        /// resistance, not amplify weakness. Does not touch pure/true damage sources
        /// (TakePureDamage, ApplyNonLethalDecay) — those already bypass mitigation by
        /// design.
        /// </summary>
        /// <param name="bonusArmorPierce">
        /// Per-source armor pierce (currently from the firing turret's dynamic tile),
        /// stacking additively with the run-wide pierce from IRunStatModifiers.
        /// </param>
        private float ApplyMitigation(float rawDamage, int sourceTurretType, float bonusArmorPierce = 0f)
        {
            if (rawDamage <= 0f)
                return rawDamage;

            float affinity = GetAffinityResistance(sourceTurretType);
            float armor = Armor;

            // Void turret's Weaken status: a generic timed value (like Slow/Freeze,
            // no dedicated tick system needed) that directly corrodes effective Armor.
            if (_modifiers.TryGetStatusValue(StatusEffectType.Weaken, out float weaken))
                armor = Mathf.Max(0f, armor - weaken);

            // Radar Tier 2 armor shred. Multiplicative on the remaining armor and applied
            // before the flat pierce subtractions below, so shred and pierce compose
            // instead of one making the other redundant.
            if (_radarMarkTimer > 0f && _radarMarkArmorShred > 0f)
                armor *= 1f - _radarMarkArmorShred;

            if (ServiceLocator.TryGet<IRunStatModifiers>(out var mods))
            {
                if (affinity > 0f)
                    affinity = Mathf.Max(0f, affinity - mods.GetAffinityPierce());
                armor = Mathf.Max(0f, armor - mods.GetArmorPierce());
            }

            if (bonusArmorPierce > 0f)
                armor = Mathf.Max(0f, armor - bonusArmorPierce);

            affinity = Mathf.Clamp(affinity,
                BalanceConstants.MaxAffinityWeakness, BalanceConstants.MaxAffinityResistance);
            armor = Mathf.Clamp(armor, 0f, BalanceConstants.MaxArmorMitigation);

            // Railgun's Expose status: amplifies all mitigated damage taken. Applied
            // last, after affinity/armor reduction, so it scales the target's
            // remaining effective HP rather than being cancelled out by resistance.
            float expose = _modifiers.TryGetStatusValue(StatusEffectType.Expose, out float exposeValue)
                ? Mathf.Max(0f, exposeValue) : 0f;

            // Radar Path A mark shares the Expose slot additively rather than as its own
            // multiplier: a railgun+radar corner should amplify strongly, not explosively.
            if (_radarMarkTimer > 0f)
                expose += _radarMarkAmp;

            return rawDamage * (1f - affinity) * (1f - armor) * (1f + expose);
        }

        private void ApplyBurnDamage(float damage, int sourceTurretType, int sourceTurretId)
        {
            if (IsDead || damage <= 0f)
                return;

            damage *= GetExposureMultiplier();
            CurrentHealth -= damage;
            LastHitDamage = damage;
            LastHitSourceTurretType = sourceTurretType;
            LastHitSourceTurretId = sourceTurretId;
            ReportDamageStats(damage, DamageNumberKind.Burn, sourceTurretType, sourceTurretId);
            PublishDamageNumber(damage, DamageNumberKind.Burn, false, CurrentHealth <= 0f);
            if (CurrentHealth <= 0f && !IsDead)
                Die();
        }

        private void OnStatusExpired(StatusEffectType type)
        {
            // A timed slow may expire while the persistent support aura is still
            // active. Do not remove status VFX or publish a false expiry in that case.
            if (type == StatusEffectType.Slow && _modifiers.HasStatus(StatusEffectType.Slow))
                return;

            EventBus.Publish(new EnemyStatusExpiredEvent
            {
                EnemyId = InstanceId,
                StatusType = (int)type
            });
            _statusVFX?.OnStatusRemoved(type);
        }

        // =================================================================
        // DEATH / END
        // =================================================================

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;

            bool wasBurning = HasStatus(StatusEffectType.Burn);
            if (wasBurning)
            {
                EventBus.Publish(new EnemyBurnedKilledEvent
                {
                    EnemyId = InstanceId
                });
            }

            _vfxConfig?.PlayDeathVFX();
            _vfxConfig?.StopLaserPulse();
            _statusVFX?.ClearAll();
            OnDeath?.Invoke(this);
            EventBus.Publish(new EnemyKilledEvent
            {
                EnemyId = InstanceId,
                GoldReward = ComputeGoldReward(Data, Tier, WaveNumber),
                XPReward = ComputeXPReward(Data, WaveNumber),
                Position = transform.position,
                EnemyTier = (int)Tier
            });

            TryTriggerDeathExplosion();
        }

        // Reentrancy guard: a Death Explosion splash can itself kill nearby enemies,
        // which would otherwise recursively roll their own explosions and cascade
        // through a dense pack. One proc per original kill; splash kills still count
        // as normal kills (gold/XP/events) but do not chain further explosions.
        private static bool s_resolvingDeathExplosion;
        private static readonly List<EnemyController> s_deathExplosionTargets = new(16);

        /// <summary>
        /// Death Explosion (Explosion spec card): a chance for a kill to splash damage
        /// to nearby enemies. Reads LastHit* so it procs off the ACTUAL kill type
        /// (direct/projectile hit, laser tick, chain lightning, burn/DoT tick, or an
        /// evolved area splash), not just primary turret hits.
        /// </summary>
        private void TryTriggerDeathExplosion()
        {
            if (s_resolvingDeathExplosion)
                return;

            if (LastHitSourceTurretType < 0 || LastHitDamage <= 0f)
                return;

            if (!ServiceLocator.TryGet<IRunStatModifiers>(out var mods))
                return;

            float chance = Mathf.Clamp01(mods.GetDeathExplosionChance());
            if (chance <= 0f || Random.value > chance)
                return;

            EnemyManager manager = EnemyManager.Instance;
            if (manager == null)
                return;

            const float radius = 2.25f;
            const float damagePercent = 0.8f;
            float splashDamage = LastHitDamage * damagePercent;
            if (splashDamage <= 0f)
                return;

            Vector3 center = hitTransform != null ? hitTransform.position : transform.position;
            int sourceTurretType = LastHitSourceTurretType;
            int sourceTurretId = LastHitSourceTurretId;

            s_resolvingDeathExplosion = true;
            try
            {
                manager.GetEnemiesInRange(center, radius, s_deathExplosionTargets);
                for (int i = 0; i < s_deathExplosionTargets.Count; i++)
                {
                    EnemyController enemy = s_deathExplosionTargets[i];
                    if (enemy == null || enemy == this || enemy.IsDead)
                        continue;

                    enemy.TakeDamage(splashDamage, damageKind: DamageNumberKind.Area,
                        sourceTurretType: sourceTurretType, sourceTurretId: sourceTurretId);
                }
            }
            finally
            {
                s_resolvingDeathExplosion = false;
            }
        }

        /// <summary>
        /// Kill gold scales with wave so player income stays in the same growth
        /// class as enemy HP (flat gold vs exponential HP is a guaranteed late-game
        /// wall). Elite/boss kills mirror their HP tier multipliers so big kills
        /// are big paydays. Static so UI (enemy info panel) can show the exact
        /// value a kill will pay.
        /// </summary>
        public static int ComputeGoldReward(EnemyData data, EnemyTier tier, int waveNumber)
        {
            if (data == null)
                return 0;

            float gold = data.GoldReward * (1f + waveNumber * GameConstants.GOLD_KILL_WAVE_SCALE);

            switch (tier)
            {
                case EnemyTier.Elite:
                    gold *= GameConstants.ELITE_GOLD_MULTIPLIER;
                    break;
                case EnemyTier.Boss:
                    gold *= GameConstants.BOSS_GOLD_MULTIPLIER;
                    break;
            }

            return Mathf.Max(1, Mathf.RoundToInt(gold));
        }

        /// <summary>
        /// Kill XP grows gently with wave so level-up pacing (and spec-card flow)
        /// does not stall against the per-level XP requirement growth.
        /// </summary>
        public static float ComputeXPReward(EnemyData data, int waveNumber)
        {
            if (data == null)
                return 0f;

            return data.XPReward * (1f + waveNumber * GameConstants.XP_KILL_WAVE_SCALE);
        }

        private void ReachEnd()
        {
            _statusVFX?.ClearAll();
            OnReachedEnd?.Invoke(this);
            EventBus.Publish(new EnemyReachedEndEvent
            {
                EnemyId = InstanceId,
                Damage = GetDamageToPlayer()
            });
        }

        private int GetDamageToPlayer() => Tier switch
        {
            EnemyTier.Elite => Data.DamageToPlayer * 3,
            EnemyTier.Boss => Data.DamageToPlayer * 5,
            _ => Data.DamageToPlayer
        };

        public bool HasStatus(StatusEffectType type) => _modifiers.HasStatus(type);
        public int StatusMask => _modifiers.StatusMask;
        public int BurnStackCount => _modifiers.BurnStackCount;

        public void SetSpeedModifier(float mult)
        {
            // Legacy compatibility. Prefer ApplySpeedMultiplier(sourceId, multiplier, duration).
            ApplySpeedMultiplier("legacy_speed_modifier", mult, 0.75f);
        }

        public void AddRadarReveal()
        {
            if (!IsStealth || IsDead)
                return;

            _radarRevealCount++;
            if (!IsRevealed)
            {
                IsRevealed = true;
                RevealGhostVFX();

                // Progress toward the Radar unlock (Signals Mastery). Published on the
                // first reveal only: IsRevealed drops back to false when the enemy
                // leaves radar range, so gating on it alone would count re-entries.
                if (!_hasBeenRadarRevealed)
                {
                    _hasBeenRadarRevealed = true;
                    EventBus.Publish(new StealthEnemyRevealedEvent());
                }
            }
        }

        public void RemoveRadarReveal()
        {
            if (!IsStealth)
                return;

            _radarRevealCount = Mathf.Max(0, _radarRevealCount - 1);
            IsRevealed = _radarRevealCount > 0;
        }

        /// <summary>
        /// Radar Path A mark. Refreshed every radar tick while the enemy is inside the
        /// reveal radius; <paramref name="duration"/> is the tick interval plus any Tier 2
        /// linger, so the mark simply lapses once the sweeps stop reaching this enemy.
        /// Values are max'd rather than summed — the strongest radar wins.
        /// </summary>
        public void ApplyRadarMark(float damageAmp, float armorShred, float duration)
        {
            if (IsDead || duration <= 0f)
                return;

            if (_radarMarkTimer <= 0f)
            {
                // Not currently marked: adopt this radar's values outright instead of
                // max'ing against stale ones left over from a weaker previous marker.
                _radarMarkAmp = Mathf.Max(0f, damageAmp);
                _radarMarkArmorShred = Mathf.Clamp01(armorShred);
            }
            else
            {
                _radarMarkAmp = Mathf.Max(_radarMarkAmp, Mathf.Max(0f, damageAmp));
                _radarMarkArmorShred = Mathf.Max(_radarMarkArmorShred, Mathf.Clamp01(armorShred));
            }

            _radarMarkTimer = Mathf.Max(_radarMarkTimer, duration);
        }

        private void UpdateRadarMark(float deltaTime)
        {
            if (_radarMarkTimer <= 0f)
                return;

            _radarMarkTimer -= deltaTime;
            if (_radarMarkTimer > 0f)
                return;

            _radarMarkTimer = 0f;
            _radarMarkAmp = 0f;
            _radarMarkArmorShred = 0f;
        }

        public void ClearRadarReveals()
        {
            _radarRevealCount = 0;
            if (IsStealth)
                IsRevealed = false;
        }

        public void StartLaserPulseVFX() => _vfxConfig?.StartLaserPulse();
        public void StopLaserPulseVFX() => _vfxConfig?.StopLaserPulse();
        public void RevealGhostVFX() => _vfxConfig?.RevealGhost();
    }

}