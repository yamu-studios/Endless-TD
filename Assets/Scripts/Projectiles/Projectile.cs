// ============================================================================
// ETD.Projectiles - Projectile.cs
// Visual projectile driven by ProjectileManager's single simulation loop.
// Pooled. This class deliberately has no Unity Update callback.
// GT1050 optimization: manager passes cached time and can resolve overflow
// projectiles immediately without spawning extra hit VFX.
// ============================================================================
using System;
using UnityEngine;
using ETD.Core;
using ETD.Data;
using ETD.Enemies;

namespace ETD.Projectiles
{
    public interface IProjectileHitHandler
    {
        void OnProjectileHitAfterDamage(EnemyController target, float damage,
            StatusEffectType statusType, float statusValue, float statusDuration);
    }

    public sealed class Projectile : MonoBehaviour
    {
        private const float HitDistanceSqr = 0.04f;
        private const float MaxLifetime = 5f;

        [SerializeField] private float _speed = 15f;
        [SerializeField] private TrailRenderer _trail;
        [SerializeField] private bool _rotateToTravelDirection = true;

        [HideInInspector] public GameObject HitVFXPrefab;
        [HideInInspector] public Transform HitVFXAttachTarget;

        private Transform _cachedTransform;
        private EnemyController _target;
        private Transform _targetHitTransform;
        private int _targetRuntimeId;
        private float _damage;
        private StatusEffectType _statusType;
        private bool _isCritical;
        private float _statusValue;
        private float _statusDuration;
        private float _expireAt;
        private bool _isActive;
        private IProjectileHitHandler _hitHandler;
        private Action<EnemyController, float> _onHitAfterDamage;
        private int _sourceTurretType = -1;
        private int _sourceTurretId = -1;

        // Pool / manager state.
        public int PoolPrefabId { get; set; }
        public Action<Projectile> OnComplete;
        internal int ActiveIndex { get; set; } = -1;
        internal bool IsManagedActive => _isActive;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public void Launch(EnemyController target, float damage, float speed,
            StatusEffectType statusType = StatusEffectType.None,
            float statusValue = 0f, float statusDuration = 0f,
            bool isCritical = false,
            Action<EnemyController, float> onHitAfterDamage = null,
            IProjectileHitHandler hitHandler = null,
            int sourceTurretType = -1,
            int sourceTurretId = -1)
        {
            _cachedTransform ??= transform;
            _target = target;
            _targetRuntimeId = target != null ? target.RuntimeId : -1;
            _targetHitTransform = target != null && target.hitTransform != null
                ? target.hitTransform
                : target != null ? target.transform : null;
            _damage = damage;
            _speed = Mathf.Max(0.01f, speed);
            _statusType = statusType;
            _statusValue = statusValue;
            _statusDuration = statusDuration;
            _isCritical = isCritical;
            _expireAt = Time.time + MaxLifetime;
            _isActive = true;
            _hitHandler = hitHandler;
            _onHitAfterDamage = onHitAfterDamage;
            _sourceTurretType = sourceTurretType;
            _sourceTurretId = sourceTurretId;

            if (_trail != null)
                _trail.Clear();
        }

        /// <summary>
        /// Called by ProjectileManager. Keeping projectile simulation in one loop
        /// removes one Unity Update callback per active projectile. Time is passed in
        /// by the manager so hundreds of projectiles do not each read Time.time.
        /// </summary>
        internal void ManagedTick(float deltaTime, float now, bool allowRotation)
        {
            if (!_isActive)
                return;

            if (now >= _expireAt || _target == null || _target.IsDead ||
                _target.RuntimeId != _targetRuntimeId || _targetHitTransform == null)
            {
                Complete();
                return;
            }

            Vector3 position = _cachedTransform.position;
            Vector3 targetPosition = _targetHitTransform.position;
            Vector3 direction = targetPosition - position;
            float sqrDistance = direction.sqrMagnitude;
            float step = _speed * Mathf.Max(0f, deltaTime);
            float stepSqr = step * step;

            // Hit on contact or when this frame's movement would pass the target.
            // This avoids an extra sqrt for close projectiles and prevents overshoot.
            if (sqrDistance <= HitDistanceSqr || sqrDistance <= stepSqr)
            {
                _cachedTransform.position = targetPosition;
                Hit(playHitVFX: true);
                return;
            }

            float inverseDistance = 1f / Mathf.Sqrt(sqrDistance);
            Vector3 normalizedDirection = direction * inverseDistance;
            _cachedTransform.position = position + (normalizedDirection * step);

            if (allowRotation && _rotateToTravelDirection)
                _cachedTransform.forward = normalizedDirection;
        }

        /// <summary>
        /// Used by ProjectileManager when the visual projectile budget is under heavy
        /// pressure. Damage/on-hit gameplay still resolves, but optional hit VFX can be
        /// skipped to avoid a second late-wave spike.
        /// </summary>
        internal void ResolveImmediate(bool playHitVFX)
        {
            if (!_isActive)
                return;

            if (_target != null && !_target.IsDead && _target.RuntimeId == _targetRuntimeId)
                Hit(playHitVFX);
            else
                Complete();
        }

        private void Hit(bool playHitVFX)
        {
            EnemyController target = _target;
            if (target != null && !target.IsDead)
            {
                target.TakeDamage(_damage, _statusType, _statusValue, _statusDuration,
                    isCritical: _isCritical,
                    sourceTurretType: _sourceTurretType,
                    sourceTurretId: _sourceTurretId);
                _hitHandler?.OnProjectileHitAfterDamage(target, _damage, _statusType, _statusValue, _statusDuration);
                _onHitAfterDamage?.Invoke(target, _damage);

                if (playHitVFX && HitVFXPrefab != null && VFXManager.Instance != null)
                {
                    if (HitVFXAttachTarget != null)
                    {
                        Vector3 hitOffset = Vector3.zero;
                        hitOffset.y = _targetHitTransform != null ? _targetHitTransform.position.y : 0f;
                        VFXManager.Instance.SpawnAttached(HitVFXPrefab, HitVFXAttachTarget, hitOffset);
                    }
                    else
                    {
                        VFXManager.Instance.Spawn(HitVFXPrefab, _cachedTransform.position);
                    }
                }
            }

            Complete();
        }

        internal void ForceComplete()
        {
            if (_isActive)
                Complete();
        }

        private void Complete()
        {
            _isActive = false;
            _target = null;
            _targetHitTransform = null;
            _targetRuntimeId = -1;
            HitVFXPrefab = null;
            HitVFXAttachTarget = null;
            _hitHandler = null;
            _onHitAfterDamage = null;
            _isCritical = false;
            _sourceTurretType = -1;
            _sourceTurretId = -1;
            OnComplete?.Invoke(this);
        }
    }
}
