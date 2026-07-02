// ============================================================================
// ETD.Turrets - SupportAuraSystem.cs
// Centralized static support-aura reconciliation.
//
// Turrets do not move during a run, so repeatedly scanning the same neighbours
// every 0.15 s is wasted work. This component recalculates aura membership only
// after a relevant topology/stat change (place, sell, evolve, level, restore or
// run modifier change). Enemy slow auras remain periodic because enemies move.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using ETD.Core;
using ETD.Enemies;

namespace ETD.Turrets
{
    [DisallowMultipleComponent]
    public sealed class SupportAuraSystem : MonoBehaviour
    {
        [Header("Moving Enemy Aura Performance")]
        [Tooltip("How often evolved support debuff auras reconcile moving enemies. 0.35 = 2.86 Hz.")]
        [SerializeField, Min(0.10f)] private float _enemySlowRefreshInterval = 0.35f;

        [Tooltip("How often enemies that have left every debuffer aura are cleared. Matches the old timed-status grace period while avoiding a full stale scan every reconciliation.")]
        [SerializeField, Min(0.20f)] private float _enemySlowRemovalSweepInterval = 0.65f;

        private struct AuraValues
        {
            public float DamageBonus;
            public float SpeedBonus;

            public AuraValues(float damageBonus, float speedBonus)
            {
                DamageBonus = damageBonus;
                SpeedBonus = speedBonus;
            }
        }

        private struct AppliedSlowAura
        {
            public int RuntimeId;
            public float Slow;

            public AppliedSlowAura(int runtimeId, float slow)
            {
                RuntimeId = runtimeId;
                Slow = slow;
            }
        }

        private TurretManager _turretManager;
        private EnemyManager _enemyManager;
        private bool _topologyDirty = true;
        private float _nextEnemySlowRefreshTime;
        private float _nextSlowRemovalSweepTime;

        // Persistent cache: targets only receive a RecalculateStats call when the
        // resolved strongest aura changes.
        private readonly Dictionary<TurretController, AuraValues> _resolvedAuras = new(128);
        private readonly Dictionary<TurretController, AuraValues> _desiredAuras = new(128);
        private readonly List<TurretController> _staleAuraTargets = new(64);

        // Support list changes only on a static-aura rebuild, not every slow tick.
        private readonly List<TurretController> _supportTurrets = new(32);
        private readonly List<TurretController> _nearbyTurrets = new(32);
        private readonly List<EnemyController> _nearbyEnemies = new(64);
        private readonly Dictionary<EnemyController, float> _strongestSlowByEnemy = new(128);

        // Re-applying an already-active slow was previously the dominant cost. Keep
        // the last value/expiry we wrote, so identical aura state only refreshes near
        // expiry or when a stronger aura becomes active.
        private readonly Dictionary<EnemyController, AppliedSlowAura> _appliedSlowAuras = new(128);
        private readonly List<EnemyController> _staleSlowAuraTargets = new(64);

        public void Initialize(TurretManager turretManager)
        {
            _turretManager = turretManager;
            ServiceLocator.TryGet(out _enemyManager);
            MarkTopologyDirty();
        }

        public void MarkTopologyDirty()
        {
            _topologyDirty = true;
        }

        private void LateUpdate()
        {
            if (_turretManager == null)
                return;

            if (_topologyDirty)
                RebuildStaticAuras();

            float now = Time.time;
            if (now < _nextEnemySlowRefreshTime)
                return;

            _nextEnemySlowRefreshTime = now + Mathf.Max(0.10f, _enemySlowRefreshInterval);
            RefreshEnemySlowAuras();
        }

