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
        GoldIncomeBuff
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
