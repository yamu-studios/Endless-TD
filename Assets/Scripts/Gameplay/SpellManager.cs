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

            DeactivateAllSceneVFX();
        }

        /// <summary>
        /// Switches off every hand-placed scene effect at run start. Without this a run
        /// would open with them already playing, because they are left visible in the
        /// scene so they can be positioned and tuned in the editor.
        /// </summary>
        private void DeactivateAllSceneVFX()
        {
            if (_database?.Spells == null) return;

            foreach (var spell in _database.Spells)
            {
                if (spell == null || string.IsNullOrEmpty(spell.CastVFXSceneObjectName)) continue;
                var go = ResolveSceneVFX(spell.CastVFXSceneObjectName);
                if (go != null) go.SetActive(false);
            }
        }

        /// <summary>
        /// Re-applies the spell and cooldown captured in a run snapshot, overriding the
        /// Hub selection. A run's spell is fixed once it starts: without this, leaving to
        /// the Hub and continuing would let the player swap spells mid-run, and would
        /// hand back a fully-refreshed cooldown every time they resumed.
        /// Tolerates old snapshots (empty id / negative cooldown) by keeping whatever
        /// Initialize already resolved.
        /// </summary>
        public void RestoreFromSnapshot(string spellId, float cooldownRemaining)
        {
            if (_database != null && !string.IsNullOrEmpty(spellId))
            {
                var spell = _database.GetSpell(spellId);
                if (spell != null)
                    _selectedSpell = spell;
            }

            if (cooldownRemaining >= 0f)
                _cooldownRemaining = Mathf.Max(0f, cooldownRemaining);
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
            SpawnCastVFX(_selectedSpell);
            _cooldownRemaining = CooldownDuration;

            EventBus.Publish(new SpellCastEvent { SpellId = _selectedSpell.Id });
            return true;
        }

        // Scratch list for the shuffle, reused so a cast allocates nothing.
        private readonly System.Collections.Generic.List<Vector2Int> _vfxCells = new();

        /// <summary>
        /// Scatters SpellData.CastVFXPrefab across random map cells at cast time, which
        /// is what sells a global spell as map-wide rather than a number changing in the
        /// HUD. Sampling is without replacement (partial Fisher-Yates) so two zaps never
        /// land on the same cell, and spawning goes through VFXManager's pool, so a cast
        /// reuses instances instead of instantiating.
        /// </summary>
        private void SpawnCastVFX(SpellData spell)
        {
            if (spell == null)
                return;

            // Scene-object mode wins: the effect is authored and positioned by hand in
            // the Game scene, so casting is just an on/off switch. Checked before the
            // prefab path so a spell can keep prefab settings around without using them.
            if (!string.IsNullOrEmpty(spell.CastVFXSceneObjectName))
            {
                ActivateSceneVFX(spell);
                return;
            }

            if (spell.CastVFXPrefab == null || spell.CastVFXCount <= 0)
                return;
            if (VFXManager.Instance == null)
                return;

            switch (spell.CastVFXTarget)
            {
                case SpellVFXTarget.AllEnemies:  SpawnOnEnemies(spell); return;
                case SpellVFXTarget.AllTurrets:  SpawnOnTurrets(spell); return;
                case SpellVFXTarget.BaseOrCenter: SpawnAtBase(spell);   return;
                case SpellVFXTarget.MapCenter:    SpawnAtMapCenter(spell); return;
            }

            if (!ServiceLocator.TryGet<ETD.Grid.GridSystem>(out var grid))
                return;

            var cells = grid.AllPositions;
            if (cells == null || cells.Count == 0)
                return;

            _vfxCells.Clear();
            for (int i = 0; i < cells.Count; i++)
                _vfxCells.Add(cells[i]);

            int count = Mathf.Min(spell.CastVFXCount, _vfxCells.Count);
            for (int i = 0; i < count; i++)
            {
                // Draw cell i from the untaken remainder, then swap it out of range.
                int pick = Random.Range(i, _vfxCells.Count);
                var cell = _vfxCells[pick];
                _vfxCells[pick] = _vfxCells[i];
                _vfxCells[i] = cell;

                Vector3 pos = grid.GridToWorld(cell) + Vector3.up * spell.CastVFXHeightOffset;
                // Random yaw so repeated casts do not look like the same stamped decal.
                var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                float scale = spell.CastVFXScale;
                if (spell.CastVFXScaleVariance > 0f)
                    scale *= 1f + Random.Range(-spell.CastVFXScaleVariance, spell.CastVFXScaleVariance);

                if (spell.CastVFXScatterDelay > 0f)
                    StartCoroutine(SpawnDelayed(spell, pos, rot, scale,
                        Random.Range(0f, spell.CastVFXScatterDelay)));
                else
                    SpawnScaled(spell, pos, rot, scale);
            }
        }

        // Scene VFX objects, resolved once and cached. Cannot use GameObject.Find here:
        // it skips inactive objects, and these spend most of the run switched off.
        private readonly System.Collections.Generic.Dictionary<string, GameObject> _sceneVFX = new();
        private readonly System.Collections.Generic.Dictionary<string, Coroutine> _sceneVFXTimers = new();

        /// <summary>
        /// Switches on a hand-placed scene effect for the spell's duration, then switches
        /// it off again. Re-casting while it is already running restarts the particles and
        /// extends the timer rather than stacking a second shutdown.
        /// </summary>
        private void ActivateSceneVFX(SpellData spell)
        {
            string key = spell.CastVFXSceneObjectName;
            var go = ResolveSceneVFX(key);
            if (go == null)
            {
                Debug.LogWarning($"[Spells] '{spell.DisplayName}' expects a scene object named " +
                                 $"'{key}' in the Game scene, but none was found.");
                return;
            }

            go.SetActive(true);
            // Replaying is required on re-activation: a system that already finished stays
            // finished, so simply toggling the object would show nothing the second time.
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                ps.Play(true);

            if (_sceneVFXTimers.TryGetValue(key, out var running) && running != null)
                StopCoroutine(running);
            _sceneVFXTimers[key] = StartCoroutine(
                DeactivateSceneVFXAfter(go, key, Mathf.Max(0.1f, spell.ResolvedVFXLoopSeconds)));
        }

        private System.Collections.IEnumerator DeactivateSceneVFXAfter(
            GameObject go, string key, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go != null)
            {
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                go.SetActive(false);
            }
            _sceneVFXTimers.Remove(key);
        }

        /// <summary>Finds a scene object by name INCLUDING inactive ones, and caches it.</summary>
        private GameObject ResolveSceneVFX(string name)
        {
            if (_sceneVFX.TryGetValue(name, out var cached) && cached != null)
                return cached;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != name)
                {
                    var child = FindInChildren(root.transform, name);
                    if (child == null) continue;
                    _sceneVFX[name] = child.gameObject;
                    return child.gameObject;
                }
                _sceneVFX[name] = root;
                return root;
            }
            return null;
        }

        private static Transform FindInChildren(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindInChildren(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>One burst per live enemy — makes a map-wide freeze or debuff legible
        /// by showing WHICH units it landed on.</summary>
        private void SpawnOnEnemies(SpellData spell)
        {
            if (!ServiceLocator.TryGet<EnemyManager>(out var enemies)) return;

            var list = enemies.ActiveEnemies;
            int spawned = 0;
            for (int i = 0; i < list.Count && spawned < spell.CastVFXCount; i++)
            {
                var e = list[i];
                if (e == null || e.IsDead) continue;
                SpawnForTarget(spell, e.transform, e.transform.position);
                spawned++;
            }
        }

        /// <summary>One effect per placed turret — buffs should read on the things buffed.</summary>
        private void SpawnOnTurrets(SpellData spell)
        {
            if (!ServiceLocator.TryGet<ETD.Turrets.TurretManager>(out var turrets)) return;

            int spawned = 0;
            foreach (var t in turrets.AllTurrets)
            {
                if (spawned >= spell.CastVFXCount) break;
                if (t == null) continue;
                SpawnForTarget(spell, t.transform, t.transform.position);
                spawned++;
            }
        }

        /// <summary>A single effect at the lane exit — the point being defended. Used by
        /// spells whose moment should feel singular rather than scattered.</summary>
        private void SpawnAtBase(SpellData spell)
        {
            if (!ServiceLocator.TryGet<ETD.Grid.GridSystem>(out var grid)) return;
            SpawnSingleAt(spell, grid.GridToWorld(grid.ExitPoint));
        }

        /// <summary>One large effect over the middle of the board, for blanket effects
        /// where the lane exit would sit off to one side.</summary>
        private void SpawnAtMapCenter(SpellData spell)
        {
            if (!ServiceLocator.TryGet<ETD.Grid.GridSystem>(out var grid)) return;
            var cells = grid.AllPositions;
            if (cells == null || cells.Count == 0) return;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < cells.Count; i++)
                sum += grid.GridToWorld(cells[i]);
            SpawnSingleAt(spell, sum / cells.Count);
        }

        /// <summary>Shared single-instance spawn for the positional targets.</summary>
        private void SpawnSingleAt(SpellData spell, Vector3 worldPos)
        {
            Vector3 pos = worldPos + Vector3.up * spell.CastVFXHeightOffset;
            var ps = VFXManager.Instance.Spawn(spell.CastVFXPrefab, pos, Quaternion.identity);
            if (ps == null) return;
            ps.transform.localScale = Vector3.one * Mathf.Max(0.01f, spell.CastVFXScale);

            // Nothing to parent to here, but a looping prefab still has to be stopped by
            // hand or it never satisfies VFXManager's IsAlive check and never returns.
            if (spell.CastVFXFollowForDuration)
                StartCoroutine(StopAfter(ps, Mathf.Max(0.1f, spell.ResolvedVFXLoopSeconds)));
        }

        /// <summary>
        /// Places one effect on a target, either parented for the spell's duration
        /// (looping auras) or as a detached one-shot at the target's position.
        /// </summary>
        private void SpawnForTarget(SpellData spell, Transform target, Vector3 worldPos)
        {
            float scale = spell.CastVFXScale;
            if (spell.CastVFXScaleVariance > 0f)
                scale *= 1f + Random.Range(-spell.CastVFXScaleVariance, spell.CastVFXScaleVariance);

            if (spell.CastVFXFollowForDuration && target != null)
            {
                var ps = VFXManager.Instance.SpawnAttached(
                    spell.CastVFXPrefab, target, Vector3.up * spell.CastVFXHeightOffset);
                if (ps == null) return;
                ps.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

                // A looping ParticleSystem never satisfies VFXManager's IsAlive check, so
                // it would sit checked-out of the pool forever. Stop it explicitly when
                // the buff ends; StopAttached handles the reparent + return.
                StartCoroutine(StopAfter(ps, Mathf.Max(0.1f, spell.ResolvedVFXLoopSeconds)));
                return;
            }

            SpawnScaled(spell,
                worldPos + Vector3.up * spell.CastVFXHeightOffset,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), scale);
        }

        private System.Collections.IEnumerator StopAfter(ParticleSystem ps, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (ps != null && VFXManager.Instance != null)
                VFXManager.Instance.StopAttached(ps);
        }

        /// <summary>
        /// Spawns one pooled effect at an explicit scale. The scale is written on EVERY
        /// spawn rather than left to the prefab: pool instances are reused, so a previous
        /// cast's scale would otherwise persist onto the next one.
        /// </summary>
        private void SpawnScaled(SpellData spell, Vector3 pos, Quaternion rot, float scale)
        {
            var ps = VFXManager.Instance.Spawn(spell.CastVFXPrefab, pos, rot);
            if (ps == null) return;
            ps.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

            // Scattered effects have no target to parent to, but a prefab containing any
            // looping system still has to be stopped by hand or it never satisfies
            // VFXManager's IsAlive check and stays checked out of the pool forever.
            if (spell.CastVFXFollowForDuration)
                StartCoroutine(StopAfter(ps, Mathf.Max(0.1f, spell.ResolvedVFXLoopSeconds)));
        }

        private System.Collections.IEnumerator SpawnDelayed(
            SpellData spell, Vector3 pos, Quaternion rot, float scale, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (VFXManager.Instance != null)
                SpawnScaled(spell, pos, rot, scale);
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

                // Breach Protocol: same shape as FreezeAllEnemies, but Expose carries a
                // value (the damage amplification) rather than being a binary state.
                // ApplyMitigation already reads it, so nothing else needs to change.
                case SpellEffectType.ExposeAllEnemies:
                    if (!ServiceLocator.TryGet<EnemyManager>(out var enemyManager3)) return;
                    var enemies3 = enemyManager3.ActiveEnemies;
                    for (int i = 0; i < enemies3.Count; i++)
                    {
                        var enemy = enemies3[i];
                        if (enemy == null || enemy.IsDead) continue;
                        enemy.ApplyStatus(StatusEffectType.Expose,
                            Mathf.Max(0f, spell.EffectValue),
                            Mathf.Max(0f, spell.EffectDuration));
                    }
                    break;

                // Aegis Protocol: mirrors the HealHealth spec card in
                // RunManager.OnSpecCardChosen — clamp to MaxLives and publish so the
                // HUD's lives counter updates.
                case SpellEffectType.RestoreLives:
                    if (ServiceLocator.TryGet<RunManager>(out var runManager)
                        && runManager.RunData != null)
                    {
                        var run = runManager.RunData;
                        int restored = Mathf.Max(0, Mathf.RoundToInt(spell.EffectValue));
                        run.Lives = Mathf.Min(run.Lives + restored, run.MaxLives);
                        EventBus.Publish(new LivesChangedEvent
                        {
                            Current = run.Lives,
                            Max = run.MaxLives
                        });
                    }
                    break;
            }
        }
    }
}