        private void RebuildStaticAuras()
        {
            Profiler.BeginSample("SupportAuraSystem.RebuildStaticAuras");
            _topologyDirty = false;
            _desiredAuras.Clear();
            _supportTurrets.Clear();

            int maxAffectedTurretCount = 0;

            foreach (TurretController support in _turretManager.AllTurrets)
            {
                if (support == null || !support.IsSupportTurret)
                    continue;

                _supportTurrets.Add(support);
                support.EnsureSupportAuraVisual();

                if (!support.TryGetSupportAuraProfile(
                        out float radius,
                        out float damageBonus,
                        out float speedBonus,
                        out _))
                {
                    continue;
                }

                Profiler.BeginSample("SupportAuraSystem.Static.QueryTurrets");
                _turretManager.GetTurretsInRange(
                    support.transform.position,
                    radius,
                    _nearbyTurrets);
                Profiler.EndSample();

                // Preserve prior challenge semantics: the original event counted
                // every turret in range, including the support source itself.
                maxAffectedTurretCount = Mathf.Max(maxAffectedTurretCount, _nearbyTurrets.Count);

                for (int i = 0; i < _nearbyTurrets.Count; i++)
                {
                    TurretController target = _nearbyTurrets[i];
                    if (target == null || target == support)
                        continue;

                    if (_desiredAuras.TryGetValue(target, out AuraValues existing))
                    {
                        existing.DamageBonus = Mathf.Max(existing.DamageBonus, damageBonus);
                        existing.SpeedBonus = Mathf.Max(existing.SpeedBonus, speedBonus);
                        _desiredAuras[target] = existing;
                    }
                    else
                    {
                        _desiredAuras.Add(target, new AuraValues(damageBonus, speedBonus));
                    }
                }
            }

            Profiler.BeginSample("SupportAuraSystem.Static.ApplyDeltas");
            _staleAuraTargets.Clear();
            foreach (KeyValuePair<TurretController, AuraValues> pair in _resolvedAuras)
            {
                if (pair.Key == null || !_desiredAuras.ContainsKey(pair.Key))
                    _staleAuraTargets.Add(pair.Key);
            }

            for (int i = 0; i < _staleAuraTargets.Count; i++)
            {
                TurretController target = _staleAuraTargets[i];
                if (target != null)
                    target.SetResolvedSupportAura(0f, 0f);
                _resolvedAuras.Remove(target);
            }

            foreach (KeyValuePair<TurretController, AuraValues> pair in _desiredAuras)
            {
                TurretController target = pair.Key;
                if (target == null)
                    continue;

                AuraValues desired = pair.Value;
                if (_resolvedAuras.TryGetValue(target, out AuraValues applied) &&
                    Mathf.Approximately(applied.DamageBonus, desired.DamageBonus) &&
                    Mathf.Approximately(applied.SpeedBonus, desired.SpeedBonus))
                {
                    continue;
                }

                target.SetResolvedSupportAura(desired.DamageBonus, desired.SpeedBonus);
                _resolvedAuras[target] = desired;
            }
            Profiler.EndSample();

            // If the final debuffer was sold/evolved, remove its persistent slow
            // immediately instead of waiting for the next moving-enemy sweep.
            if (_supportTurrets.Count == 0)
                ClearAllSupportSlows();

            // The old per-support event was only used to keep the "buff N towers"
            // challenge progress at the highest observed simultaneous count. One
            // event per topology change gives the same result without EventBus spam.
            EventBus.Publish(new BuffTurretsEvent { Count = maxAffectedTurretCount });
            Profiler.EndSample();
        }

