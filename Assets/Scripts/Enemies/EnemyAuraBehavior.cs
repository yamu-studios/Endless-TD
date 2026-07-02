// ============================================================================
// ETD.Enemies - EnemyAuraBehavior.cs
// Handles Buffer and Debuffer enemy aura effects on nearby units.
// Uses EventBus to communicate with turret system (avoids circular dependency).
// Attach alongside EnemyController on Buffer/Debuffer enemy prefabs.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;

namespace ETD.Enemies
{
    // Event for turret system to handle debuff application
    public struct EnemyDebuffAuraEvent
    {
        public Vector3 Center;
        public float Radius;
        public float DebuffPercent;
    }

    public class EnemyAuraBehavior : MonoBehaviour
    {
        private EnemyController _controller;
        private EnemyManager _enemyManager;
        private readonly List<EnemyController> _nearbyEnemies = new(16);
        private float _tickTimer;
        private const float TICK_INTERVAL = 0.5f;

        private void Awake()
        {
            _controller = GetComponent<EnemyController>();
        }

        private void Start()
        {
            _enemyManager = ServiceLocator.Get<EnemyManager>();
        }

        private void Update()
        {
            if (_controller == null || _controller.IsDead) return;
            if (_controller.Data.Type != EnemyType.Buffer
                && _controller.Data.Type != EnemyType.Debuffer) return;

            _tickTimer += Time.deltaTime;
            if (_tickTimer < TICK_INTERVAL) return;
            _tickTimer -= TICK_INTERVAL;

            ApplyAura();
        }

        private void ApplyAura()
        {
            float radius = _controller.Data.AuraRadius;

            if (_controller.Data.Type == EnemyType.Buffer)
            {
                // Buff nearby enemies - stays within Enemies assembly
                _enemyManager.GetEnemiesInRange(transform.position, radius, _nearbyEnemies);
                for (int i = 0; i < _nearbyEnemies.Count; i++)
                {
                    var enemy = _nearbyEnemies[i];
                    if (enemy == _controller || enemy.IsDead) continue;

                    enemy.ApplySpeedMultiplier(
                        "enemy_buffer_" + _controller.RuntimeId,
                        1f + _controller.Data.BuffPercent,
                        TICK_INTERVAL * 1.5f);
                }
            }
            else if (_controller.Data.Type == EnemyType.Debuffer)
            {
                // Publish event for turret system to handle (no direct reference)
                EventBus.Publish(new EnemyDebuffAuraEvent
                {
                    Center = transform.position,
                    Radius = radius,
                    DebuffPercent = _controller.Data.DebuffPercent
                });
            }
        }
    }
}
