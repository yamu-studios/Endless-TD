// ============================================================================
// ETD.Gameplay - SpellManager.cs
// v1.0 active spell system (see [[etd-v1-full-release]] Phase 2). Global-effect,
// no targeting, pure-cooldown model. One spell is selected in the planning tab
// (SaveData.SelectedSpellId) and cast manually during the run via TryCast().
// ============================================================================
using UnityEngine;
using ETD.Core;
using ETD.Data;
using ETD.Enemies;

namespace ETD.Gameplay
{
    public class SpellManager : MonoBehaviour
    {
        private GameDatabase _database;
        private SpellData _selectedSpell;
        private float _cooldownRemaining;
        private int _upgradeLevel;

        public SpellData SelectedSpell => _selectedSpell;
        public float CooldownRemaining => _cooldownRemaining;
        public float CooldownDuration => _selectedSpell != null
            ? _selectedSpell.GetEffectiveCooldown(_upgradeLevel) : 0f;
        public bool IsReady => _selectedSpell != null && _cooldownRemaining <= 0f;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<SpellManager>();
        }

        /// <summary>Call once at run start (mirrors TraitManager.Initialize/TurretManager setup).</summary>
        public void Initialize(GameDatabase database, SaveData save)
        {
            _database = database;
            _selectedSpell = _database != null && !string.IsNullOrEmpty(save?.SelectedSpellId)
                ? _database.GetSpell(save.SelectedSpellId)
                : null;
            _upgradeLevel = save != null ? Mathf.Max(0, save.ShopSpellUpgradeLevel) : 0;
            _cooldownRemaining = 0f;
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f)
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Time.deltaTime);
        }

        public bool TryCast()
        {
            if (!IsReady)
                return false;

            ApplyEffect(_selectedSpell);
            _cooldownRemaining = CooldownDuration;

            EventBus.Publish(new SpellCastEvent { SpellId = _selectedSpell.Id });
            return true;
        }

        private void ApplyEffect(SpellData spell)
        {
            switch (spell.EffectType)
            {
                case SpellEffectType.DamageAllEnemiesPercentCurrentHP:
                    if (!ServiceLocator.TryGet<EnemyManager>(out var enemyManager)) return;
                    var enemies = enemyManager.ActiveEnemies;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        var enemy = enemies[i];
                        if (enemy == null || enemy.IsDead) continue;
                        float dmg = enemy.CurrentHealth * Mathf.Max(0f, spell.EffectValue);
                        if (dmg > 0f)
                            enemy.TakePureDamage(dmg, playHitVFX: false, showDamageNumber: false);
                    }
                    break;

                case SpellEffectType.FreezeAllEnemies:
                    if (!ServiceLocator.TryGet<EnemyManager>(out var enemyManager2)) return;
                    var enemies2 = enemyManager2.ActiveEnemies;
                    for (int i = 0; i < enemies2.Count; i++)
                    {
                        var enemy = enemies2[i];
                        if (enemy == null || enemy.IsDead) continue;
                        enemy.ApplyStatus(StatusEffectType.Freeze, 1f, Mathf.Max(0f, spell.EffectDuration));
                    }
                    break;

                // Overclock / Gold Surge reuse pre-existing timer fields on
                // RunStatModifiers (_overclockTimer/_overclockBonus,
                // _goldSurgeTimer/_goldSurgeMultiplier) that already existed with no
                // caller before this — see RunStatModifiers.ApplyOverclock/ApplyGoldSurge.
                case SpellEffectType.AttackSpeedBuffAllTurrets:
                    if (ServiceLocator.TryGet<IRunStatModifiers>(out var statsA))
                        statsA.ApplyOverclock(spell.EffectValue, spell.EffectDuration);
                    break;

                case SpellEffectType.GoldIncomeBuff:
                    if (ServiceLocator.TryGet<IRunStatModifiers>(out var statsG))
                        statsG.ApplyGoldSurge(1f + spell.EffectValue, spell.EffectDuration);
                    break;
            }
        }
    }
}