        private void RefreshEnemySlowAuras()
        {
            if (_supportTurrets.Count == 0)
                return;

            if (_enemyManager == null && !ServiceLocator.TryGet(out _enemyManager))
                return;

            Profiler.BeginSample("SupportAuraSystem.RefreshEnemySlowAuras");
            _strongestSlowByEnemy.Clear();

            // The spatial lookup remains necessary because enemies move. The expensive
            // status write is no longer part of this loop: we only resolve the strongest
            // aura value for each candidate enemy here.
            for (int i = 0; i < _supportTurrets.Count; i++)
            {
                TurretController support = _supportTurrets[i];
                if (support == null || !support.TryGetSupportAuraProfile(
                        out float radius,
                        out _,
                        out _,
                        out float enemySlow) || enemySlow <= 0f)
                {
                    continue;
                }

                Profiler.BeginSample("SupportAuraSystem.Slow.QueryEnemies");
                _enemyManager.GetEnemiesInRange(
                    support.transform.position,
                    radius,
                    _nearbyEnemies);
                Profiler.EndSample();

                for (int enemyIndex = 0; enemyIndex < _nearbyEnemies.Count; enemyIndex++)
                {
                    EnemyController enemy = _nearbyEnemies[enemyIndex];
                    if (enemy == null || enemy.IsDead)
                        continue;

                    if (_strongestSlowByEnemy.TryGetValue(enemy, out float existingSlow))
                    {
                        if (enemySlow > existingSlow)
                            _strongestSlowByEnemy[enemy] = enemySlow;
                    }
                    else
                    {
                        _strongestSlowByEnemy.Add(enemy, enemySlow);
                    }
                }
            }

            // Only changed enemy aura values write into EnemyController / VFX / events.
            // A stable enemy inside a stable debuffer radius now costs one dictionary
            // read, not a timed Slow status refresh every reconciliation.
            Profiler.BeginSample("SupportAuraSystem.Slow.ApplyResolved");
            foreach (KeyValuePair<EnemyController, float> pair in _strongestSlowByEnemy)
            {
                EnemyController enemy = pair.Key;
                if (enemy == null || enemy.IsDead)
                    continue;

                float desiredSlow = pair.Value;
                bool needsApply = true;
                if (_appliedSlowAuras.TryGetValue(enemy, out AppliedSlowAura previous))
                {
                    bool recycledEnemy = previous.RuntimeId != enemy.RuntimeId;
                    bool valueChanged = Mathf.Abs(desiredSlow - previous.Slow) > 0.0001f;
                    needsApply = recycledEnemy || valueChanged;
                }

                if (!needsApply)
                    continue;

                // Match the old timed-aura challenge accounting without creating a
                // timed status entry on the enemy.
                enemy.SetSupportAuraSlow(desiredSlow, _enemySlowRemovalSweepInterval);
                _appliedSlowAuras[enemy] = new AppliedSlowAura(enemy.RuntimeId, desiredSlow);
            }
            Profiler.EndSample();

            // Leaving an aura used to leave the timed slow active for roughly
            // _enemySlowDuration. Sweep at the same cadence, but only then, so we do
            // not scan all cached enemies on every reconciliation.
            float now = Time.time;
            if (now >= _nextSlowRemovalSweepTime)
            {
                _nextSlowRemovalSweepTime = now + Mathf.Max(0.20f, _enemySlowRemovalSweepInterval);
                Profiler.BeginSample("SupportAuraSystem.Slow.ClearExited");
                _staleSlowAuraTargets.Clear();

                foreach (KeyValuePair<EnemyController, AppliedSlowAura> pair in _appliedSlowAuras)
                {
                    EnemyController enemy = pair.Key;
                    if (enemy == null || enemy.IsDead || enemy.RuntimeId != pair.Value.RuntimeId ||
                        !_strongestSlowByEnemy.ContainsKey(enemy))
                    {
                        _staleSlowAuraTargets.Add(enemy);
                    }
                }

                for (int i = 0; i < _staleSlowAuraTargets.Count; i++)
                {
                    EnemyController enemy = _staleSlowAuraTargets[i];
                    if (enemy != null && !enemy.IsDead)
                        enemy.SetSupportAuraSlow(0f);
                    _appliedSlowAuras.Remove(enemy);
                }
                Profiler.EndSample();
            }

            Profiler.EndSample();
        }

        private void ClearAllSupportSlows()
        {
            if (_appliedSlowAuras.Count == 0)
                return;

            _staleSlowAuraTargets.Clear();
            foreach (KeyValuePair<EnemyController, AppliedSlowAura> pair in _appliedSlowAuras)
                _staleSlowAuraTargets.Add(pair.Key);

            for (int i = 0; i < _staleSlowAuraTargets.Count; i++)
            {
                EnemyController enemy = _staleSlowAuraTargets[i];
                if (enemy != null && !enemy.IsDead)
                    enemy.SetSupportAuraSlow(0f);
                _appliedSlowAuras.Remove(enemy);
            }
        }

    }
}
