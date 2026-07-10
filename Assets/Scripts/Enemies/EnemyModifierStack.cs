using System;
using System.Collections.Generic;
using ETD.Core;
using UnityEngine;

namespace ETD.Enemies
{
    /// <summary>
    /// Central modifier container for enemy status effects and timed stat modifiers.
    /// Keeps duration, stacking, ticking, and expiry in one place so auras/statuses cannot leak forever.
    /// </summary>
    public sealed class EnemyModifierStack
    {
        private const float BurnTickInterval = 0.5f;

        // Burn is a stacking DoT: every applying hit adds an independent stack with
        // its own DPS and expiry, so burn output scales with attack speed and burn
        // investment instead of being capped at the single strongest application.
        // DefaultMaxBurnStacks bounds regular sources; the Ember Stacker evolution
        // raises the effective cap per application (TurretEvolutionData.MaxBurnStacks).
        // BurnStackSlots is the absolute array bound shared by all sources.
        public const int DefaultMaxBurnStacks = 5;
        private const int BurnStackSlots = 16;

        private struct BurnStack
        {
            public float Dps;
            public float Remaining;
            public int SourceTurretType;
            public int SourceTurretId;
        }

        private readonly BurnStack[] _burnStacks = new BurnStack[BurnStackSlots];
        private int _burnStackCount;
        private float _burnTickTimer;

        // Slow (including support aura) can never fully stop an enemy — only freeze/shock
        // (which are now time-limited per-enemy) reduce movement to zero. This floor
        // prevents the "tiny freeze/slow + high attack speed = permanent lock" exploit
        // and gives slow builds diminishing returns instead of a hard stop.
        private const float MinSlowSpeedMultiplier = 0.15f;

        private readonly Dictionary<StatusEffectType, EnemyStatusModifier> _statuses = new();
        private readonly Dictionary<string, TimedFloatModifier> _moveSpeedMultipliers = new();

        public const int StatusMaskSlow = 1 << (int)StatusEffectType.Slow;
        public const int StatusMaskFreeze = 1 << (int)StatusEffectType.Freeze;
        public const int StatusMaskBurn = 1 << (int)StatusEffectType.Burn;
        public const int StatusMaskShock = 1 << (int)StatusEffectType.Shock;

        private int _timedStatusMask;

        public int StatusMask => _timedStatusMask | (_supportAuraSlow > 0.0001f ? StatusMaskSlow : 0);

        private static int MaskFor(StatusEffectType type)
        {
            return type == StatusEffectType.None ? 0 : 1 << (int)type;
        }

        // Support auras are spatially persistent rather than timed combat statuses.
        // Keeping this value outside _statuses avoids a timed-status write and expiry
        // refresh for every affected enemy on every aura reconciliation.
        private float _supportAuraSlow;
        private readonly List<StatusEffectType> _expiredStatuses = new(8);
        private readonly List<string> _expiredSpeedSources = new(8);

        public void Clear()
        {
            _statuses.Clear();
            _moveSpeedMultipliers.Clear();
            _supportAuraSlow = 0f;
            _timedStatusMask = 0;
            _burnStackCount = 0;
            _burnTickTimer = 0f;
            _expiredStatuses.Clear();
            _expiredSpeedSources.Clear();
        }

        public bool HasTimedModifiers => _statuses.Count > 0 || _moveSpeedMultipliers.Count > 0 || _burnStackCount > 0;

        public int BurnStackCount => _burnStackCount;

        /// <summary>True while a Support debuff aura is slowing this enemy (Exposure hook).</summary>
        public bool HasSupportAuraSlow => _supportAuraSlow > 0.0001f;

        public bool HasStatus(StatusEffectType type)
        {
            int mask = MaskFor(type);
            return mask != 0 && (StatusMask & mask) != 0;
        }

        public bool TryGetStatusValue(StatusEffectType type, out float value)
        {
            if (type == StatusEffectType.Slow)
            {
                value = _supportAuraSlow;
                if (_statuses.TryGetValue(type, out EnemyStatusModifier slow))
                    value = Mathf.Max(value, slow.Value);
                return value > 0.0001f;
            }

            if (type == StatusEffectType.Burn)
            {
                value = 0f;
                for (int i = 0; i < _burnStackCount; i++)
                    value += _burnStacks[i].Dps;
                return _burnStackCount > 0;
            }

            if (_statuses.TryGetValue(type, out EnemyStatusModifier modifier))
            {
                value = modifier.Value;
                return true;
            }

            value = 0f;
            return false;
        }

