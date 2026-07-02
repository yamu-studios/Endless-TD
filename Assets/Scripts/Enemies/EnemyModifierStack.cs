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
            _expiredStatuses.Clear();
            _expiredSpeedSources.Clear();
        }

        public bool HasTimedModifiers => _statuses.Count > 0 || _moveSpeedMultipliers.Count > 0;

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

            if (strongestSlow > 0f)
                multiplier *= Mathf.Clamp01(1f - strongestSlow);

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

            foreach (var kvp in _statuses)
            {
                EnemyStatusModifier modifier = kvp.Value;
                modifier.Duration -= deltaTime;

                if (modifier.Type == StatusEffectType.Burn)
                {
                    modifier.TickTimer += deltaTime;
                    while (modifier.TickTimer >= BurnTickInterval)
                    {
                        modifier.TickTimer -= BurnTickInterval;
                        onBurnDamage?.Invoke(modifier.Value * BurnTickInterval, modifier.SourceTurretType, modifier.SourceTurretId);
                    }
                }

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
