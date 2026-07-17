// ============================================================================
// ETD.Data - TurretData.cs  [UPDATED - matches ETD_FULL_SHEET]
// Base values are conservative because of infinite upgrades + relic/spec scaling.
// ============================================================================
using UnityEngine;

namespace ETD.Data
{
    public enum TurretType
    {
        Basic,
        Frost,
        Laser,
        Inferno,
        Lightning,
        Support,
        Radar,

        // v1.0 Phase 3 - appended, do not reorder. See [[etd-v1-full-release]].
        Void,
        Toxin,
        Railgun
    }

    public enum TargetingMode
    {
        First,
        Last,
        Strongest,
        Closest,
        None // Support/Radar
    }

    [CreateAssetMenu(fileName = "New Turret", menuName = "ETD/Turret Data")]
    public class TurretData : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public TurretType Type;
        public Sprite Icon;
        public GameObject Prefab;
        public float yOffset;

        [Header("Base Stats (conservative — endless scaling)")]
        public float Damage = 18f;
        public float AttackInterval = 0.7f; // seconds between attacks
        public float Range = 4.5f;
        public int Cost = 100;
        public bool IsContinuousBeam; // Laser type

        [Header("Upgrades")]
        [Tooltip("Damage upgrade value. Runtime now converts this to multiplicative growth. Existing flat-style values are converted as DamagePerLevel / base damage, then capped in TurretController.")]
        public float DamagePerLevel = 2f;
        public float AttackSpeedPerLevel = 0.01f; // reduces interval
        public float RangePerLevel = 0.05f;
        public int BaseUpgradeCost = 30;

        [Tooltip("Cost growth per level. Keep near 1.3: with +15% damage per level, 1.325 prices doubling a turret's damage at ~4x gold (cost ~ power^2). The old 1.75 priced it at ~16x (power^4), which made deep upgrades strictly worse than spamming new turrets.")]
        public float UpgradeCostMultiplier = 1.325f;

        [Header("Unlock Condition")]
        [Tooltip("Short text shown instead of description when locked. E.g. 'Reach Wave 10'")]
        public string UnlockCondition;
        public ChallengeConditionType UnlockConditionType;
        public float UnlockConditionTarget;
        [Tooltip("True if available from the start — skip unlock check")]
        public bool IsUnlockedByDefault = false;

        [Header("Localization")]
        [Tooltip("Base key used for all localized fields. E.g. 'trait_tr001'")]
        public string LocalizationKey;

        [Header("Targeting")]
        public TargetingMode DefaultTargeting = TargetingMode.First;
        public bool CanTargetStealth;

        [Header("Frost")]
        public float SlowPercent = 0.25f;
        public float SlowDuration = 1.5f;

        [Header("Basic")]
        [Tooltip("Basic's signature status: reduces the target's armor by this fraction for ArmorBreakDuration.")]
        public float ArmorBreakPercent = 0.2f;
        public float ArmorBreakDuration = 2f;

        [Header("Void")]
        [Tooltip("Void's signature status: reduces the target's effective armor by this fraction for WeakenDuration (see EnemyController.ApplyMitigation).")]
        public float WeakenPercent = 0.25f;
        public float WeakenDuration = 3f;

        [Header("Toxin")]
        [Tooltip("Toxin's signature identity: every hit deals this fraction of the target's CURRENT HP as bonus PURE damage (bypasses Armor and affinity entirely via TakePureDamage, unlike HitCurrentHPPercent which goes through normal mitigation).")]
        public float ToxinPurePercent = 0.02f;

        [Header("Railgun")]
        [Tooltip("Railgun's signature status: increases ALL mitigated damage the target takes by this fraction for ExposeDuration (see EnemyController.ApplyMitigation).")]
        public float ExposePercent = 0.2f;
        public float ExposeDuration = 3f;

        [Header("Inferno")]
        public float BurnDPS = 6f;
        public float BurnDuration = 3f;

        [Header("Lightning")]
        public int ChainCount = 3;
        public float ChainDamageFalloff = 0.6f; // 60% of previous
        public float ChainRange = 2f; // 60% of previous

        [Header("Support")]
        public float SupportDamageAura = 0.18f;   // +18%
        public float SupportSpeedAura;              // Path A: +15%
        public float SupportEnemySlowAura;           // Path B: -18%

        [Header("Support Upgrade Scaling")]
        [Tooltip("Extra damage aura per support turret level. 0.01 = +1% per level.")]
        public float SupportDamageAuraPerLevel = 0.01f;

        [Tooltip("Extra attack speed aura per support turret level. 0.005 = +0.5% per level.")]
        public float SupportSpeedAuraPerLevel = 0.005f;

        [Tooltip("Extra enemy slow aura per support turret level. 0.005 = +0.5% slow per level.")]
        public float SupportEnemySlowAuraPerLevel = 0.005f;

        [Header("Radar")]
        public float RevealRange = 5.5f;