        /// <summary>
        /// Updates the spatial support-aura slow. Unlike ApplyStatus this does not
        /// create a timed status entry, generate expiry churn, or invoke callbacks
        /// unless the resolved aura value actually changes.
        /// </summary>
        public bool SetSupportAuraSlow(float slow)
        {
            slow = Mathf.Clamp01(slow);
            if (Mathf.Abs(_supportAuraSlow - slow) <= 0.0001f)
                return false;

            _supportAuraSlow = slow;
            return true;
        }

        public void ApplyStatus(
            StatusEffectType type,
            float value,
            float duration,
            Action<StatusEffectType, float> onApplied,
            int sourceTurretType = -1,
            int sourceTurretId = -1)
        {
            if (type == StatusEffectType.None || duration <= 0f)
                return;

            if (type == StatusEffectType.Burn)
            {
                ApplyBurnStack(value, duration, DefaultMaxBurnStacks, onApplied, sourceTurretType, sourceTurretId);
                return;
            }

            value = Mathf.Max(0f, value);
            duration = Mathf.Max(0f, duration);

            if (_statuses.TryGetValue(type, out EnemyStatusModifier existing))
            {
                existing.Duration = Mathf.Max(existing.Duration, duration);
                existing.Value = Mathf.Max(existing.Value, value);
                if (sourceTurretType >= 0)
                {
                    existing.SourceTurretType = sourceTurretType;
                    existing.SourceTurretId = sourceTurretId;
                }
                return;
            }

            _statuses[type] = new EnemyStatusModifier
            {
                Type = type,
                Value = value,
                Duration = duration,
                TickTimer = 0f,
                SourceTurretType = sourceTurretType,
                SourceTurretId = sourceTurretId
            };
            _timedStatusMask |= MaskFor(type);

            onApplied?.Invoke(type, duration);
        }

        /// <summary>
        /// Adds one burn stack. While below the requested cap the stack is appended;
        /// at the cap a stronger application replaces the weakest active stack, and a
        /// weaker one only refreshes the weakest stack's duration (uptime, no free DPS).
        /// onApplied fires only on the not-burning -> burning transition so VFX and
        /// status-applied events keep their previous once-per-burn semantics.
        /// </summary>
        public void ApplyBurnStack(float dps, float duration, int maxStacks,
            Action<StatusEffectType, float> onApplied, int sourceTurretType = -1, int sourceTurretId = -1)
        {
            if (dps <= 0f || duration <= 0f)
                return;

            if (maxStacks <= 0)
                maxStacks = DefaultMaxBurnStacks;
            maxStacks = Mathf.Min(maxStacks, BurnStackSlots);

            if (_burnStackCount < maxStacks)
            {
                _burnStacks[_burnStackCount++] = new BurnStack
                {
                    Dps = dps,
                    Remaining = duration,
                    SourceTurretType = sourceTurretType,
                    SourceTurretId = sourceTurretId
                };

                if (_burnStackCount == 1)
                {
                    _timedStatusMask |= StatusMaskBurn;
                    onApplied?.Invoke(StatusEffectType.Burn, duration);
                }
                return;
            }

            int weakest = 0;
            for (int i = 1; i < _burnStackCount; i++)
            {
                if (_burnStacks[i].Dps < _burnStacks[weakest].Dps)
                    weakest = i;
            }

            if (dps >= _burnStacks[weakest].Dps)
            {
                _burnStacks[weakest] = new BurnStack
                {
                    Dps = dps,
                    Remaining = duration,
                    SourceTurretType = sourceTurretType,
                    SourceTurretId = sourceTurretId
                };
            }
            else
            {
                _burnStacks[weakest].Remaining = Mathf.Max(_burnStacks[weakest].Remaining, duration);
            }
        }

        public void ApplyMoveSpeedMultiplier(string sourceId, float multiplier, float duration)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || duration <= 0f)
                return;

