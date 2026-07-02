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

        public EnemyData splitChildData;

        // Path
        private List<Vector3> _worldPath = new();
        private int _pathIndex;
        private Grid.GridSystem _grid;

        // Centralized enemy modifiers/statuses.
        private readonly EnemyModifierStack _modifiers = new();
        private int _radarRevealCount;

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
            IsDead = false;
            _hasSplit = false;
            _splitCount = data.SplitCount;
            _berserkMaxSpeedMult = data.BerserkMaxSpeedMultiplier;
            RuntimeId = _nextRuntimeId++;

            _worldPath.Clear();
            for (int i = 0; i < gridPath.Count; i++)
                _worldPath.Add(grid.GridToWorld(gridPath[i]));

            _pathIndex = 0;
            _modifiers.Clear();
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

            // FIX (memory-leak #1b): Reuse the existing _worldPath list instead of
            // allocating a new List<Vector3> for every enemy on every path-recalc event.
            // At wave 100+ with 80–120 enemies alive, the old code produced 80–120
            // List<Vector3> allocations per turret placement/sale, driving the observed
            // 3 GB memory growth and GC freeze spikes.
            _worldPath.Clear();
            for (int i = 0; i < newGridPath.Count; i++)
                _worldPath.Add(_grid.GridToWorld(newGridPath[i]));

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
        // UPDATE
        // =================================================================

        internal void ManagedUpdate(float deltaTime)
        {
            if (IsDead || _worldPath.Count == 0) return;

            UpdateStatusEffects(deltaTime);
            Move(deltaTime);
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
            bool showDamageNumber = true, int sourceTurretType = -1, int sourceTurretId = -1)
        {
            if (IsDead)
                return;

            float effectiveDamage = Mathf.Max(damage - Armor, 1f);
            CurrentHealth -= effectiveDamage;
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
            ReportDamageStats(effectiveDamage, DamageNumberKind.Pure, sourceTurretType, sourceTurretId);

            if (showDamageNumber)
                PublishDamageNumber(effectiveDamage, DamageNumberKind.Pure, isCritical, CurrentHealth <= 0f);

            if (playHitVFX)
                _vfxConfig?.FlashOnHit();

            ResolveDeathAfterDamage();
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

            float effectiveDamage = Mathf.Max(damage - Armor, 1f);
            CurrentHealth -= effectiveDamage;

            if (CurrentHealth > 0f && remainingHpPercentDamage > 0f)
            {
                float percentDamage = CurrentHealth * Mathf.Max(0f, remainingHpPercentDamage);
                CurrentHealth -= percentDamage;
                effectiveDamage += percentDamage;
            }

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
                _hasSplit = true;
                CurrentHealth = MaxHealth * 0.4f;
                EventBus.Publish(new EnemyKilledEvent
                {
                    EnemyId = InstanceId,
                    Position = transform.position
                });

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

        public void ApplyStatus(StatusEffectType type, float value, float duration, int sourceTurretType = -1, int sourceTurretId = -1)
        {
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

        private void ApplyBurnDamage(float damage, int sourceTurretType, int sourceTurretId)
        {
            if (IsDead || damage <= 0f)
                return;

            CurrentHealth -= damage;
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
                GoldReward = Data.GoldReward,
                XPReward = Data.XPReward,
                Position = transform.position,
                EnemyTier = (int)Tier
            });
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
            }
        }

        public void RemoveRadarReveal()
        {
            if (!IsStealth)
                return;

            _radarRevealCount = Mathf.Max(0, _radarRevealCount - 1);
            IsRevealed = _radarRevealCount > 0;
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