        [Tooltip("Extra reveal range per radar level (0.08 = +0.08/level, roughly doubling a 3.5 " +
                 "base reveal by level 50). Radar never attacks, so without this its upgrades " +
                 "bought nothing (player-reported).")]
        public float RevealRangePerLevel = 0.08f;

        [Header("Evolution (Lv 15)")]
        public int EvolveLevel = 15;
        public TurretEvolutionData PathA;
        public TurretEvolutionData PathB;

        [Header("Second Evolution Tier (Lv 25, shared - converges from either Path A or B)")]
        public int EvolveLevel2 = 25;
        public TurretEvolutionData Tier2;

        /// <summary>
        /// Attacks per second (inverse of interval)
        /// </summary>
        public float AttackSpeed => IsContinuousBeam ? 1f : (AttackInterval > 0 ? 1f / AttackInterval : 1f);
    }

    [System.Serializable]
    public class TurretEvolutionData
    {
        public string Id;
        public string Name;
        [TextArea] public string Description;
        public Sprite Icon;
        public GameObject EvolvedPrefab;

        [Header("Stat Overrides")]
        public float DamageOverride = -1;      // -1 = use base scaled
        public float AttackIntervalOverride = -1;
        public float RangeOverride = -1;

        [Header("Basic Evolutions")]
        public bool DoubleProjectile;          // TU001A: 2 shots at 72% each
        public float PerShotDamagePercent = 1f;// TU001A: 0.72
        public bool HeavyDamage;               // TU001B: Sniper
        public float CritDamageBonus;          // TU001B: +50% crit dmg

        [Header("Frost Evolutions")]
        public bool FreezeOnHit;               // TU002A: 0.8s freeze
        public float FreezeDuration = 0.8f;
        public float FreezeImmunityTime = 8f;  // per-enemy cooldown
        public bool AreaSlow;                  // TU002B: area slow burst
        public float AreaSlowPercent = 0.18f;
        public float AreaSlowRadius = 1.6f;
        public float AreaSlowDuration = 2f;

        [Header("Laser Evolutions")]
        public bool StackingDamage;            // TU003A: +12% per 0.5s
        public float StackingDPSPercent = 0.12f;
        public float StackingInterval = 0.5f;
        public float StackingCap = 3.0f;       // +300% max
        public int MultiTargetCount;           // TU003B: 3 beams
        public float MultiTargetDamagePercent = 0.45f;

        [Header("Inferno Evolutions")]
        public bool AreaDamage;                // TU004A: cone flame
        public float ConeDPS;
        public float ConeRange = 3.2f;
        public float ConeBurnDPS = 8f;
        public bool StackingBurn;              // TU004B: stacking burn
        public float BurnStackDPS = 5f;
        public int MaxBurnStacks = 10;

        [Header("Lightning Evolutions")]
        public int ExtendedChainCount;         // TU005A: 7 chains
        public float ExtendedChainFalloff = 0.5f;
        public float ChainHPPercentDamage;     // TU005B: 5% current HP
        public float ChainHPCooldown = 8f;     // per-enemy cooldown

        [Header("Late-Game Scaling (any evolved turret)")]
        [Tooltip("Evolved turret hits deal this fraction of the target's CURRENT HP as bonus " +
                 "damage (0.04 = +4% current HP per hit). This is the same scaling class as the " +
                 "Lightning chain evolution, so non-lightning builds stay viable past wave 100+. " +
                 "Counts toward percent-HP damage tracking.")]
        public float HitCurrentHPPercent;

        [Header("Support Evolutions")]
        public bool BuffAttackSpeed;           // TU006A: +15% speed aura
        public float AttackSpeedAura = 0.15f;
        public bool DebuffEnemySpeed;          // TU006B: -18% enemy speed
        public float EnemySlowAura = 0.18f;

        [Header("Tier 2 (Lv 25, shared, see [[etd-v1-full-release]])")]
        [Tooltip("Basic's Tier2: additive bonus on top of TurretData.ArmorBreakPercent while this tier is active.")]
        public float ArmorBreakPercentBonus;
        [Tooltip("Frost's Tier2: additive bonus on top of TurretData.SlowPercent while this tier is active.")]
        public float SlowPercentBonus;
        [Tooltip("Inferno's Tier2: additive bonus on top of TurretData.BurnDPS (pre hit-scaling) while this tier is active.")]
        public float BurnDPSBonus;
        [Tooltip("Support's Tier2: additive bonus on top of the computed support damage aura while this tier is active.")]
        public float SupportDamageAuraBonus;
        [Tooltip("Void's Tier2: additive bonus on top of TurretData.WeakenPercent while this tier is active.")]
        public float WeakenPercentBonus;
        [Tooltip("Toxin's Tier2: additive bonus on top of TurretData.ToxinPurePercent while this tier is active.")]
        public float ToxinPurePercentBonus;
        [Tooltip("Railgun's Tier2: additive bonus on top of TurretData.ExposePercent while this tier is active.")]
        public float ExposePercentBonus;
    }
}
