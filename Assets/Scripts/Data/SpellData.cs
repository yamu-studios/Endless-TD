// ============================================================================
// ETD.Data - SpellData.cs
// v1.0 active spell system (see [[etd-v1-full-release]] Phase 2). Global-effect,
// no targeting, pure-cooldown model: one spell is picked in the planning tab,
// cast manually during the run, off cooldown to cast again. Shop-upgradable
// (cooldown reduction), mirroring TraitData.GetEffectiveValue's shop-scaling shape.
// ============================================================================
using UnityEngine;

namespace ETD.Data
{
    public enum SpellEffectType
    {
        /// <summary>Pure damage to every active enemy, as a fraction of EACH enemy's
        /// current HP (same scaling class as TurretEvolutionData.HitCurrentHPPercent —
        /// stays relevant at any wave, unlike a flat damage number would). Lightning Storm.</summary>
        DamageAllEnemiesPercentCurrentHP,

        /// <summary>Applies StatusEffectType.Freeze to every active enemy for EffectDuration. Freeze Pulse.</summary>
        FreezeAllEnemies,

        /// <summary>+EffectValue attack speed for every turret for EffectDuration
        /// (EffectValue 1.0 = +100% = doubles). Overclock.</summary>
        AttackSpeedBuffAllTurrets,

        /// <summary>Gold income multiplier of (1 + EffectValue) for EffectDuration
        /// (EffectValue 1.0 = +100% = doubles). Gold Surge.</summary>
        GoldIncomeBuff,

        // v1.0 Phase 5 - appended, do not reorder.

        /// <summary>Applies StatusEffectType.Expose to every active enemy for
        /// EffectDuration, amplifying ALL mitigated damage they take by EffectValue
        /// (0.35 = +35%). Reuses Railgun's status, already consumed in
        /// EnemyController.ApplyMitigation. Breach Protocol.</summary>
        ExposeAllEnemies,

        /// <summary>Restores EffectValue lives, clamped to RunData.MaxLives. The only
        /// spell that answers a leak; ignores EffectDuration. Aegis Protocol.</summary>
        RestoreLives
    }

    /// <summary>Where a spell's cast VFX is placed. A global spell reads very
    /// differently depending on this: a storm wants scattered ground strikes, a freeze
    /// wants a burst on each enemy, and a buff wants an effect on the things buffed.</summary>
    public enum SpellVFXTarget
    {
        /// <summary>Random grid cells across the map. Lightning Storm.</summary>
        RandomCells = 0,
        /// <summary>One per live enemy. Freeze Pulse, Breach Protocol.</summary>
        AllEnemies,
        /// <summary>One per placed turret. Overclock, Gold Surge.</summary>
        AllTurrets,
        /// <summary>A single effect at the lane exit (the base you are defending). Aegis Protocol.</summary>
        BaseOrCenter,

        /// <summary>A single effect at the centroid of the playable grid. For one large
        /// area effect meant to blanket the whole board (e.g. a gold rain), where the
        /// lane exit would be off to one side.</summary>
        MapCenter
    }

    [CreateAssetMenu(fileName = "New Spell", menuName = "ETD/Spell Data")]
    public class SpellData : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;

        [Header("Localization")]
        public string LocalizationKey;

        [Header("Effect")]
        public SpellEffectType EffectType;
        public float EffectValue;
        [Tooltip("Only used by effects with a duration (e.g. SlowAllEnemies).")]
        public float EffectDuration;

        [Header("Cast VFX")]
        [Tooltip("Optional. Name of a GameObject that already lives in the Game scene " +
                 "(e.g. 'GoldSurge_VFX'). If set, casting simply switches that object on " +
                 "and switches it off again after the timer — no pooling, no spawning, " +
                 "and it takes precedence over CastVFXPrefab. Use this for authored " +
                 "one-off effects that are placed and tuned by hand in the scene.")]
        public string CastVFXSceneObjectName;

        [Tooltip("Optional. Spawned at random points across the map the moment the spell " +
                 "is cast, to sell a map-wide effect (e.g. Lightning Storm's zaps). " +
                 "Pooled through VFXManager, so the prefab must have a ParticleSystem.")]
        public GameObject CastVFXPrefab;

        [Tooltip("Where the effect is placed. RandomCells scatters across the map; " +
                 "AllEnemies/AllTurrets put one on each; BaseOrCenter spawns a single " +
                 "effect at the lane exit.")]
        public SpellVFXTarget CastVFXTarget = SpellVFXTarget.RandomCells;

        [Tooltip("For RandomCells: how many to spawn. For AllEnemies/AllTurrets: an " +
                 "upper cap so a huge wave cannot spawn hundreds at once. 0 disables the VFX.")]
        [Min(0)] public int CastVFXCount = 12;

        [Tooltip("REQUIRED for looping prefabs (buff auras, sparkles): a looping " +
                 "ParticleSystem never reports 'done', so a plain spawn would stay " +
                 "checked out of the VFX pool forever. With this on, the effect is " +
                 "parented to its target (where there is one) and explicitly stopped " +
                 "when the timer below elapses.")]
        public bool CastVFXFollowForDuration;

        [Tooltip("Seconds a looping effect runs before being stopped. 0 = use " +
                 "EffectDuration. Set this for spells with no duration of their own " +
                 "(e.g. an instant heal that still wants a few seconds of shield VFX).")]
        [Min(0f)] public float CastVFXLoopSeconds = 0f;

        /// <summary>How long a looping cast VFX should run before being stopped.</summary>
        public float ResolvedVFXLoopSeconds =>
            CastVFXLoopSeconds > 0f ? CastVFXLoopSeconds : EffectDuration;

        [Tooltip("Vertical offset from the grid cell centre, so the effect reads above " +
                 "the ground plane rather than being buried in it.")]
        public float CastVFXHeightOffset = 0f;

        [Tooltip("Random extra delay per spawn, in seconds. 0 = perfectly simultaneous; " +
                 "a small value (0.1-0.2) makes a storm feel less mechanical.")]
        [Min(0f)] public float CastVFXScatterDelay = 0f;

        [Tooltip("Uniform scale applied to each spawned effect. Lets a vendor prefab be " +
                 "sized to the board without editing the prefab itself. 1 = prefab size.")]
        [Min(0.01f)] public float CastVFXScale = 1f;

        [Tooltip("Random +/- variation applied on top of CastVFXScale, as a fraction " +
                 "(0.2 = each spawn is 80%-120% of the scale). 0 = every copy identical.")]
        [Range(0f, 0.9f)] public float CastVFXScaleVariance = 0f;

        [Header("Cooldown")]
        public float Cooldown = 20f;
        [Tooltip("Cooldown reduction per shop upgrade level, as a fraction of base (0.05 = -5%/level). Shop sells up to 10 levels by convention (see ShopWindowUI), so default maxes at -50% cooldown.")]
        public float CooldownReductionPerLevel = 0.05f;

        [Header("Unlock")]
        public bool IsUnlockedByDefault = true;

        public float GetEffectiveCooldown(int upgradeLevel)
        {
            float reduction = Mathf.Clamp01(Mathf.Max(0, upgradeLevel) * Mathf.Max(0f, CooldownReductionPerLevel));
            return Mathf.Max(1f, Cooldown * (1f - reduction));
        }
    }
}