            _moveSpeedMultipliers[sourceId] = new TimedFloatModifier
            {
                SourceId = sourceId,
                Value = Mathf.Max(0f, multiplier),
                Duration = Mathf.Max(0f, duration)
            };
        }

        public float GetMoveSpeedMultiplier()
        {
            if (_supportAuraSlow <= 0.0001f && _moveSpeedMultipliers.Count == 0 && _statuses.Count == 0)
                return 1f;

            int statusMask = StatusMask;

            float multiplier = 1f;

            if (_moveSpeedMultipliers.Count > 0)
            {
                foreach (var kvp in _moveSpeedMultipliers)
                    multiplier *= Mathf.Max(0f, kvp.Value.Value);
            }

            float strongestSlow = _supportAuraSlow;
            if (_statuses.TryGetValue(StatusEffectType.Slow, out EnemyStatusModifier slow))
                strongestSlow = Mathf.Max(strongestSlow, slow.Value);

            // Floor the slow contribution so slow alone can't reach zero movement.
            if (strongestSlow > 0f)
                multiplier *= Mathf.Max(MinSlowSpeedMultiplier, 1f - strongestSlow);

            // Freeze/shock are hard stops, but they are time-limited (freeze now has a
            // per-enemy immunity cooldown in EnemyController), so they cannot lock forever.
            if ((statusMask & (StatusMaskFreeze | StatusMaskShock)) != 0)
                multiplier = 0f;

            return Mathf.Max(0f, multiplier);
        }

        public void Update(
            float deltaTime,
            Action<float, int, int> onBurnDamage,
            Action<StatusEffectType> onExpired)
        {
            if (deltaTime <= 0f || !HasTimedModifiers)
                return;

            _expiredStatuses.Clear();
            _expiredSpeedSources.Clear();

            UpdateBurnStacks(deltaTime, onBurnDamage, onExpired);

            foreach (var kvp in _statuses)
            {
                EnemyStatusModifier modifier = kvp.Value;
                modifier.Duration -= deltaTime;

                if (modifier.Duration <= 0f)
                    _expiredStatuses.Add(kvp.Key);
            }

            foreach (StatusEffectType type in _expiredStatuses)
            {
                _statuses.Remove(type);
                _timedStatusMask &= ~MaskFor(type);
                onExpired?.Invoke(type);
            }

            foreach (var kvp in _moveSpeedMultipliers)
            {
                TimedFloatModifier modifier = kvp.Value;
                modifier.Duration -= deltaTime;
                if (modifier.Duration <= 0f)
                    _expiredSpeedSources.Add(kvp.Key);
            }

            for (int i = 0; i < _expiredSpeedSources.Count; i++)
                _moveSpeedMultipliers.Remove(_expiredSpeedSources[i]);
        }

        private void UpdateBurnStacks(
            float deltaTime,
            Action<float, int, int> onBurnDamage,
            Action<StatusEffectType> onExpired)
        {
            if (_burnStackCount == 0)
                return;

            // All stacks share one batched tick so damage numbers and stat events
            // stay at the pre-stacking frequency regardless of stack count. The
            // combined tick is attributed to the strongest stack's source turret.
            _burnTickTimer += deltaTime;
            while (_burnTickTimer >= BurnTickInterval)
            {
                _burnTickTimer -= BurnTickInterval;

                float totalDps = 0f;
                float strongest = -1f;
                int sourceType = -1;
                int sourceId = -1;
                for (int i = 0; i < _burnStackCount; i++)
                {
                    totalDps += _burnStacks[i].Dps;
                    if (_burnStacks[i].Dps > strongest)
                    {
                        strongest = _burnStacks[i].Dps;
                        sourceType = _burnStacks[i].SourceTurretType;
                        sourceId = _burnStacks[i].SourceTurretId;
                    }
                }

                if (totalDps > 0f)
                    onBurnDamage?.Invoke(totalDps * BurnTickInterval, sourceType, sourceId);
            }

            for (int i = _burnStackCount - 1; i >= 0; i--)
            {
                _burnStacks[i].Remaining -= deltaTime;
                if (_burnStacks[i].Remaining <= 0f)
                {
                    _burnStackCount--;
                    _burnStacks[i] = _burnStacks[_burnStackCount];
                }
            }

            if (_burnStackCount == 0)
            {
                _timedStatusMask &= ~StatusMaskBurn;
                _burnTickTimer = 0f;
                onExpired?.Invoke(StatusEffectType.Burn);
            }
        }
    }

    public sealed class EnemyStatusModifier
    {
        public StatusEffectType Type;
        public float Value;
        public float Duration;
        public float TickTimer;
        public int SourceTurretType = -1;
        public int SourceTurretId = -1;
    }

    public sealed class TimedFloatModifier
    {
        public string SourceId;
        public float Value;
        public float Duration;
    }
}
