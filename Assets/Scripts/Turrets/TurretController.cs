// ============================================================================
// ETD.Turrets - TurretController.cs
// Base turret component. Handles targeting, shooting, upgrades, evolution.
// Crit implementation added:
// - Reads crit chance from IRunStatModifiers, so traits/spec cards can affect it.
// - Projectile/direct hits can crit.
// - Lightning chain hits can crit, without double-damaging the primary target.
// - Laser rolls crit once per short damage window instead of every frame.
// - Evolution CritDamageBonus is applied to crit multiplier.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using ETD.Core;
using ETD.Data;
using ETD.Enemies;
using ETD.Projectiles;
using System.Collections;

namespace ETD.Turrets
{
    public class TurretController : MonoBehaviour, IProjectileHitHandler
    {
        [Header("Runtime")]
        public int InstanceId;
        public TurretData Data;
        public Vector2Int GridPosition;

        [Header("Aiming")]
        [Tooltip("Turret must be within this angle (degrees) of target before firing")]
        [SerializeField] private float _aimThreshold = 5f;

        [Tooltip("How fast the turret rotates toward its target (degrees per second)")]
        [SerializeField] private float _rotationSpeed = 180f;

        [Header("Aiming Performance")]
        [Tooltip("How often projectile turrets recompute aim direction while tracking the same target. 0.033 = 30 Hz visual aiming.")]
        [SerializeField, Min(0.005f)] private float _projectileAimRefreshInterval = 0.033f;

        [Tooltip("How often laser turrets recompute aim direction while tracking the same target. Lasers are more visible, so this is a little faster.")]
        [SerializeField, Min(0.005f)] private float _laserAimRefreshInterval = 0.016f;

        [Tooltip("When already aligned to the same target, projectile turrets can reuse the last aim result for this long.")]
        [SerializeField, Min(0.005f)] private float _lockedProjectileAimRefreshInterval = 0.05f;

        [Header("Critical Hits")]
        [Tooltip("Default critical damage multiplier. 2 means 200% damage.")]
        [SerializeField] private float _baseCritDamageMultiplier = 2f;

        [Tooltip("Continuous lasers roll crit once per this many seconds, not every frame.")]
        [SerializeField] private float _laserCritWindowSeconds = 1f;

        [Header("Attack Speed Safety")]
        [Tooltip("Lowest allowed projectile attack interval after level scaling. Prevents zero/near-zero cooldowns.")]
        [SerializeField] private float _minimumProjectileAttackInterval = 0.12f;

        [Tooltip("Final hard cap for non-laser projectile attacks per second after all buffs/specs/tiles.")]
        [SerializeField] private float _maxProjectileAttackSpeed = 8f;

        [Header("Damage Scaling")]
        [Tooltip("Caps multiplicative damage growth per turret upgrade level. 0.25 = at most +25% damage per turret level.")]
        [SerializeField] private float _maxMultiplicativeDamageGrowthPerLevel = 0.25f;

        [Header("Laser Ramp (base lasers)")]
        [Tooltip("All lasers gain this much bonus damage per second while staying on the same target " +
                 "(0.08 = +8%/s, COMPOUNDING: the multiplier grows on itself so the ramp accelerates). " +
                 "Resets when the target changes. Gives Laser a late-game anti-tank identity. " +
                 "The Path-B stacking evolution replaces this with its own stronger ramp.")]
        [SerializeField] private float _innateLaserRampPerSecond = 0.08f;

        [Tooltip("Cap for the innate ramp bonus. 1 = at most +100% damage from sustained fire.")]
        [SerializeField] private float _innateLaserRampCap = 1.0f;

        [Tooltip("Stacker Laser evolution only: while the ramp is at full stacks, the beam also deals " +
                 "this fraction of the target's MISSING HP per second as execute damage (0.03 = 3%/s). " +
                 "Cannot touch full-HP enemies, accelerates as the target gets lower — the reward for " +
                 "holding the beam on one tank. Set 0 to disable.")]
        [SerializeField, Range(0f, 0.2f)] private float _laserExecuteMissingHPPercent = 0.03f;

        [Header("Identity Scaling")]
        [Tooltip("Turret IDENTITY effects grow with upgrade level: Frost slow strength/duration " +
                 "and the Inferno flat-burn floor gain this fraction per level (0.01 = +1%/level). " +
                 "Capped by the value below. Damage/speed/range already scale separately.")]
        [SerializeField] private float _identityScalingPerLevel = 0.01f;

        [Tooltip("Maximum total identity bonus from levels. 0.5 = at most +50% (reached at level 50).")]
        [SerializeField] private float _identityScalingCap = 0.5f;

        [Header("Burn Scaling")]
        [Tooltip("Burn total damage equals this fraction of the applying hit, spread over the burn " +
                 "duration (0.25 = the burn deals 25% of the hit's damage over its duration). The flat " +
                 "BurnDPS from turret data acts as an early-game floor. Keeps Inferno relevant late-game.")]
        [SerializeField, Range(0f, 1f)] private float _burnHitPercent = 0.25f;

        [Header("Blastfire Splash (Inferno Path A)")]
        [Tooltip("Blastfire's evolved detonation splashes this fraction of the turret's per-hit " +
                 "damage to every enemy within ConeRange of the struck target, on EVERY shot. " +
                 "Because it scales off the turret's damage, the explosion keeps up with upgrades " +
                 "and enemy HP scaling. 0.6 = 60% of a normal hit to each nearby enemy. The flat " +
                 "ConeDPS from turret data acts as an early-game floor when set.")]
        [SerializeField, Range(0f, 2f)] private float _blastfireAreaDamagePercent = 0.6f;

        [Header("Upgrade Visual Scale")]
        [Tooltip("If enabled, the turret model starts at Min Scale Multiplier and grows only during the first Scale Steps upgrades.")]
        [SerializeField] private bool _scaleTurretOnUpgrade = true;

        [Tooltip("Optional visual/model root to scale. Leave empty to scale this turret object. Assign a child model root if you do not want colliders, range visuals or UI children to scale.")]
        [SerializeField] private Transform _upgradeScaleRoot;

        [Tooltip("Visual scale multiplier at turret level 1. Use values below 1 to make newly placed turrets start smaller.")]
        [SerializeField, Min(0.01f)] private float _upgradeMinScaleMultiplier = 0.85f;

        [Tooltip("Maximum visual scale multiplier reached after Scale Steps upgrades. Examples: 1 or 1.2.")]
        [SerializeField, Min(0.01f)] private float _upgradeMaxScaleMultiplier = 1.2f;

        [Tooltip("How many upgrades are allowed to increase visual scale. Formula per upgrade: (Max Scale - Min Scale) / Scale Steps.")]
        [SerializeField, Min(1)] private int _upgradeScaleSteps = 10;

        [Tooltip("Play a small scale pop when a turret is upgraded.")]
        [SerializeField] private bool _animateUpgradeScale = true;

        [Tooltip("Duration of the upgrade scale pop animation.")]
        [SerializeField, Min(0.01f)] private float _upgradeScaleAnimationDuration = 0.16f;

        [Header("Performance")]
        [Tooltip("How often turrets refresh target search. Lower is more responsive, higher is cheaper.")]
        [SerializeField] private float _targetSearchInterval = 0.16f;

        [Tooltip("How often support turrets refresh aura targets. Keep below aura grace duration.")]
        [SerializeField] private float _supportUpdateInterval = 0.15f;

        [Tooltip("How often radar turrets refresh stealth reveal checks.")]
        [SerializeField] private float _radarUpdateInterval = 0.25f;

        [Tooltip("Minimum interval between expensive combat ticks. 0.02 = 50 Hz.")]
        [SerializeField] private float _combatUpdateInterval = 0.02f;

        [Header("Laser Performance")]
        [Tooltip("Laser damage is integrated at this rate instead of every scheduler tick. 0.05 = 20 damage ticks/sec.")]
        [SerializeField] private float _laserDamageTickInterval = 0.05f;

        [Tooltip("How often evolved multi-target lasers refresh their target list.")]
        [SerializeField] private float _multiLaserTargetRefreshInterval = 0.12f;

        [Tooltip("Prevents a long frame from applying an enormous laser damage chunk at once.")]
        [SerializeField] private float _maxLaserDamageStep = 0.15f;

        [Tooltip("How often laser-only conditional status/elite damage multipliers refresh for the same target.")]
        [SerializeField, Min(0.02f)] private float _laserConditionalModifierRefreshInterval = 0.10f;

        [Tooltip("Laser refraction spill damage is accumulated and resolved at this cadence instead of querying neighbours every laser tick.")]
        [SerializeField, Min(0.03f)] private float _laserRefractionTickInterval = 0.12f;

        [Tooltip("How often laser progress/achievement telemetry is published. Progress uses accumulated time, so higher values are cheaper but still accurate.")]
        [SerializeField, Min(0.05f)] private float _laserChallengeTelemetryInterval = 0.25f;

        [Tooltip("Laser beams already provide continuous feedback. Keep this disabled for laptop builds to avoid FlashOnHit work every laser tick.")]
        [SerializeField] private bool _flashLaserHitVFX = false;

        [Tooltip("Primary laser damage numbers are expensive when many laser turrets tick together. Disable for best performance.")]
        [SerializeField] private bool _showPrimaryLaserDamageNumbers = false;

        [Tooltip("Secondary/multi/refraction laser damage numbers are usually visual noise and very expensive. Keep disabled for best performance.")]
        [SerializeField] private bool _showSecondaryLaserDamageNumbers = false;

        [Header("Laser Profiling")]
        [Tooltip("Keep disabled while profiling bottlenecks. When disabled, the smaller Turret.AttackLaser.* markers appear directly in the Profiler instead of being hidden under Turret.AttackLaser.Total.")]
        [SerializeField] private bool _profileOuterLaserTotal = false;

        [Tooltip("Projectile turrets keep a valid target for this long before looking for a better one. Higher values reduce enemy scans.")]
        [SerializeField] private float _validTargetRetargetInterval = 0.35f;

        [Tooltip("Continuous beam turrets retarget more often so the beam feels responsive.")]
        [SerializeField] private float _laserRetargetInterval = 0.12f;

        [Header("High-Load Combat LOD")]
        [Tooltip("Maximum number of visible projectile GameObjects emitted by one logical attack. Remaining shots still deal full damage/status immediately.")]
        [SerializeField, Min(0)] private int _maxVisualProjectilesPerAttack = 1;

        [Tooltip("How often nearby-enemy attack-speed scaling is refreshed. Between refreshes the computed speed is cached.")]
        [SerializeField, Min(0.03f)] private float _nearbyEnemyAttackSpeedRefreshInterval = 0.15f;

        [Header("Evolved Attack Performance")]
        [Tooltip("Hard cap for enemies affected by a single evolved area effect. Keeps dense late-wave area attacks bounded.")]
        [SerializeField, Min(1)] private int _maxEvolvedAreaTargets = 48;

        [Tooltip("How often evolved area attacks refresh their nearby-enemy candidate cache.")]
        [SerializeField, Min(0.03f)] private float _evolvedAreaTargetRefreshInterval = 0.12f;

        [Tooltip("Maximum cadence used to renew evolved slow/burn statuses. Existing durations remain uninterrupted.")]
        [SerializeField, Min(0.03f)] private float _evolvedStatusReapplyInterval = 0.35f;

        [Header("Lightning Performance")]
        [Tooltip("Maximum visible arc segments spawned by one chain attack. All chain damage still resolves logically.")]
        [SerializeField, Min(0)] private int _maxChainVisualsPerAttack = 4;

        [Tooltip("Secondary chain hits can create hundreds of damage-number events in dense waves. Keep this disabled for laptop/high-quality targets.")]
        [SerializeField] private bool _showSecondaryChainDamageNumbers = false;

        // Current stats (base + upgrades + buffs + tile + run modifiers)
        public float Damage { get; private set; }
        public float AttackSpeed { get; private set; } // attacks per second
        public float Range { get; private set; }
        public int Level { get; private set; } = 1;
        public int TotalGoldInvested { get; private set; }
        public bool IsEvolved { get; private set; }
        public bool IsEvolvedTier2 { get; private set; }
        public TileSpecialty TileSpecialty { get; private set; }
        public DynamicTileData DynamicTile { get; private set; }
        public int EvolutionPath { get; private set; } = -1;

        /// <summary>Current laser ramp damage multiplier (1 = no ramp). UI-facing.</summary>
        public float LaserRampMultiplier => _laserStackMultiplier;

        /// <summary>Maximum laser ramp multiplier for this turret's current form. UI-facing.</summary>
        public float LaserRampCapMultiplier =>
            _cachedIsStackingLaser && Data != null && Data.PathB != null
                ? 1f + Data.PathB.StackingCap
                : 1f + Mathf.Max(0f, _innateLaserRampCap);

        /// <summary>True for any laser form that ramps (innate or Stacker evolution). UI-facing.</summary>
        public bool HasLaserRamp => Data != null && Data.IsContinuousBeam &&
            (_cachedIsStackingLaser || _innateLaserRampPerSecond > 0f);

        // Targeting
        private TargetingMode _targetingMode;
        private float _attackTimer;
        private EnemyController _currentTarget;
        private float _targetSearchTimer;
        [SerializeField] Transform _rotationPart;
        [SerializeField] Vector3 _rotationOffset;
        private readonly List<EnemyController> _enemiesInRange = new(16);
        private readonly List<EnemyController> _radarRevealedEnemies = new(16);

        // Support turret
        private readonly List<TurretController> _turretsInRange = new(8);

        // Buff tracking
        //private float _externalDamageMultiplier = 1f;
        //private float _externalSpeedMultiplier = 1f;

        [SerializeField] private float _externalEffectGraceSeconds = 0.20f;

        // Support aura bonuses. These are positive values like 0.18 = +18%.
        private float _supportDamageBonus;
        private float _supportSpeedBonus;
        private float _supportAuraExpireTime;

        // Enemy aura penalty. Positive value like 0.18 = -18% damage.
        private float _enemyDamageDebuff;
        private float _enemyDebuffExpireTime;


        // Tile
        private TileSpecialty _tileSpecialty;
        private DynamicTileData _dynamicTile;

        // Laser stacking / crit window
        private float _laserStackMultiplier = 1f;
        private float _pendingExecuteHpDamage;
        private EnemyController _lastLaserTarget;
        private float _laserCritWindowTimer;
        private bool _laserCritActive;
        private bool _lastDamageRollWasCritical;

        // References
        private EnemyManager _enemyManager;
        private TurretManager _turretManager;
        private ProjectileManager _projectileManager;
        private BeamRenderer _beamRenderer;
        private TurretVisualConfig _visualConfig;
        private TurretVFXConfig _vfxConfig;
        private TurretMuzzleVFX _muzzleVFX;
        private TurretSoundConfig _soundConfig;
        private ChainLightningRenderer _chainRenderer;
        private IRunStatModifiers _statModifiers;

        private float _laserPublishTimer = 0f;
        private int _shotCounter;
        private readonly List<EnemyController> _specialTargets = new(16);
        private readonly List<int> _chainHitEnemyIds = new(8);
        private EnemyController[] _multiLaserTargets = new EnemyController[4];
        private float _supportTickTimer;
        private float _radarTickTimer;
        private float _combatTickAccumulator;
        private float _cachedRangeSqr;
        private float _aimDotThreshold;
        private bool _laserStateActive;
        private float _nextAimRefreshTime;
        private int _cachedAimTargetRuntimeId = -1;
        private bool _cachedAimAligned = true;

        // Projectile spawn cache. These values used to be pulled from component
        // configs on every shot. At high waves that cost is visible in the Profiler.
        private GameObject _cachedProjectilePrefab;
        private ProjectileManager.ProjectilePoolHandle _cachedProjectilePoolHandle;
        private Transform[] _cachedFirePoints = System.Array.Empty<Transform>();
        private bool _cachedHasFirePoints;
        private int _cachedFirePointCount = 1;
        private GameObject _cachedHitVFX;
        private int _visualProjectileSequence;

        // Laser damage/query work is intentionally decoupled from beam visuals.
        // This lets laser turrets look smooth while avoiding expensive target/VFX/
        // damage work every scheduler tick.
        private float _laserDamageAccumulator;
        private float _multiLaserTargetRefreshAccumulator;
        private int _multiLaserTargetCount;
        private EnemyController[] _multiLaserScratch = new EnemyController[4];
        private float[] _multiLaserDamageMultipliers = new float[4];
        private bool _cachedIsMultiLaser;
        private bool _cachedIsStackingLaser;
        private int _cachedRequiredMultiLaserTargets = 1;
        private float _cachedMultiLaserDamagePercent;
        private float _cachedLaserRefractionPercent;
        private bool _hasLaserRefraction;
        private float _laserRefractionTimer;
        private float _laserRefractionDamageAccumulator;
        private int _cachedLaserModifierTargetRuntimeId = -1;
        private float _nextLaserConditionalModifierRefreshTime;
        private float _cachedLaserConditionalDamageMultiplier = 1f;
        private float _cachedLaserDamageTickInterval = 0.05f;
        private float _cachedMaxLaserDamageStep = 0.15f;
        private float _cachedMultiLaserTargetRefreshInterval = 0.12f;
        private float _cachedLaserRefractionTickInterval = 0.12f;
        private float _cachedLaserChallengeTelemetryInterval = 0.25f;
        private ILaserHitTelemetrySink _cachedLaserTelemetrySink;

        // Cached Unity references / evolution facts keep the attack hot path branch-light.
        private Transform _cachedTransform;
        private Transform _cachedRotationPart;
        private float _rotationSpeedRadians;
        private float _cachedEffectiveAttackSpeed;
        private float _nextEffectiveAttackSpeedRefreshTime;
        private bool _effectiveAttackSpeedCached;

        private Transform _cachedUpgradeScaleRoot;
        private Vector3 _baseUpgradeVisualScale = Vector3.one;
        private bool _baseUpgradeVisualScaleCached;
        private Coroutine _upgradeScaleRoutine;

        // Upgrade scale progression is visual-only. For evolved turrets it must
        // restart from the evolved form, even though gameplay Level is preserved.
        private int _upgradeVisualScaleStartLevel = 1;

        private TurretEvolutionData _cachedEvolution;
        // v1.0 level-25 shared second evolution tier. Kept separate from
        // _cachedEvolution (Path A/B) rather than folding into it, since Tier2 only
        // contributes a small, explicitly-summed set of bonuses (currently
        // HitCurrentHPPercent) instead of replacing the Path's own cached fields.
        private TurretEvolutionData _cachedTier2Evolution;
        private bool _hasPostAttackEvolutionEffects;
        private bool _cachedDoubleProjectile;
        private float _cachedDoubleProjectileDamageMultiplier = 1f;

        // Evolved Frost/Inferno area-effect caches. The source target can move, so
        // cache validity also checks source displacement instead of time alone.
        private readonly List<EnemyController> _evolvedAreaSlowTargets = new(24);
        private readonly List<EnemyController> _evolvedAreaDamageTargets = new(24);
        private EnemyController _areaSlowAnchorTarget;
        private EnemyController _areaDamageAnchorTarget;
        private Vector3 _areaSlowAnchorPosition;
        private Vector3 _areaDamageAnchorPosition;
        private float _nextAreaSlowTargetRefreshTime;
        private float _nextAreaDamageTargetRefreshTime;
        private float _nextAreaSlowApplyTime;
        private float _nextAreaBurnApplyTime;

        private const float EvolvedAreaAnchorRequeryDistanceSqr = 0.36f; // 0.6 metres

        private bool IsLaserLike => Data != null && (Data.IsContinuousBeam || Data.Type == TurretType.Laser);

        internal float GetManagedUpdateInterval()
        {
            if (Data == null)
                return 0.10f;

            if (Data.Type == TurretType.Support)
            {
                // Static support aura membership is handled by SupportAuraSystem only
                // when the turret topology or an aura stat changes. Keep this turret's
                // own scheduler tick rare; it only maintains lightweight visual state.
                return 1f;
            }

            if (Data.Type == TurretType.Radar)
                return Mathf.Max(0.08f, _radarUpdateInterval);

            if (IsLaserLike)
                return Mathf.Max(0.01f, _combatUpdateInterval);

            // Projectile turrets do not need frame-by-frame logic. They receive
            // accumulated dt on each scheduled tick, so attack cooldowns remain
            // accurate while target search/rotation work happens much less often.
            float attackInterval = AttackSpeed > 0.01f ? 1f / AttackSpeed : 0.20f;
            return Mathf.Clamp(Mathf.Min(_targetSearchInterval, attackInterval * 0.5f), 0.035f, 0.085f);
        }

        // =================================================================
        // INITIALIZATION
        // =================================================================

        private void Awake()
        {
            _cachedTransform = transform;
            _cachedRotationPart = _rotationPart;
            _rotationSpeedRadians = Mathf.Max(0f, _rotationSpeed) * Mathf.Deg2Rad;
            CacheUpgradeVisualScaleBase();
        }

        private Transform GetUpgradeScaleRoot()
        {
            if (_cachedUpgradeScaleRoot == null)
                _cachedUpgradeScaleRoot = _upgradeScaleRoot != null ? _upgradeScaleRoot : transform;

            return _cachedUpgradeScaleRoot;
        }

        private void CacheUpgradeVisualScaleBase()
        {
            Transform scaleRoot = GetUpgradeScaleRoot();
            if (scaleRoot == null)
                return;

            if (!_baseUpgradeVisualScaleCached)
            {
                _baseUpgradeVisualScale = scaleRoot.localScale;
                _baseUpgradeVisualScaleCached = true;
            }
        }

        private void ApplyUpgradeVisualScale(bool animate)
        {
            Transform scaleRoot = GetUpgradeScaleRoot();
            if (scaleRoot == null)
                return;

            CacheUpgradeVisualScaleBase();

            Vector3 targetScale = CalculateUpgradeVisualScale();

            if (_upgradeScaleRoutine != null)
            {
                StopCoroutine(_upgradeScaleRoutine);
                _upgradeScaleRoutine = null;
            }

            if (animate && _animateUpgradeScale && isActiveAndEnabled && gameObject.activeInHierarchy)
                _upgradeScaleRoutine = StartCoroutine(AnimateUpgradeVisualScale(scaleRoot, targetScale));
            else
                scaleRoot.localScale = targetScale;
        }

        private Vector3 CalculateUpgradeVisualScale()
        {
            if (!_baseUpgradeVisualScaleCached)
                _baseUpgradeVisualScale = Vector3.one;

            if (!_scaleTurretOnUpgrade)
                return _baseUpgradeVisualScale;

            int steps = Mathf.Max(1, _upgradeScaleSteps);

            // Base turrets count visual growth from level 1.
            // Evolved turrets reset this start level at the moment of evolution,
            // so they begin from the evolved prefab/controller's own min scale
            // and grow only from upgrades made after evolution.
            int startLevel = Mathf.Max(1, _upgradeVisualScaleStartLevel);
            int appliedUpgradeSteps = Mathf.Clamp(Level - startLevel, 0, steps);

            float minMultiplier = Mathf.Max(0.01f, _upgradeMinScaleMultiplier);
            float maxMultiplier = Mathf.Max(minMultiplier, _upgradeMaxScaleMultiplier);
            float scalePerUpgrade = (maxMultiplier - minMultiplier) / steps;
            float multiplier = minMultiplier + (scalePerUpgrade * appliedUpgradeSteps);

            return _baseUpgradeVisualScale * multiplier;
        }

        private IEnumerator AnimateUpgradeVisualScale(Transform scaleRoot, Vector3 targetScale)
        {
            Vector3 startScale = scaleRoot != null ? scaleRoot.localScale : targetScale;
            float duration = Mathf.Max(0.01f, _upgradeScaleAnimationDuration);
            float halfDuration = duration * 0.5f;
            Vector3 popScale = Vector3.LerpUnclamped(startScale, targetScale, 1.12f);

            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                if (scaleRoot == null)
                    yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                t = t * t * (3f - 2f * t);
                scaleRoot.localScale = Vector3.Lerp(startScale, popScale, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                if (scaleRoot == null)
                    yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                t = t * t * (3f - 2f * t);
                scaleRoot.localScale = Vector3.Lerp(popScale, targetScale, t);
                yield return null;
            }

            if (scaleRoot != null)
                scaleRoot.localScale = targetScale;

            _upgradeScaleRoutine = null;
        }

        private void RefreshEvolutionAttackCache()
        {
            _cachedEvolution = IsEvolved && Data != null
                ? (EvolutionPath == 0 ? Data.PathA : Data.PathB)
                : null;

            _cachedDoubleProjectile = _cachedEvolution != null && _cachedEvolution.DoubleProjectile;
            _cachedDoubleProjectileDamageMultiplier = _cachedDoubleProjectile
                ? Mathf.Max(0f, _cachedEvolution.PerShotDamagePercent)
                : 1f;

            _hasPostAttackEvolutionEffects = _cachedEvolution != null &&
                (_cachedEvolution.FreezeOnHit || _cachedEvolution.AreaSlow ||
                 _cachedEvolution.AreaDamage || _cachedEvolution.StackingBurn);

            _cachedIsMultiLaser = _cachedEvolution != null && EvolutionPath == 0 && _cachedEvolution.MultiTargetCount > 0;
            _cachedRequiredMultiLaserTargets = _cachedIsMultiLaser
                ? Mathf.Max(1, _cachedEvolution.MultiTargetCount + 1)
                : 1;
            _cachedMultiLaserDamagePercent = _cachedIsMultiLaser
                ? Mathf.Max(0f, _cachedEvolution.MultiTargetDamagePercent)
                : 0f;
            _cachedIsStackingLaser = _cachedEvolution != null && EvolutionPath == 1 && _cachedEvolution.StackingDamage;

            _cachedTier2Evolution = IsEvolvedTier2 && Data != null ? Data.Tier2 : null;

            _areaSlowAnchorTarget = null;
            _areaDamageAnchorTarget = null;
            _evolvedAreaSlowTargets.Clear();
            _evolvedAreaDamageTargets.Clear();
            _nextAreaSlowTargetRefreshTime = 0f;
            _nextAreaDamageTargetRefreshTime = 0f;
            _nextAreaSlowApplyTime = 0f;
            _nextAreaBurnApplyTime = 0f;
        }

        public void Initialize(TurretData data, Vector2Int gridPos,
            TileSpecialty tileSpecialty, DynamicTileData dynamicTile = null)
        {
            Data = data;
            GridPosition = gridPos;
            _tileSpecialty = tileSpecialty;
            TileSpecialty = tileSpecialty;
            _dynamicTile = dynamicTile;
            DynamicTile = dynamicTile;
            _targetingMode = data.DefaultTargeting;
            _upgradeVisualScaleStartLevel = 1;
            _cachedTransform ??= transform;
            _cachedRotationPart = _rotationPart;
            _rotationSpeedRadians = Mathf.Max(0f, _rotationSpeed) * Mathf.Deg2Rad;
            RefreshEvolutionAttackCache();

            _enemyManager = ServiceLocator.Get<EnemyManager>();
            _turretManager = ServiceLocator.Get<TurretManager>();
            ServiceLocator.TryGet(out _projectileManager);
            ServiceLocator.TryGet(out _statModifiers);

            _beamRenderer = GetComponentInChildren<BeamRenderer>();
            _visualConfig = GetComponent<TurretVisualConfig>();
            _vfxConfig = GetComponent<TurretVFXConfig>();
            _muzzleVFX = GetComponent<TurretMuzzleVFX>();
            _soundConfig = GetComponent<TurretSoundConfig>();
            _chainRenderer = GetComponent<ChainLightningRenderer>();
            RefreshProjectileCache();
            _vfxConfig?.SpawnPlace();
            _soundConfig?.PlayPlace();

            RecalculateStats();
            ApplyUpgradeVisualScale(false);

            if (Data.Type == TurretType.Support)
                EnsureSupportAuraVisual();

            // Stagger expensive periodic work so newly placed/restored turrets do
            // not all scan enemies on the same frame.
            float stagger = (InstanceId % 17) / 17f;
            _targetSearchTimer = Mathf.Max(0.02f, _targetSearchInterval) * stagger;
            _supportTickTimer = Mathf.Max(0.05f, _supportUpdateInterval) * stagger;
            _radarTickTimer = Mathf.Max(0.05f, _radarUpdateInterval) * stagger;
            _combatTickAccumulator = Mathf.Max(0f, _combatUpdateInterval) * stagger;
        }

        private void OnEnable()
        {
            TurretUpdateScheduler.Register(this);
        }

        private void OnDisable()
        {
            ReleaseRadarReveals();
            TurretUpdateScheduler.Unregister(this);
        }

        private void RefreshProjectileCache()
        {
            if (_visualConfig == null)
                _visualConfig = GetComponent<TurretVisualConfig>();
            if (_vfxConfig == null)
                _vfxConfig = GetComponent<TurretVFXConfig>();

            _cachedProjectilePrefab = _visualConfig != null
                ? _visualConfig.GetProjectilePrefab(IsEvolved)
                : null;

            var firePoints = _visualConfig != null
                ? _visualConfig.GetFirePoints(IsEvolved)
                : null;

            _cachedFirePoints = firePoints ?? System.Array.Empty<Transform>();
            _cachedHasFirePoints = _cachedFirePoints.Length > 0;
            _cachedFirePointCount = _cachedHasFirePoints ? _cachedFirePoints.Length : 1;
            _cachedHitVFX = _vfxConfig != null ? _vfxConfig.HitVFX : null;

            // Cache the pool handle once. The hot fire path then avoids repeated
            // GetInstanceID + Dictionary lookup for every logical projectile.
            _cachedProjectilePoolHandle = _projectileManager != null
                ? _projectileManager.GetPoolHandle(_cachedProjectilePrefab)
                : default;

            // Spread adaptive visual-LOD phases between turrets so they do not all
            // skip or render a projectile on the same attack frame.
            _visualProjectileSequence = Mathf.Abs(InstanceId * 31);

            // Warm this visual once when placed/evolved/restored so the first shot
            // does not pay an Instantiate spike inside SpawnProjectile.
            _projectileManager?.WarmPool(_cachedProjectilePrefab);
        }

        // =================================================================
        // STAT CALCULATION
        // =================================================================

        public void RecalculateStats()
        {
            // Stat changes invalidate nearby-enemy attack-speed cache.
            _effectiveAttackSpeedCached = false;

            // Base stats + per-level scaling.
            // Damage now scales multiplicatively so turret upgrades can keep up
            // with enemy health scaling, which uses Mathf.Pow(HealthScalePerWave, wave).
            int levelIndex = Mathf.Max(0, Level - 1);
            float baseDamageSource = Data.Damage;
            float baseInterval = Data.AttackInterval - (Data.AttackSpeedPerLevel * levelIndex);
            baseInterval = Mathf.Max(_minimumProjectileAttackInterval, baseInterval);
            float baseRng = Data.Range + (Data.RangePerLevel * levelIndex);

            // Evolution stat overrides
            if (IsEvolved)
            {
                var evo = EvolutionPath == 0 ? Data.PathA : Data.PathB;
                if (evo != null)
                {
                    if (evo.DamageOverride >= 0) baseDamageSource = evo.DamageOverride;
                    if (evo.AttackIntervalOverride >= 0) baseInterval = evo.AttackIntervalOverride - (Data.AttackSpeedPerLevel * levelIndex);
                    if (evo.RangeOverride >= 0) baseRng = evo.RangeOverride + (Data.RangePerLevel * levelIndex);
                    baseInterval = Mathf.Max(_minimumProjectileAttackInterval, baseInterval);
                }
            }

            float baseDmg = CalculateMultiplicativeUpgradeDamage(
                baseDamageSource,
                Data.DamagePerLevel,
                levelIndex);

            float baseSpd = Data.IsContinuousBeam ? 1f : (baseInterval > 0 ? 1f / baseInterval : 1f);

            // Dynamic tile modifiers
            if (_dynamicTile != null)
            {
                ApplyTileMod(ref baseDmg, ref baseSpd, ref baseRng, _dynamicTile.PrimaryEffect);
                ApplyTileMod(ref baseDmg, ref baseSpd, ref baseRng, _dynamicTile.Tradeoff);
            }

            // Optional run-wide modifiers. These are safe even if your current
            // IRunStatModifiers only implements some of these methods; remove the
            // unavailable calls if your interface is smaller.
            if (_statModifiers != null)
            {
                baseDmg *= Mathf.Max(0f, _statModifiers.GetGlobalDamageMultiplier());
                baseSpd *= Mathf.Max(0f, _statModifiers.GetGlobalAttackSpeedMultiplier());
                baseRng *= Mathf.Max(0f, _statModifiers.GetGlobalRangeMultiplier());
                if (Data.Type == TurretType.Support)
                    baseRng *= Mathf.Max(0f, _statModifiers.GetSupportRadiusMultiplier());
            }

            _cachedLaserRefractionPercent = _statModifiers != null
                ? Mathf.Clamp01(_statModifiers.GetLaserRefractionPercent())
                : 0f;
            _hasLaserRefraction = _cachedLaserRefractionPercent > 0.0001f;

            // External buffs (support turrets)
            float supportDamageMultiplier = 1f + Mathf.Max(0f, _supportDamageBonus);
            float supportSpeedMultiplier = 1f + Mathf.Max(0f, _supportSpeedBonus);
            float enemyDebuffMultiplier = 1f - Mathf.Clamp01(_enemyDamageDebuff);

            Damage = baseDmg * supportDamageMultiplier * enemyDebuffMultiplier;
            AttackSpeed = baseSpd * supportSpeedMultiplier;
            if (!Data.IsContinuousBeam && Data.Type != TurretType.Laser)
                AttackSpeed = Mathf.Min(AttackSpeed, _maxProjectileAttackSpeed);

            Range = Mathf.Max(0.1f, baseRng);
            _cachedRangeSqr = Range * Range;
            _aimDotThreshold = Mathf.Cos(Mathf.Clamp(_aimThreshold, 0f, 180f) * Mathf.Deg2Rad);
            RefreshLaserPerformanceCache();

            //Damage = baseDmg * _externalDamageMultiplier;
            //AttackSpeed = baseSpd * _externalSpeedMultiplier;
            //Range = baseRng;
            
        }


        private void RefreshLaserPerformanceCache()
        {
            _cachedLaserDamageTickInterval = Mathf.Max(0.01f, _laserDamageTickInterval);
            _cachedMaxLaserDamageStep = Mathf.Max(_cachedLaserDamageTickInterval, _maxLaserDamageStep);
            _cachedMultiLaserTargetRefreshInterval = Mathf.Max(0.04f, _multiLaserTargetRefreshInterval);
            _cachedLaserRefractionTickInterval = Mathf.Max(0.03f, _laserRefractionTickInterval);
            _cachedLaserChallengeTelemetryInterval = Mathf.Max(0.05f, _laserChallengeTelemetryInterval);
        }

        private float CalculateMultiplicativeUpgradeDamage(float baseDamage, float damagePerLevel, int levelIndex)
        {
            baseDamage = Mathf.Max(0f, baseDamage);
            if (levelIndex <= 0 || damagePerLevel <= 0f)
                return baseDamage;

            float perLevelBonus = GetDamageGrowthPercentPerLevel(baseDamage, damagePerLevel);
            return baseDamage * Mathf.Pow(1f + perLevelBonus, levelIndex);
        }

        private float GetDamageGrowthPercentPerLevel(float baseDamage, float damagePerLevel)
        {
            if (damagePerLevel <= 0f)
                return 0f;

            float bonus;

            if (damagePerLevel > 1f)
            {
                // Backward-compatible interpretation for existing assets.
                // Old data was authored as flat damage: base 10, DamagePerLevel 2.
                // New scaling converts that first upgrade into +20% per level.
                bonus = damagePerLevel / Mathf.Max(1f, baseDamage);
            }
            else
            {
                // New data can be authored directly as percent: 0.08 = +8% per level.
                bonus = damagePerLevel;
            }

            // Safety fallback: if an existing prefab deserializes the new field as 0,
            // still use the intended default cap instead of disabling scaling.
            float maxGrowth = _maxMultiplicativeDamageGrowthPerLevel > 0f
                ? _maxMultiplicativeDamageGrowthPerLevel
                : 0.25f;

            return Mathf.Clamp(bonus, 0f, maxGrowth);
        }

        private void ApplyTileMod(ref float damage, ref float speed, ref float range,
            TurretStatModifier mod)
        {
            switch (mod.Stat)
            {
                case TurretStatModifier.StatType.Damage:
                    damage *= (1f + mod.Value);
                    break;
                case TurretStatModifier.StatType.AttackSpeed:
                    speed *= (1f + mod.Value);
                    break;
                case TurretStatModifier.StatType.Range:
                    range *= (1f + mod.Value);
                    break;
            }
        }

        // =================================================================
        // CRITICAL HIT HELPERS
        // =================================================================

        private float GetCritChance()
        {
            if (_statModifiers == null) return 0f;
            return Mathf.Clamp01(_statModifiers.GetCritChance());
        }

        private float GetCritDamageMultiplier()
        {
            float multiplier = Mathf.Max(1f, _baseCritDamageMultiplier);

            if (_cachedEvolution != null && _cachedEvolution.CritDamageBonus > 0f)
                multiplier += _cachedEvolution.CritDamageBonus;

            return multiplier;
        }

        private bool RollCriticalHit()
        {
            float chance = GetCritChance();
            if (chance <= 0f) return false;

            bool isCrit = Random.value < chance;
            if (isCrit)
            {
                _statModifiers?.NotifyCriticalHit();
                EventBus.Publish(new CriticalHitEvent { });
            }


            return isCrit;
        }

        private float ApplyCriticalDamage(float baseDamage)
        {
            _lastDamageRollWasCritical = RollCriticalHit();
            return _lastDamageRollWasCritical
                ? baseDamage * GetCritDamageMultiplier()
                : baseDamage;
        }

        private void ResetLaserCritWindow()
        {
            _laserCritWindowTimer = 0f;
            _laserCritActive = false;
        }

        private float GetCurrentLaserCritMultiplier(float dt)
        {
            _laserCritWindowTimer -= dt;

            if (_laserCritWindowTimer <= 0f)
            {
                _laserCritWindowTimer = Mathf.Max(0.05f, _laserCritWindowSeconds);
                _laserCritActive = RollCriticalHit();
            }

            return _laserCritActive ? GetCritDamageMultiplier() : 1f;
        }

        // =================================================================
        // UPDATE LOOP
        // =================================================================

        internal void ManagedUpdate(float dt, float now)
        {
            if (Data == null)
                return;

            Profiler.BeginSample("Turret.ManagedUpdate.RefreshExternalEffects");
            RefreshExternalEffects(now);
            Profiler.EndSample();

            if (Data.Type == TurretType.Support)
            {
                Profiler.BeginSample("Turret.ManagedUpdate.Support");
                UpdateSupport(dt);
                Profiler.EndSample();
                return;
            }

            if (Data.Type == TurretType.Radar)
            {
                Profiler.BeginSample("Turret.ManagedUpdate.Radar");
                UpdateRadar(dt);
                Profiler.EndSample();
                return;
            }

            _combatTickAccumulator -= dt;
            if (_combatTickAccumulator > 0f)
                return;

            float combatDt = Mathf.Max(dt, _combatUpdateInterval - _combatTickAccumulator);
            _combatTickAccumulator = Mathf.Max(0.005f, _combatUpdateInterval);

            Profiler.BeginSample("Turret.ManagedUpdate.Combat");
            UpdateCombat(combatDt);
            Profiler.EndSample();
        }

        private void UpdateCombat(float dt)
        {
            _attackTimer -= dt;
            _targetSearchTimer -= dt;

            bool targetValid;
            Profiler.BeginSample("Turret.Combat.ValidateTarget");
            targetValid = IsTargetStillValid(_currentTarget);
            Profiler.EndSample();

            if (!targetValid)
            {
                Profiler.BeginSample("Turret.Combat.FindTarget.Invalid");
                _currentTarget = FindTarget();
                Profiler.EndSample();
                _targetSearchTimer = Mathf.Max(0.02f, _targetSearchInterval);
            }
            else if (_targetSearchTimer <= 0f)
            {
                // Valid projectile targets are intentionally kept longer. This avoids
                // every turret scanning every active enemy 10+ times per second.
                _targetSearchTimer = IsLaserLike
                    ? Mathf.Max(0.02f, _laserRetargetInterval)
                    : Mathf.Max(0.05f, _validTargetRetargetInterval);

                Profiler.BeginSample("Turret.Combat.FindTarget.Retarget");
                EnemyController betterTarget = FindTarget();
                Profiler.EndSample();
                if (betterTarget != null)
                    _currentTarget = betterTarget;
            }

            if (_currentTarget == null)
            {
                if (IsLaserLike)
                {
                    Profiler.BeginSample("Turret.Combat.ClearLaserState");
                    ClearLaserState();
                    Profiler.EndSample();
                }
                return;
            }

            Profiler.BeginSample("Turret.Combat.AimAndAttack");
            AimAndAttack(_currentTarget, dt);
            Profiler.EndSample();
        }

        private void AimAndAttack(EnemyController target, float dt)
        {
            bool isAimed = true;
            Transform rotationPart = _cachedRotationPart != null ? _cachedRotationPart : _rotationPart;

            if (rotationPart != null)
                isAimed = UpdateAimCached(rotationPart, target, dt);

            if (IsLaserLike)
            {
                if (isAimed)
                    AttackLaser(target, dt);
                else
                    ClearLaserState();

                return;
            }

            if (!isAimed || _attackTimer > 0f)
                return;

            Attack(target);
            float effectiveAttackSpeed = GetEffectiveAttackSpeed();
            _attackTimer = 1f / Mathf.Max(0.01f, effectiveAttackSpeed);
        }

        private bool UpdateAimCached(Transform rotationPart, EnemyController target, float dt)
        {
            if (target == null || rotationPart == null)
                return false;

            float now = Time.time;
            int targetRuntimeId = target.RuntimeId;
            bool sameTarget = _cachedAimTargetRuntimeId == targetRuntimeId;

            float refreshInterval = IsLaserLike
                ? Mathf.Max(0.005f, _laserAimRefreshInterval)
                : (_cachedAimAligned
                    ? Mathf.Max(0.005f, _lockedProjectileAimRefreshInterval)
                    : Mathf.Max(0.005f, _projectileAimRefreshInterval));

            if (sameTarget && now < _nextAimRefreshTime)
                return _cachedAimAligned;

            _cachedAimTargetRuntimeId = targetRuntimeId;
            _nextAimRefreshTime = now + refreshInterval;

            Vector3 origin = rotationPart.position;
            Vector3 targetPosition = target.hitTransform != null ? target.hitTransform.position : target.transform.position;
            float dx = targetPosition.x - origin.x;
            float dz = targetPosition.z - origin.z;
            float sqrMagnitude = (dx * dx) + (dz * dz);
            if (sqrMagnitude < 0.001f)
            {
                _cachedAimAligned = false;
                return false;
            }

            float invMagnitude = 1f / Mathf.Sqrt(sqrMagnitude);
            Vector3 targetDirection = new Vector3(dx * invMagnitude, 0f, dz * invMagnitude);
            Vector3 currentForward = rotationPart.forward;
            float alignment = (currentForward.x * targetDirection.x) + (currentForward.z * targetDirection.z);

            // Most turrets are already aligned while firing. Do not call
            // RotateTowards/Euler every combat tick in that common case.
            if (alignment < _aimDotThreshold)
            {
                float rotateRadians = _rotationSpeedRadians * Mathf.Max(0f, dt);
                if (rotateRadians > 0f)
                {
                    rotationPart.forward = Vector3.RotateTowards(
                        currentForward, targetDirection, rotateRadians, 0f);

                    if (_rotationOffset != Vector3.zero)
                        rotationPart.rotation = Quaternion.Euler(rotationPart.eulerAngles + _rotationOffset);

                    Vector3 newForward = rotationPart.forward;
                    alignment = (newForward.x * targetDirection.x) + (newForward.z * targetDirection.z);
                }
            }

            _cachedAimAligned = alignment >= _aimDotThreshold;
            return _cachedAimAligned;
        }

        private EnemyController FindTarget()
        {
            if (_enemyManager == null) return null;

            Profiler.BeginSample("Turret.FindTarget.EnemyManager.GetBestEnemyInRange");
            Vector3 origin = _cachedTransform != null ? _cachedTransform.position : transform.position;
            // Combat turrets always target something; None only applies to Support/Radar,
            // which never reach FindTarget. Fall back to First just in case.
            TargetingMode mode = _targetingMode == TargetingMode.None ? TargetingMode.First : _targetingMode;
            EnemyController target = _enemyManager.GetBestEnemyInRange(
                origin, Range, Data.CanTargetStealth, mode);
            Profiler.EndSample();
            return target;
        }

        // =================================================================
        // TARGETING MODE (runtime-selectable; persisted per turret)
        // =================================================================

        public TargetingMode CurrentTargetingMode => _targetingMode;

        /// <summary>
        /// Runtime setter used by the turret info UI and by save/restore. Combat turrets
        /// always keep a real priority, so None is ignored. Clears the current target so
        /// the new priority is applied on the next combat tick.
        /// </summary>
        public void SetTargetingMode(TargetingMode mode)
        {
            if (mode == TargetingMode.None) return;
            if (_targetingMode == mode) return;

            _targetingMode = mode;
            _currentTarget = null;
            _targetSearchTimer = 0f;
        }

        private bool IsTargetStillValid(EnemyController target)
        {
            if (target == null || target.IsDead || !target.gameObject.activeInHierarchy) return false;
            if (target.IsStealth && !target.IsRevealed && !Data.CanTargetStealth) return false;

            Vector3 origin = _cachedTransform != null ? _cachedTransform.position : transform.position;
            Vector3 targetPos = target.transform.position;
            float dx = targetPos.x - origin.x;
            float dz = targetPos.z - origin.z;
            return (dx * dx) + (dz * dz) <= _cachedRangeSqr;
        }

        private void ClearLaserState()
        {
            if (!_laserStateActive && _lastLaserTarget == null)
                return;

            if (_lastLaserTarget != null)
            {
                _lastLaserTarget.StopLaserPulseVFX();
                _lastLaserTarget = null;
            }

            ClearSecondaryLaserTargets();
            _beamRenderer?.Deactivate();
            _soundConfig?.StopLaserLoop();
            _muzzleVFX?.SetContinuous(false);
            _laserStackMultiplier = 1f;
            _pendingExecuteHpDamage = 0f;
            _laserStateActive = false;
            _laserDamageAccumulator = 0f;
            _multiLaserTargetRefreshAccumulator = 0f;
            _laserPublishTimer = 0f;
            _laserRefractionTimer = 0f;
            _laserRefractionDamageAccumulator = 0f;
            _cachedLaserModifierTargetRuntimeId = -1;
            _cachedLaserConditionalDamageMultiplier = 1f;
            ResetLaserCritWindow();
        }

        private Transform GetPrimaryFirePoint()
        {
            if (_cachedHasFirePoints && _cachedFirePoints.Length > 0 && _cachedFirePoints[0] != null)
                return _cachedFirePoints[0];
            return transform;
        }

        private void ClearSecondaryLaserTargets()
        {
            for (int i = 1; i < _multiLaserTargetCount; i++)
            {
                var secondary = _multiLaserTargets[i];
                if (secondary != null && secondary != _lastLaserTarget)
                    secondary.StopLaserPulseVFX();
                _multiLaserTargets[i] = null;
            }

            _multiLaserTargetCount = 0;
        }

        // =================================================================
        // ATTACK Ã¢ÂÂ Projectile-based turrets (Basic, Frost, Inferno, Lightning)
        // =================================================================

        protected virtual void Attack(EnemyController target)
        {
            if (target == null || target.IsDead) return;

            StatusEffectType status = StatusEffectType.None;
            float statusValue = 0f;
            float statusDuration = 0f;

            // Identity scaling: status effects grow with turret level so upgrading a
            // Frost/Inferno turret improves what makes it special, not just raw stats.
            float identityMult = 1f + Mathf.Min(
                Mathf.Max(0f, _identityScalingCap),
                Mathf.Max(0f, _identityScalingPerLevel) * Mathf.Max(0, Level - 1));

            switch (Data.Type)
            {
                case TurretType.Basic:
                    // Basic's signature status: temporary armor reduction on the target,
                    // consumed by EnemyController.ApplyMitigation. Tier2 (Lv 25) adds
                    // ArmorBreakPercentBonus on top, converging from either Path A or B.
                    status = StatusEffectType.ArmorBreak;
                    statusValue = (Data.ArmorBreakPercent
                        + (_cachedTier2Evolution != null ? _cachedTier2Evolution.ArmorBreakPercentBonus : 0f))
                        * identityMult;
                    statusDuration = Data.ArmorBreakDuration * identityMult;
                    break;

                case TurretType.Toxin:
                    // Identity/VFX marker only — mirrors Laser's HPPercentReduce fix
                    // from Phase 0. The actual damage is a direct TakePureDamage call
                    // below (after finalDamage is computed), not carried through this
                    // status's value, since it must bypass mitigation entirely.
                    status = StatusEffectType.Poison;
                    statusValue = 1f;
                    statusDuration = 1f * identityMult;
                    break;

                case TurretType.Railgun:
                    status = StatusEffectType.Expose;
                    statusValue = (Data.ExposePercent
                        + (_cachedTier2Evolution != null ? _cachedTier2Evolution.ExposePercentBonus : 0f))
                        * identityMult;
                    statusDuration = Data.ExposeDuration * identityMult;
                    break;

                case TurretType.Void:
                    // Void's signature status: corrodes the target's effective Armor
                    // (consumed in EnemyController.ApplyMitigation), stacking with the
                    // player's own ArmorPierce stat rather than duplicating it.
                    status = StatusEffectType.Weaken;
                    statusValue = (Data.WeakenPercent
                        + (_cachedTier2Evolution != null ? _cachedTier2Evolution.WeakenPercentBonus : 0f))
                        * identityMult;
                    statusDuration = Data.WeakenDuration * identityMult;
                    break;

                case TurretType.Frost:
                    status = StatusEffectType.Slow;
                    statusValue = (Data.SlowPercent
                        + (_cachedTier2Evolution != null ? _cachedTier2Evolution.SlowPercentBonus : 0f))
                        * identityMult;
                    statusDuration = Data.SlowDuration * identityMult;
                    if (_statModifiers != null)
                    {
                        statusValue *= _statModifiers.GetSlowStrengthMultiplier();
                        statusDuration *= _statModifiers.GetSlowDurationMultiplier();
                    }
                    break;

                case TurretType.Inferno:
                    status = StatusEffectType.Burn;
                    statusValue = (Data.BurnDPS
                        + (_cachedTier2Evolution != null ? _cachedTier2Evolution.BurnDPSBonus : 0f))
                        * identityMult;
                    statusDuration = Data.BurnDuration;
                    if (_statModifiers != null)
                    {
                        statusValue *= _statModifiers.GetBurnDamageMultiplier();
                        statusDuration *= _statModifiers.GetBurnDurationMultiplier();
                    }
                    break;

                case TurretType.Lightning:
                    // Chain damage is now secondary-only. The primary target is damaged
                    // by the projectile/direct hit below, preventing the old double-hit bug.
                    Profiler.BeginSample("Turret.Attack.LightningChain");
                    ApplyChainDamage(target);
                    Profiler.EndSample();
                    break;
            }

            _shotCounter++;
            float finalDamage = CalculateHitDamage(Damage, target);
            bool isCriticalHit = _lastDamageRollWasCritical;

            // Late-game scaling for evolved non-lightning turrets: bonus damage equal
            // to a fraction of the target's CURRENT HP per hit — the same scaling class
            // that makes the Lightning chain evolution viable at wave 100+. Counts
            // toward percent-HP damage tracking/unlocks.
            float hitCurrentHPPercent = (_cachedEvolution != null ? _cachedEvolution.HitCurrentHPPercent : 0f)
                + (_cachedTier2Evolution != null ? _cachedTier2Evolution.HitCurrentHPPercent : 0f);
            if (hitCurrentHPPercent > 0f && !target.IsDead)
            {
                float pctDamage = target.CurrentHealth * hitCurrentHPPercent;
                finalDamage += pctDamage;
                EventBus.Publish(new PercentHPDamageEvent { DamageAmount = pctDamage });
            }

            // Toxin's signature identity: bonus PURE damage per hit that bypasses
            // Armor/affinity entirely (unlike the HitCurrentHPPercent bonus above,
            // which is added to finalDamage and still goes through mitigation).
            if (Data.Type == TurretType.Toxin && !target.IsDead)
            {
                float toxinPercent = Data.ToxinPurePercent
                    + (_cachedTier2Evolution != null ? _cachedTier2Evolution.ToxinPurePercentBonus : 0f);
                if (toxinPercent > 0f)
                {
                    float pureDamage = target.CurrentHealth * toxinPercent * identityMult;
                    if (pureDamage > 0f)
                    {
                        target.TakePureDamage(pureDamage, playHitVFX: false, showDamageNumber: false,
                            sourceTurretType: (int)Data.Type, sourceTurretId: InstanceId);
                        EventBus.Publish(new PercentHPDamageEvent { DamageAmount = pureDamage });
                    }
                }
            }

            // Burn rework: flat data BurnDPS becomes irrelevant once hits reach
            // thousands+, so burn now scales from the applying hit. statusValue is the
            // DPS of ONE burn stack (_burnHitPercent of the hit spread over the
            // duration); the flat DPS (with its bonuses, already in statusValue)
            // remains as an early-game floor. Burns stack per hit (capped in
            // EnemyModifierStack), so attack speed and burn investment scale total
            // burn output. finalDamage includes crit, so crits apply stronger burns.
            if (status == StatusEffectType.Burn && statusDuration > 0.01f)
            {
                float burnBonus = _statModifiers != null ? _statModifiers.GetBurnDamageMultiplier() : 1f;
                // Catalytic Burn cards add to the burn-from-hit fraction.
                float hitFraction = _burnHitPercent
                    + (_statModifiers != null ? Mathf.Max(0f, _statModifiers.GetBurnFromHitBonus()) : 0f);
                float hitScaledDps = (finalDamage * hitFraction / statusDuration) * burnBonus;
                if (hitScaledDps > statusValue)
                    statusValue = hitScaledDps;
            }

            // Spawn visual projectile or use direct-hit fallback.
            // Projectile telemetry is aggregated once per rendered frame instead of being
            // dispatched through EventBus for every turret attack.
            Profiler.BeginSample("Turret.Attack.SpawnProjectile");
            int projectileEvents = 0;

            bool fireExtraProjectile = ShouldFireExtraProjectile();
            if (_projectileManager != null)
            {
                projectileEvents += SpawnProjectileFromConfig(target, finalDamage, status, statusValue, statusDuration, isCriticalHit);

                if (fireExtraProjectile)
                    projectileEvents += SpawnProjectileFromConfig(target, finalDamage, status, statusValue, statusDuration, isCriticalHit);
            }
            else
            {
                // Fallback: direct damage
                target.TakeDamage(finalDamage, status, statusValue, statusDuration, isCritical: isCriticalHit,
                    sourceTurretType: (int)Data.Type, sourceTurretId: InstanceId);
                ApplyOnHitSpecials(target, finalDamage, status, statusValue, statusDuration);
                projectileEvents++;

                if (fireExtraProjectile)
                {
                    target.TakeDamage(finalDamage, status, statusValue, statusDuration, isCritical: isCriticalHit,
                    sourceTurretType: (int)Data.Type, sourceTurretId: InstanceId);
                    ApplyOnHitSpecials(target, finalDamage, status, statusValue, statusDuration);
                    projectileEvents++;
                }
            }

            if (projectileEvents > 0)
            {
                Profiler.BeginSample("Turret.Attack.ProjectileSpawnedEvent.Batch");
                // Challenge/analytics telemetry is flushed once in LateUpdate,
                // rather than dispatching EventBus work for every turret attack.
                ProjectileSpawnEventBatcher.Report(projectileEvents);
                Profiler.EndSample();
            }
            Profiler.EndSample();

            // Evolution-specific effects
            if (_hasPostAttackEvolutionEffects)
            {
                Profiler.BeginSample("Turret.Attack.HandleEvolvedAttack");
                HandleEvolvedAttack(target);
                Profiler.EndSample();
            }
        }

        private ILaserHitTelemetrySink GetLaserTelemetrySink()
        {
            if (_cachedLaserTelemetrySink != null)
                return _cachedLaserTelemetrySink;

            ServiceLocator.TryGet(out _cachedLaserTelemetrySink);
            return _cachedLaserTelemetrySink;
        }

        // =================================================================
        // ATTACK Ã¢ÂÂ Laser (continuous beam)
        // =================================================================

        private void AttackLaser(EnemyController target, float dt)
        {
            bool profileOuterTotal = _profileOuterLaserTotal;
            if (profileOuterTotal)
                Profiler.BeginSample("Turret.AttackLaser.Total");

            bool targetChanged = _lastLaserTarget != target;
            Transform beamOrigin = null;

            if (targetChanged)
            {
                Profiler.BeginSample("Turret.AttackLaser.TargetChangedVisuals");
                beamOrigin = GetPrimaryFirePoint();

                if (_lastLaserTarget != null)
                    _lastLaserTarget.StopLaserPulseVFX();

                ClearSecondaryLaserTargets();
                _laserStackMultiplier = 1f;
                _pendingExecuteHpDamage = 0f;
                _lastLaserTarget = target;
                _laserDamageAccumulator = 0f;
                _multiLaserTargetRefreshAccumulator = 0f;
                _laserRefractionTimer = 0f;
                _laserRefractionDamageAccumulator = 0f;
                _cachedLaserModifierTargetRuntimeId = -1;
                _cachedLaserConditionalDamageMultiplier = 1f;
                ResetLaserCritWindow();

                target.StartLaserPulseVFX();
                _beamRenderer?.Activate(beamOrigin, target);
                Profiler.EndSample();
            }

            if (!_laserStateActive)
            {
                Profiler.BeginSample("Turret.AttackLaser.StateStart");
                _laserStateActive = true;
                _soundConfig?.StartLaserLoop();
                _muzzleVFX?.SetContinuous(true);
                Profiler.EndSample();
            }

            Profiler.BeginSample("Turret.AttackLaser.AccumulateAndGate");
            _laserDamageAccumulator += Mathf.Max(0f, dt);
            float tickInterval = _cachedLaserDamageTickInterval;
            if (_laserDamageAccumulator < tickInterval)
            {
                Profiler.EndSample();
                if (profileOuterTotal)
                    Profiler.EndSample();
                return;
            }

            float damageDt = Mathf.Min(_laserDamageAccumulator, _cachedMaxLaserDamageStep);
            _laserDamageAccumulator = 0f;
            Profiler.EndSample();

            Profiler.BeginSample("Turret.AttackLaser.CritAndConditionalMultiplier");
            float critMultiplier = GetCurrentLaserCritMultiplier(damageDt);
            float conditionalMultiplier = GetCachedLaserConditionalDamageMultiplier(target);
            float dps = Damage * conditionalMultiplier * _laserStackMultiplier * critMultiplier;
            float damageThisTick = dps * damageDt;
            Profiler.EndSample();

            Profiler.BeginSample("Turret.AttackLaser.PrimaryTakeDamage");
            target.TakeDamage(damageThisTick,
                playHitVFX: _flashLaserHitVFX,
                isCritical: _laserCritActive,
                damageKind: DamageNumberKind.Laser,
                showDamageNumber: _showPrimaryLaserDamageNumbers,
                sourceTurretType: (int)Data.Type,
                sourceTurretId: InstanceId);
            Profiler.EndSample();

            // Laser's signature status marker: HPPercentReduce previously existed only
            // in the enum/VFX map with no ApplyStatus call site, so a base (non-Stacker)
            // laser never registered as its own turret-type family for challenge/
            // achievement tracking. Applied on every tick (short refreshing duration,
            // mirrors Frost/Inferno applying alongside their per-hit damage) so any
            // laser build counts, not just the evolved missing-HP execute.
            if (!target.IsDead)
                target.ApplyStatus(StatusEffectType.HPPercentReduce, _laserStackMultiplier - 1f, 0.5f,
                    (int)Data.Type, InstanceId);

            AccumulateLaserRefraction(target, damageThisTick, damageDt);

            Profiler.BeginSample("Turret.AttackLaser.Telemetry.01.AccumulateAndGate");
            _laserPublishTimer += damageDt;
            bool publishLaserTelemetry = _laserPublishTimer >= _cachedLaserChallengeTelemetryInterval;
            float publishedDt = publishLaserTelemetry ? _laserPublishTimer : 0f;
            if (publishLaserTelemetry)
            {
                _laserPublishTimer = 0f;

                // Execute damage accumulates per frame; report it toward percent-HP
                // tracking on the same batched cadence as the laser challenge telemetry.
                if (_pendingExecuteHpDamage > 0f)
                {
                    EventBus.Publish(new PercentHPDamageEvent { DamageAmount = _pendingExecuteHpDamage });
                    _pendingExecuteHpDamage = 0f;
                }
            }
            Profiler.EndSample();

            if (publishLaserTelemetry)
            {
                Profiler.BeginSample("Turret.AttackLaser.Telemetry.02.ResolveSink");
                ILaserHitTelemetrySink laserSink = GetLaserTelemetrySink();
                Profiler.EndSample();

                if (laserSink != null)
                {
                    // No wrapper here: this allows ChallengeTracker.LaserBatch.*
                    // markers to appear directly in the Profiler.
                    laserSink.OnLaserHitBatch(publishedDt, target.InstanceId);
                }
                else
                {
                    Profiler.BeginSample("Turret.AttackLaser.Telemetry.03.EventBusFallback");
                    EventBus.Publish(new LaserHitEvent
                    {
                        TargetEnemyId = target.InstanceId,
                        DeltaTime = publishedDt
                    });
                    Profiler.EndSample();
                }
            }

            if (!_cachedIsStackingLaser && _innateLaserRampPerSecond > 0f)
            {
                // Non-lightning viability: ALL lasers now ramp on a sustained target
                // (the Path-B evolution keeps its much stronger dedicated ramp below).
                // COMPOUNDING growth: the ramp accelerates instead of crawling
                // linearly, so sustained focus fire visibly snowballs. Resets on
                // target change, so it rewards tanks/bosses without buffing swarm clear.
                _laserStackMultiplier = Mathf.Min(
                    _laserStackMultiplier * (1f + _innateLaserRampPerSecond * damageDt),
                    1f + _innateLaserRampCap);
                _beamRenderer?.SetStackMultiplier(_laserStackMultiplier);
            }

            if (_cachedIsStackingLaser && Data.PathB != null)
            {
                Profiler.BeginSample("Turret.AttackLaser.Stacking");
                float stackingInterval = Mathf.Max(0.001f, Data.PathB.StackingInterval);
                // Compounding like the innate ramp, at the evolution's stronger rate.
                float rampRate = Data.PathB.StackingDPSPercent / stackingInterval;
                float stackingCap = 1f + Data.PathB.StackingCap;
                _laserStackMultiplier = Mathf.Min(
                    _laserStackMultiplier * (1f + rampRate * damageDt),
                    stackingCap);

                _beamRenderer?.SetStackMultiplier(_laserStackMultiplier);

                // Missing-HP execute: the payoff for reaching and holding full stacks.
                // Pure damage (no armor), no per-frame damage number; the amount is
                // batched into a PercentHPDamageEvent on the laser telemetry cadence.
                if (_laserExecuteMissingHPPercent > 0f &&
                    _laserStackMultiplier >= stackingCap * 0.999f && !target.IsDead)
                {
                    float missingHp = target.MaxHealth - target.CurrentHealth;
                    if (missingHp > 0f)
                    {
                        float executeDamage = missingHp * _laserExecuteMissingHPPercent * damageDt;
                        target.TakePureDamage(executeDamage, playHitVFX: false,
                            showDamageNumber: false,
                            sourceTurretType: (int)Data.Type, sourceTurretId: InstanceId);
                        _pendingExecuteHpDamage += executeDamage;
                    }
                }
                Profiler.EndSample();
            }

            if (_cachedIsMultiLaser && Data.PathA != null)
            {
                Profiler.BeginSample("Turret.AttackLaser.MultiLaser.01.Gate");
                _multiLaserTargetRefreshAccumulator += damageDt;
                bool refreshMultiTargets = _multiLaserTargetCount <= 1 ||
                    _multiLaserTargetRefreshAccumulator >= _cachedMultiLaserTargetRefreshInterval;
                Profiler.EndSample();

                if (refreshMultiTargets)
                {
                    _multiLaserTargetRefreshAccumulator = 0f;
                    if (beamOrigin == null)
                        beamOrigin = GetPrimaryFirePoint();
                    RefreshMultiLaserTargets(target, beamOrigin);
                }

                ApplyMultiLaserDamage(target, critMultiplier, damageDt);
            }

            if (profileOuterTotal)
                Profiler.EndSample();
        }

        private float GetCachedLaserConditionalDamageMultiplier(EnemyController target)
        {
            if (_statModifiers == null || target == null)
                return 1f;

            float now = Time.time;
            int runtimeId = target.RuntimeId;
            if (_cachedLaserModifierTargetRuntimeId == runtimeId && now < _nextLaserConditionalModifierRefreshTime)
                return _cachedLaserConditionalDamageMultiplier;

            Profiler.BeginSample("Turret.AttackLaser.ConditionalMultiplier.Refresh");
            _cachedLaserModifierTargetRuntimeId = runtimeId;
            _nextLaserConditionalModifierRefreshTime = now + Mathf.Max(0.02f, _laserConditionalModifierRefreshInterval);

            float multiplier = 1f;
            if (target.Tier == EnemyTier.Elite || target.Tier == EnemyTier.Boss)
                multiplier *= _statModifiers.GetDamageVsEliteMultiplier();

            int statusMask = target.StatusMask;
            if ((statusMask & EnemyModifierStack.StatusMaskBurn) != 0)
                multiplier *= _statModifiers.GetDamageVsBurningMultiplier();

            if ((statusMask & (EnemyModifierStack.StatusMaskSlow | EnemyModifierStack.StatusMaskFreeze)) != 0)
                multiplier *= _statModifiers.GetDamageVsSlowedFrozenMultiplier();

            if ((statusMask & EnemyModifierStack.StatusMaskFreeze) != 0)
                multiplier *= _statModifiers.GetFreezeAmplifierMultiplier();

            _cachedLaserConditionalDamageMultiplier = multiplier;
            Profiler.EndSample();
            return multiplier;
        }

        private void RefreshMultiLaserTargets(EnemyController primary, Transform beamOrigin)
        {
            if (_enemyManager == null || primary == null)
                return;

            int requiredTargets = Mathf.Max(1, _cachedRequiredMultiLaserTargets);

            Profiler.BeginSample("Turret.AttackLaser.MultiTargetQueryLimited");
            Vector3 origin = _cachedTransform != null ? _cachedTransform.position : transform.position;
            _enemyManager.GetEnemiesInRangeLimited(origin, Range, _enemiesInRange, requiredTargets - 1, primary);
            Profiler.EndSample();

            Profiler.BeginSample("Turret.AttackLaser.MultiTargetCacheBuild");
            if (_multiLaserTargets == null || _multiLaserTargets.Length < requiredTargets)
                _multiLaserTargets = new EnemyController[requiredTargets];
            if (_multiLaserScratch == null || _multiLaserScratch.Length < requiredTargets)
                _multiLaserScratch = new EnemyController[requiredTargets];
            if (_multiLaserDamageMultipliers == null || _multiLaserDamageMultipliers.Length < requiredTargets)
                _multiLaserDamageMultipliers = new float[requiredTargets];

            _multiLaserScratch[0] = primary;
            _multiLaserDamageMultipliers[0] = 1f;
            int added = 1;
            for (int i = 0; i < _enemiesInRange.Count && added < requiredTargets; i++)
            {
                var enemy = _enemiesInRange[i];
                if (enemy == null || enemy == primary || enemy.IsDead)
                    continue;

                _multiLaserScratch[added] = enemy;
                _multiLaserDamageMultipliers[added] = GetLaserConditionalDamageMultiplierUncached(enemy);
                added++;
            }
            Profiler.EndSample();

            Profiler.BeginSample("Turret.AttackLaser.MultiTargetVFXDiff");
            int previousCount = _multiLaserTargetCount;
            int compareCount = Mathf.Max(previousCount, added);
            bool targetsChanged = previousCount != added;
            for (int i = 1; i < compareCount; i++)
            {
                EnemyController previous = i < previousCount ? _multiLaserTargets[i] : null;
                EnemyController next = i < added ? _multiLaserScratch[i] : null;

                if (previous == next)
                    continue;

                targetsChanged = true;
                if (previous != null && previous != _lastLaserTarget)
                    previous.StopLaserPulseVFX();
                if (next != null && !next.IsDead)
                    next.StartLaserPulseVFX();

                _multiLaserTargets[i] = next;
            }

            _multiLaserTargets[0] = primary;
            _multiLaserTargetCount = added;
            Profiler.EndSample();

            if (targetsChanged || (_beamRenderer != null && !_beamRenderer.IsActive))
            {
                Profiler.BeginSample("Turret.AttackLaser.BeamActivateMulti");
                _beamRenderer?.ActivateMulti(beamOrigin, _multiLaserTargets, added);
                Profiler.EndSample();
            }
        }

        private void ApplyMultiLaserDamage(EnemyController primary, float critMultiplier, float damageDt)
        {
            if (_multiLaserTargetCount <= 1 || _cachedMultiLaserDamagePercent <= 0f)
                return;

            Profiler.BeginSample("Turret.AttackLaser.MultiLaser.ApplyDamage");
            float baseSecondaryDamage = Damage * _cachedMultiLaserDamagePercent * critMultiplier * damageDt;
            for (int i = 1; i < _multiLaserTargetCount; i++)
            {
                var enemy = _multiLaserTargets[i];
                if (enemy == null || enemy.IsDead)
                    continue;

                float multiplier = (i < _multiLaserDamageMultipliers.Length) ? _multiLaserDamageMultipliers[i] : 1f;
                enemy.TakeDamage(baseSecondaryDamage * multiplier,
                    playHitVFX: _flashLaserHitVFX,
                    isCritical: _laserCritActive,
                    damageKind: DamageNumberKind.Laser,
                    showDamageNumber: _showSecondaryLaserDamageNumbers,
                    sourceTurretType: (int)Data.Type,
                    sourceTurretId: InstanceId);
            }
            Profiler.EndSample();
        }

        private float GetLaserConditionalDamageMultiplierUncached(EnemyController target)
        {
            if (_statModifiers == null || target == null)
                return 1f;

            float multiplier = 1f;
            if (target.Tier == EnemyTier.Elite || target.Tier == EnemyTier.Boss)
                multiplier *= _statModifiers.GetDamageVsEliteMultiplier();

            int statusMask = target.StatusMask;
            if ((statusMask & EnemyModifierStack.StatusMaskBurn) != 0)
                multiplier *= _statModifiers.GetDamageVsBurningMultiplier();
            if ((statusMask & (EnemyModifierStack.StatusMaskSlow | EnemyModifierStack.StatusMaskFreeze)) != 0)
                multiplier *= _statModifiers.GetDamageVsSlowedFrozenMultiplier();
            if ((statusMask & EnemyModifierStack.StatusMaskFreeze) != 0)
                multiplier *= _statModifiers.GetFreezeAmplifierMultiplier();

            return multiplier;
        }



        private float GetEffectiveAttackSpeed()
        {
            float speed = AttackSpeed;
            float perEnemy = _statModifiers?.GetAttackSpeedPerNearbyEnemyBonus() ?? 0f;
            if (perEnemy <= 0f || _enemyManager == null)
                return Mathf.Max(0.01f, speed);

            float now = Time.time;
            if (!_effectiveAttackSpeedCached || now >= _nextEffectiveAttackSpeedRefreshTime)
            {
                Profiler.BeginSample("Turret.GetEffectiveAttackSpeed.NearbyEnemyQuery");
                _enemyManager.GetEnemiesInRange(_cachedTransform != null ? _cachedTransform.position : transform.position,
                    Range, _specialTargets);
                Profiler.EndSample();

                _cachedEffectiveAttackSpeed = AttackSpeed * (1f + perEnemy * _specialTargets.Count);
                _cachedEffectiveAttackSpeed = Mathf.Min(_cachedEffectiveAttackSpeed, _maxProjectileAttackSpeed);
                _nextEffectiveAttackSpeedRefreshTime = now + Mathf.Max(0.03f, _nearbyEnemyAttackSpeedRefreshInterval);
                _effectiveAttackSpeedCached = true;
            }

            return Mathf.Max(0.01f, _cachedEffectiveAttackSpeed);
        }

        private float CalculateHitDamage(float baseDamage, EnemyController target)
        {
            float damage = ApplyConditionalDamageModifiers(baseDamage, target);

            float distanceBonus = _statModifiers?.GetDamagePerDistanceBonus() ?? 0f;
            if (distanceBonus > 0f && target != null)
            {
                float distance = (target.transform.position - transform.position).magnitude;
                damage *= 1f + distanceBonus * distance;
                EventBus.Publish(new SniperDamageEvent { Damage = damage - baseDamage });
            }

            return ApplyCriticalDamage(damage);
        }

        private float ApplyConditionalDamageModifiers(float damage, EnemyController target)
        {
            if (_statModifiers == null || target == null) return damage;

            float finalDamage = damage;

            if (target.Tier == EnemyTier.Elite || target.Tier == EnemyTier.Boss)
                finalDamage *= _statModifiers.GetDamageVsEliteMultiplier();

            int statusMask = target.StatusMask;
            if ((statusMask & EnemyModifierStack.StatusMaskBurn) != 0)
                finalDamage *= _statModifiers.GetDamageVsBurningMultiplier();

            if ((statusMask & (EnemyModifierStack.StatusMaskSlow | EnemyModifierStack.StatusMaskFreeze)) != 0)
                finalDamage *= _statModifiers.GetDamageVsSlowedFrozenMultiplier();

            if ((statusMask & EnemyModifierStack.StatusMaskFreeze) != 0)
                finalDamage *= _statModifiers.GetFreezeAmplifierMultiplier();

            return finalDamage;
        }
     
        private bool ShouldFireExtraProjectile()
        {
            float enabled = _statModifiers?.GetExtraProjectileEveryNthShot() ?? 0f;
            if (enabled <= 0f) return false;

            const int interval = 4; // Double Barrel asset uses ConditionInterval 4.
            return _shotCounter > 0 && _shotCounter % interval == 0;
        }

        private void ApplyOnHitSpecials(EnemyController target, float hitDamage,
     StatusEffectType status, float statusValue, float statusDuration)
        {
            if (target == null || _statModifiers == null)
                return;

            float shockChance = _statModifiers.GetShockChance();
            if (shockChance > 0f && !target.IsDead && Random.value < shockChance)
                target.ApplyStatus(StatusEffectType.Shock, 1f, 0.25f, (int)Data.Type, InstanceId);

            // Ember Stacker (Inferno Path B): every hit applies one EXTRA burn stack
            // and may push the enemy past the default burn-stack cap up to the
            // evolution's MaxBurnStacks. statusValue is already the hit-scaled DPS
            // of one stack, so an Ember Stacker hit burns twice as hard and deep
            // focus fire can sustain a full high-cap stack set on a tank.
            if (status == StatusEffectType.Burn && statusDuration > 0f && !target.IsDead &&
                _cachedEvolution != null && _cachedEvolution.StackingBurn)
            {
                target.ApplyStatus(StatusEffectType.Burn,
                    Mathf.Max(statusValue, _cachedEvolution.BurnStackDPS), statusDuration,
                    (int)Data.Type, InstanceId,
                    maxBurnStacks: _cachedEvolution.MaxBurnStacks);
            }

            // Thermal Cascade: burn spreads while attacking.
            if (status == StatusEffectType.Burn && statusDuration > 0f)
            {
                float chance = _statModifiers.GetBurnSpreadChance();
                if (chance > 0f && Random.value < chance)
                    SpreadBurn(target, statusValue, statusDuration);
            }

            // Death Explosion now procs centrally from EnemyController.Die() so it can
            // trigger off ANY kill type (laser ticks, chain hits, burn/DoT, evolved
            // area splash), not just this primary-hit path. See EnemyController's
            // LastHit* tracking + TryTriggerDeathExplosion.

            // Thermal Bloom: burn spreads only when a burning enemy dies.
            if (target.IsDead && target.HasStatus(StatusEffectType.Burn))
            {
                float chance = _statModifiers.GetBurnSpreadOnDeathChance();
                if (chance > 0f && Random.value < chance)
                    SpreadBurn(target, Data.BurnDPS, Data.BurnDuration);
            }
        }

        public void OnProjectileHitAfterDamage(EnemyController target, float damage,
            StatusEffectType statusType, float statusValue, float statusDuration)
        {
            ApplyOnHitSpecials(target, damage, statusType, statusValue, statusDuration);
        }

        private void SpreadBurn(EnemyController source, float burnDps, float duration)
        {
            if (source == null || _enemyManager == null)
                return;

            const float radius = 2.25f;
            const float spreadPower = 0.6f;

            _enemyManager.GetEnemiesInRange(source.transform.position, radius, _specialTargets);

            for (int i = 0; i < _specialTargets.Count; i++)
            {
                EnemyController enemy = _specialTargets[i];

                if (enemy == null || enemy == source || enemy.IsDead)
                    continue;

                enemy.ApplyStatus(
                    StatusEffectType.Burn,
                    burnDps * spreadPower,
                    duration * spreadPower,
                    (int)Data.Type,
                    InstanceId
                );
            }
        }

        private void ApplyChainBounceBack(EnemyController primary, float chainDamage)
        {
            float chance = _statModifiers?.GetChainBounceBackChance() ?? 0f;
            if (chance <= 0f || primary == null || primary.IsDead || Random.value > chance) return;

            primary.TakeDamage(chainDamage * 0.5f, sourceTurretType: (int)Data.Type, sourceTurretId: InstanceId);
        }

        private void AccumulateLaserRefraction(EnemyController primary, float frameDamage, float damageDt)
        {
            if (!_hasLaserRefraction || _enemyManager == null || primary == null || frameDamage <= 0f)
                return;

            Profiler.BeginSample("Turret.AttackLaser.Refraction.Accumulate");
            _laserRefractionDamageAccumulator += frameDamage;
            _laserRefractionTimer += damageDt;
            if (_laserRefractionTimer < _cachedLaserRefractionTickInterval)
            {
                Profiler.EndSample();
                return;
            }

            float spillDamage = _laserRefractionDamageAccumulator * _cachedLaserRefractionPercent;
            _laserRefractionDamageAccumulator = 0f;
            _laserRefractionTimer = 0f;
            Profiler.EndSample();

            Profiler.BeginSample("Turret.AttackLaser.Refraction.QueryLimited");
            _enemyManager.GetEnemiesInRangeLimited(primary.transform.position, 2.5f, _specialTargets, 2, primary);
            Profiler.EndSample();

            Profiler.BeginSample("Turret.AttackLaser.Refraction.ApplyDamage");
            for (int i = 0; i < _specialTargets.Count; i++)
            {
                var e = _specialTargets[i];
                if (e == null || e.IsDead) continue;
                e.TakeDamage(spillDamage,
                    playHitVFX: _flashLaserHitVFX,
                    damageKind: DamageNumberKind.Laser,
                    showDamageNumber: _showSecondaryLaserDamageNumbers,
                    sourceTurretType: (int)Data.Type,
                    sourceTurretId: InstanceId);
            }
            Profiler.EndSample();
        }

        // =================================================================
        // ATTACK Ã¢ÂÂ Lightning (chain damage)
        // =================================================================

        private void ApplyChainDamage(EnemyController primary)
        {
            if (primary == null || primary.IsDead || _enemyManager == null)
                return;

            int chains = Data.ChainCount;
            if (IsEvolved && EvolutionPath == 0 && Data.PathA != null)
                chains = Data.PathA.ExtendedChainCount;
            if (_statModifiers != null)
                chains += _statModifiers.GetBonusChainTargets();

            chains = Mathf.Max(0, chains);
            Vector3 primaryPosition = primary.transform.position;

            if (chains > 0)
            {
                float chainRange = Data.ChainRange;
                float chainDamageMultiplier = 1f;
                float bounceBackChance = 0f;

                if (_statModifiers != null)
                {
                    chainRange *= _statModifiers.GetChainRangeMultiplier();
                    chainDamageMultiplier = _statModifiers.GetChainDamageMultiplier();
                    bounceBackChance = _statModifiers.GetChainBounceBackChance();
                }

                // The old code collected every candidate and then used only the first
                // <chains> valid entries. This limited query preserves that bucket order
                // while stopping as soon as enough secondary targets are found.
                Profiler.BeginSample("Turret.LightningChain.QueryLimited");
                _enemyManager.GetEnemiesInRangeLimited(
                    primaryPosition,
                    chainRange,
                    _enemiesInRange,
                    chains,
                    primary);
                Profiler.EndSample();

                float chainDamage = Damage;
                float falloff = Data.ChainDamageFalloff;
                bool usePercentHpDamage = IsEvolved && EvolutionPath == 1 && Data.PathB != null
                    && Data.PathB.ChainHPPercentDamage > 0f;
                float percentHpDamage = usePercentHpDamage ? Data.PathB.ChainHPPercentDamage : 0f;

                int applied = 0;
                int visualBudget = _chainRenderer != null ? Mathf.Max(0, _maxChainVisualsPerAttack) : 0;
                bool collectChainEnemyIds = ChainLightningHitBatcher.WantsEnemyIds;
                if (collectChainEnemyIds)
                    _chainHitEnemyIds.Clear();

                Profiler.BeginSample("Turret.LightningChain.ApplyDamage");
                for (int i = 0; i < _enemiesInRange.Count; i++)
                {
                    EnemyController enemy = _enemiesInRange[i];
                    if (enemy == null || enemy.IsDead)
                        continue;

                    chainDamage *= falloff;
                    float finalChainDamage = CalculateHitDamage(chainDamage * chainDamageMultiplier, enemy);
                    bool isChainCritical = _lastDamageRollWasCritical;

                    // Secondary chain hits are extremely numerous. Damage still applies
                    // fully, but damage-number publishing is optional to protect the HUD
                    // event path on low-end laptops.
                    enemy.TakeChainDamage(finalChainDamage, percentHpDamage, playHitVFX: false,
                        isCritical: isChainCritical, showDamageNumber: _showSecondaryChainDamageNumbers,
                        sourceTurretType: (int)Data.Type, sourceTurretId: InstanceId);

                    if (bounceBackChance > 0f && !primary.IsDead && Random.value <= bounceBackChance)
                        primary.TakeChainDamage(finalChainDamage * 0.5f, 0f, playHitVFX: false,
                            isCritical: isChainCritical, showDamageNumber: _showSecondaryChainDamageNumbers,
                            sourceTurretType: (int)Data.Type, sourceTurretId: InstanceId);

                    if (collectChainEnemyIds)
                        _chainHitEnemyIds.Add(enemy.RuntimeId);

                    if (visualBudget > 0)
                    {
                        _chainRenderer.ShowChain(primaryPosition, enemy.transform.position);
                        visualBudget--;
                    }

                    applied++;
                }

                // One batcher lookup rather than one static report per bounce. Once
                // combo tracking is complete, send count-only telemetry and avoid all
                // unique-id work.
                if (collectChainEnemyIds)
                    ChainLightningHitBatcher.Report(_chainHitEnemyIds);
                else
                    ChainLightningHitBatcher.ReportCount(applied);
                Profiler.EndSample();

                // AudioManager already limits simultaneous chain sounds. One request per
                // chain attack avoids invoking its cooldown/category machinery per bounce.
                if (applied > 0)
                    _soundConfig?.PlayChainLightning(primaryPosition);

                EventBus.Publish(new ChainLightningFiredEvent
                {
                    HitCount = applied + 1, // +1 for the primary projectile hit
                    Origin = primaryPosition
                });
                return;
            }

            EventBus.Publish(new ChainLightningFiredEvent
            {
                HitCount = 1,
                Origin = primaryPosition
            });
        }

        // =================================================================
        // EVOLUTION EFFECTS
        // =================================================================

        private void RefreshEvolvedAreaTargets(
            EnemyController anchor,
            float radius,
            List<EnemyController> results,
            ref EnemyController cachedAnchor,
            ref Vector3 cachedAnchorPosition,
            ref float nextRefreshTime,
            bool prioritizeClosestToExit = false)
        {
            if (_enemyManager == null || anchor == null || anchor.IsDead)
            {
                results.Clear();
                cachedAnchor = null;
                return;
            }

            Vector3 position = anchor.transform.position;
            bool anchorChanged = cachedAnchor != anchor;
            bool moved = (position - cachedAnchorPosition).sqrMagnitude > EvolvedAreaAnchorRequeryDistanceSqr;
            if (!anchorChanged && !moved && Time.time < nextRefreshTime)
                return;

            _enemyManager.GetEnemiesInRangeLimited(
                position,
                Mathf.Max(0.1f, radius),
                results,
                Mathf.Max(1, _maxEvolvedAreaTargets),
                prioritizeClosestToExit: prioritizeClosestToExit);

            cachedAnchor = anchor;
            cachedAnchorPosition = position;
            nextRefreshTime = Time.time + Mathf.Max(0.03f, _evolvedAreaTargetRefreshInterval);
        }

        private void HandleEvolvedAttack(EnemyController target)
        {
            TurretEvolutionData evo = _cachedEvolution;
            if (evo == null || target == null || target.IsDead)
                return;

            float now = Time.time;

            // Freeze is already naturally de-duplicated by the enemy modifier stack.
            // Pass the evolution's per-enemy immunity so fast attack speed can't
            // permanently freeze-lock (the enemy downgrades re-freezes to a slow).
            if (evo.FreezeOnHit && !target.HasStatus(StatusEffectType.Freeze))
                target.ApplyStatus(StatusEffectType.Freeze, 1f, evo.FreezeDuration, (int)Data.Type, InstanceId,
                    evo.FreezeImmunityTime);

            // Frost Path B: status renewal is independent from shot rate. Re-query
            // candidates only when the source target moves, changes or cache expires.
            if (evo.AreaSlow && now >= _nextAreaSlowApplyTime)
            {
                RefreshEvolvedAreaTargets(target, evo.AreaSlowRadius, _evolvedAreaSlowTargets,
                    ref _areaSlowAnchorTarget, ref _areaSlowAnchorPosition, ref _nextAreaSlowTargetRefreshTime,
                    prioritizeClosestToExit: true);

                for (int i = 0; i < _evolvedAreaSlowTargets.Count; i++)
                {
                    EnemyController enemy = _evolvedAreaSlowTargets[i];
                    if (enemy != null && !enemy.IsDead)
                        enemy.ApplyStatus(StatusEffectType.Slow, evo.AreaSlowPercent, evo.AreaSlowDuration, (int)Data.Type, InstanceId);
                }

                float renewal = Mathf.Min(
                    Mathf.Max(0.03f, evo.AreaSlowDuration * 0.5f),
                    Mathf.Max(0.03f, _evolvedStatusReapplyInterval));
                _nextAreaSlowApplyTime = now + renewal;
            }

            // Inferno Path A: query a bounded cached set, then resolve damage against
            // that set. One crit roll per flame pulse replaces one roll per enemy.
            if (evo.AreaDamage)
            {
                RefreshEvolvedAreaTargets(target, evo.ConeRange, _evolvedAreaDamageTargets,
                    ref _areaDamageAnchorTarget, ref _areaDamageAnchorPosition, ref _nextAreaDamageTargetRefreshTime);

                bool applyBurn = now >= _nextAreaBurnApplyTime;

                // Blastfire detonation. Each shot splashes a fraction of this turret's
                // per-hit damage to nearby enemies, so the explosion scales with upgrades
                // and enemy HP instead of the old flat ConeDPS. ConeDPS remains an
                // early-game flat floor when authored. (The previous code multiplied
                // ConeDPS by Time.deltaTime even though this runs once per shot, which,
                // combined with ConeDPS being 0 in data, meant the blast dealt no damage.)
                float splashDamage = Mathf.Max(evo.ConeDPS, Damage * Mathf.Max(0f, _blastfireAreaDamagePercent));
                float areaDamage = ApplyCriticalDamage(splashDamage);
                bool isAreaCritical = _lastDamageRollWasCritical;

                // Spread burn scales off the splash hit (mirrors the primary Inferno burn),
                // with the flat ConeBurnDPS as an early-game floor so it stays relevant late.
                float spreadBurnDps = evo.ConeBurnDPS;
                if (Data.BurnDuration > 0.01f)
                {
                    float burnBonus = _statModifiers != null ? _statModifiers.GetBurnDamageMultiplier() : 1f;
                    float hitScaledDps = (areaDamage * _burnHitPercent / Data.BurnDuration) * burnBonus;
                    if (hitScaledDps > spreadBurnDps)
                        spreadBurnDps = hitScaledDps;
                }

                int hitVfxBudget = 2;

                for (int i = 0; i < _evolvedAreaDamageTargets.Count; i++)
                {
                    EnemyController enemy = _evolvedAreaDamageTargets[i];
                    if (enemy == null || enemy == target || enemy.IsDead)
                        continue;

                    // Dense cone damage does not need a full hit-flash object on every
                    // enemy every shot. Keep the first two for readable feedback.
                    bool playHitVfx = hitVfxBudget-- > 0;
                    enemy.TakeDamage(areaDamage, StatusEffectType.None, 0f, 0f, playHitVfx,
                        isCritical: isAreaCritical, damageKind: DamageNumberKind.Area,
                        sourceTurretType: (int)Data.Type, sourceTurretId: InstanceId);

                    if (applyBurn && !enemy.IsDead)
                        enemy.ApplyStatus(StatusEffectType.Burn, spreadBurnDps, Data.BurnDuration, (int)Data.Type, InstanceId);
                }

                if (applyBurn)
                {
                    float renewal = Mathf.Min(
                        Mathf.Max(0.03f, Data.BurnDuration * 0.5f),
                        Mathf.Max(0.03f, _evolvedStatusReapplyInterval));
                    _nextAreaBurnApplyTime = now + renewal;
                }
            }

            // Inferno Path B (Ember Stacker) applies its extra burn stack per hit in
            // ApplyOnHitSpecials now that burns truly stack; no periodic renewal needed.
        }

        // =================================================================
        // SUPPORT TURRET
        // =================================================================

        //private void UpdateSupport()
        //{
        //    if (_visualConfig != null && _visualConfig.AuraVFXObject != null)
        //        _visualConfig.AuraVFXObject.SetActive(true);
        //    if (_turretManager == null) return;

        //    _turretManager.GetTurretsInRange(transform.position, Range, _turretsInRange);
        //    EventBus.Publish(new BuffTurretsEvent { Count = _turretsInRange.Count });

        //    float dmgAura = Data.SupportDamageAura;
        //    if (_statModifiers != null) dmgAura *= _statModifiers.GetSupportAuraMultiplier();
        //    float spdAura = 0f;
        //    float enemySlow = 0f;

        //    if (IsEvolved && EvolutionPath == 0) // Buffer (TU006A)
        //        spdAura = Data.PathA?.AttackSpeedAura ?? Data.SupportSpeedAura;
        //        if (_statModifiers != null) spdAura *= _statModifiers.GetSupportAuraMultiplier();
        //    if (IsEvolved && EvolutionPath == 1) // Debuffer (TU006B)
        //        enemySlow = Data.PathB?.EnemySlowAura ?? Data.SupportEnemySlowAura;

        //    for (int i = 0; i < _turretsInRange.Count; i++)
        //    {
        //        var t = _turretsInRange[i];
        //        if (t == this) continue;
        //        t.ApplyExternalBuff(dmgAura, spdAura > 0, false);
        //        //if (spdAura > 0)
        //        //    t._externalSpeedMultiplier = 1f + spdAura;
        //    }

        //    // Debuffer (TU006B): slow enemies in aura
        //    if (enemySlow > 0)
        //    {
        //        _enemyManager?.GetEnemiesInRange(transform.position, Range, _enemiesInRange);
        //        for (int i = 0; i < _enemiesInRange.Count; i++)
        //            _enemiesInRange[i].ApplyStatus(StatusEffectType.Slow, enemySlow, 1f);
        //    }
        //}

        /// <summary>
        /// True when this controller contributes a support aura. Static turret auras
        /// are reconciled by <see cref="SupportAuraSystem"/> instead of every support
        /// tower repeatedly scanning nearby towers.
        /// </summary>
        internal bool IsSupportTurret => Data != null && Data.Type == TurretType.Support;

        internal void EnsureSupportAuraVisual()
        {
            if (_visualConfig != null && _visualConfig.AuraVFXObject != null &&
                !_visualConfig.AuraVFXObject.activeSelf)
            {
                _visualConfig.AuraVFXObject.SetActive(true);
            }
        }

        /// <summary>
        /// Returns the current immutable aura profile for this support turret.
        /// This is evaluated only when topology/aura stats change, not per target tick.
        /// </summary>
        internal bool TryGetSupportAuraProfile(
            out float radius,
            out float damageBonus,
            out float speedBonus,
            out float enemySlow)
        {
            radius = 0f;
            damageBonus = 0f;
            speedBonus = 0f;
            enemySlow = 0f;

            if (!IsSupportTurret)
                return false;

            radius = Range;
            if (radius <= 0f)
                return false;

            float auraMultiplier = _statModifiers != null
                ? Mathf.Max(0f, _statModifiers.GetSupportAuraMultiplier())
                : 1f;

            int levelIndex = Mathf.Max(0, Level - 1);
            float tier2AuraBonus = _cachedTier2Evolution != null ? _cachedTier2Evolution.SupportDamageAuraBonus : 0f;
            damageBonus = Mathf.Max(
                0f,
                Data.SupportDamageAura + Data.SupportDamageAuraPerLevel * levelIndex + tier2AuraBonus) * auraMultiplier;

            if (IsEvolved && EvolutionPath == 0)
            {
                float baseSpeedBonus = Data.PathA != null && Data.PathA.AttackSpeedAura > 0f
                    ? Data.PathA.AttackSpeedAura
                    : Data.SupportSpeedAura;

                speedBonus = Mathf.Max(
                    0f,
                    baseSpeedBonus + Data.SupportSpeedAuraPerLevel * levelIndex) * auraMultiplier;
            }
            else if (IsEvolved && EvolutionPath == 1)
            {
                float baseEnemySlow = Data.PathB != null && Data.PathB.EnemySlowAura > 0f
                    ? Data.PathB.EnemySlowAura
                    : Data.SupportEnemySlowAura;

                enemySlow = Mathf.Clamp01(
                    baseEnemySlow + Data.SupportEnemySlowAuraPerLevel * levelIndex);
            }

            return true;
        }

        /// <summary>
        /// Applies the already-resolved strongest aura values for this turret. Called
        /// only when the resolved value changes, avoiding repeated RecalculateStats()
        /// for every support/target pair.
        /// </summary>
        internal void SetResolvedSupportAura(float damageBonus, float speedBonus)
        {
            damageBonus = Mathf.Max(0f, damageBonus);
            speedBonus = Mathf.Max(0f, speedBonus);

            if (Mathf.Approximately(_supportDamageBonus, damageBonus) &&
                Mathf.Approximately(_supportSpeedBonus, speedBonus))
            {
                return;
            }

            _supportDamageBonus = damageBonus;
            _supportSpeedBonus = speedBonus;
            // Resolved static auras have explicit removal through SupportAuraSystem;
            // they must not expire simply because a periodic refresh was skipped.
            _supportAuraExpireTime = 0f;
            RecalculateStats();
        }

        private void UpdateSupport(float dt)
        {
            // Expensive range queries, repeated aura applications and challenge events
            // are intentionally owned by SupportAuraSystem. This method stays so the
            // scheduler can maintain legacy support prefabs, but is now O(1).
            EnsureSupportAuraVisual();
        }



        // =================================================================
        // RADAR TURRET
        // =================================================================

        private void UpdateRadar(float dt)
        {
            if (_visualConfig != null && _visualConfig.RadarVFXObject != null && !_visualConfig.RadarVFXObject.activeSelf)
                _visualConfig.RadarVFXObject.SetActive(true);

            _radarTickTimer -= dt;
            if (_radarTickTimer > 0f)
                return;
            _radarTickTimer = Mathf.Max(0.05f, _radarUpdateInterval);

            if (_enemyManager == null || Data == null)
            {
                ReleaseRadarReveals();
                return;
            }

            // Reveal range grows with upgrades — the only stat a radar level buys
            // (Damage/AttackSpeed are meaningless for a turret that never attacks).
            float revealRange = (Data.RevealRange > 0 ? Data.RevealRange : Range)
                + Mathf.Max(0f, Data.RevealRangePerLevel) * (Level - 1);

            Profiler.BeginSample("Turret.Radar.GetStealthEnemiesInRange");
            _enemyManager.GetStealthEnemiesInRange(transform.position, revealRange, _enemiesInRange);
            Profiler.EndSample();

            Profiler.BeginSample("Turret.Radar.ApplyRevealWindow");
            for (int i = 0; i < _enemiesInRange.Count; i++)
            {
                EnemyController enemy = _enemiesInRange[i];
                if (enemy == null || !enemy.IsStealth || enemy.IsDead)
                    continue;

                if (!ContainsEnemyReference(_radarRevealedEnemies, enemy))
                {
                    _radarRevealedEnemies.Add(enemy);
                    enemy.AddRadarReveal();
                }
            }

            for (int i = _radarRevealedEnemies.Count - 1; i >= 0; i--)
            {
                EnemyController enemy = _radarRevealedEnemies[i];
                if (enemy == null || enemy.IsDead || !ContainsEnemyReference(_enemiesInRange, enemy))
                {
                    if (enemy != null)
                        enemy.RemoveRadarReveal();

                    int last = _radarRevealedEnemies.Count - 1;
                    _radarRevealedEnemies[i] = _radarRevealedEnemies[last];
                    _radarRevealedEnemies.RemoveAt(last);
                }
            }
            Profiler.EndSample();
        }

        private static bool ContainsEnemyReference(List<EnemyController> list, EnemyController enemy)
        {
            if (enemy == null || list == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == enemy)
                    return true;
            }

            return false;
        }

        private void ReleaseRadarReveals()
        {
            if (_radarRevealedEnemies.Count == 0)
                return;

            for (int i = 0; i < _radarRevealedEnemies.Count; i++)
            {
                EnemyController enemy = _radarRevealedEnemies[i];
                if (enemy != null)
                    enemy.RemoveRadarReveal();
            }

            _radarRevealedEnemies.Clear();
        }

        // =================================================================
        // EXTERNAL BUFFS
        // =================================================================

        //public void ApplyExternalBuff(float damageBuff, bool attackSpeedBuff, bool enemyDebuff)
        //{
        //    _externalDamageMultiplier = 1f + damageBuff;
        //    if (attackSpeedBuff) _externalSpeedMultiplier = 1f + damageBuff;
        //    RecalculateStats();
        //}

        public void ApplySupportAura(float damageBonus, float speedBonus, float duration)
        {
            // Support auras should not stack infinitely. Strongest support aura wins.
            float newDamageBonus = Mathf.Max(_supportDamageBonus, Mathf.Max(0f, damageBonus));
            float newSpeedBonus = Mathf.Max(_supportSpeedBonus, Mathf.Max(0f, speedBonus));
            float newExpireTime = Mathf.Max(_supportAuraExpireTime, Time.time + Mathf.Max(0.05f, duration));

            bool statsChanged =
                !Mathf.Approximately(newDamageBonus, _supportDamageBonus) ||
                !Mathf.Approximately(newSpeedBonus, _supportSpeedBonus);

            _supportAuraExpireTime = newExpireTime;

            if (!statsChanged)
                return;

            _supportDamageBonus = newDamageBonus;
            _supportSpeedBonus = newSpeedBonus;

            RecalculateStats();
        }

        public void ApplyEnemyDamageDebuff(float damagePenalty, float duration)
        {
            // Positive value. Example: 0.18 means -18% damage.
            float newPenalty = Mathf.Max(_enemyDamageDebuff, Mathf.Clamp01(damagePenalty));
            float newExpireTime = Mathf.Max(_enemyDebuffExpireTime, Time.time + Mathf.Max(0.05f, duration));

            bool statsChanged = !Mathf.Approximately(newPenalty, _enemyDamageDebuff);
            _enemyDebuffExpireTime = newExpireTime;

            if (!statsChanged)
                return;

            _enemyDamageDebuff = newPenalty;

            RecalculateStats();
        }

        private void RefreshExternalEffects(float now)
        {
            bool changed = false;

            if (_supportAuraExpireTime > 0f && now > _supportAuraExpireTime)
            {
                _supportDamageBonus = 0f;
                _supportSpeedBonus = 0f;
                _supportAuraExpireTime = 0f;
                changed = true;
            }

            if (_enemyDebuffExpireTime > 0f && now > _enemyDebuffExpireTime)
            {
                _enemyDamageDebuff = 0f;
                _enemyDebuffExpireTime = 0f;
                changed = true;
            }

            if (changed)
                RecalculateStats();
        }



        // =================================================================
        // UPGRADES & EVOLUTION
        // =================================================================

        public int GetUpgradeCost()
        {
            float cost = Data.BaseUpgradeCost * Mathf.Pow(Data.UpgradeCostMultiplier, Level - 1);

            // Dynamic tile upgrade cost modifier
            if (_dynamicTile != null)
            {
                if (_dynamicTile.PrimaryEffect.Stat == TurretStatModifier.StatType.UpgradeCost)
                    cost *= (1f + _dynamicTile.PrimaryEffect.Value);
                if (_dynamicTile.Tradeoff.Stat == TurretStatModifier.StatType.UpgradeCost)
                    cost *= (1f + _dynamicTile.Tradeoff.Value);
            }

            if (_statModifiers != null)
                cost *= Mathf.Max(0f, _statModifiers.GetUpgradeCostMultiplier());

            return Mathf.Max(1, Mathf.RoundToInt(cost));
        }

        /// <summary>
        /// v1.0 level-25 Tier2 evolution gold cost. Tier1's evolution is free (unlike
        /// every other turret upgrade), but Tier2 is a bigger power spike, so it's
        /// gated — reuses the per-level upgrade cost formula rather than authoring a
        /// new tunable per turret, since GetUpgradeCost() already scales to a hefty
        /// number by level 25.
        /// </summary>
        public int GetTier2EvolveCost() => GetUpgradeCost();

        public void Upgrade()
        {
            TotalGoldInvested += GetUpgradeCost();
            Level++;
            RecalculateStats();
            ApplyUpgradeVisualScale(true);
            if (IsSupportTurret)
                _turretManager?.MarkSupportAurasDirty();

            _vfxConfig?.SpawnUpgrade();
            _soundConfig?.PlayUpgrade();

            EventBus.Publish(new TurretUpgradedEvent
            {
                TurretId = InstanceId,
                NewLevel = Level
            });

            if (Level >= Data.EvolveLevel && !IsEvolved && Data.Type != TurretType.Radar)
            {
                // Path A/B is still a real player choice, but it no longer pauses
                // the game — the panel just shows over live gameplay.
                EventBus.Publish(new ShowEvolveChoiceEvent { TurretId = InstanceId });
            }
            else if (IsEvolved && !IsEvolvedTier2 && Data.Tier2 != null &&
                Level >= Data.EvolveLevel2 && Data.Type != TurretType.Radar)
            {
                // Tier2 has no choice to make (single shared upgrade), so it
                // evolves immediately with no confirm button and no pause.
                EvolveTier2();
            }
        }

        public void UpgradeSilent()
        {
            
            Level++;
            RecalculateStats();
            ApplyUpgradeVisualScale(false);
            if (IsSupportTurret)
                _turretManager?.MarkSupportAurasDirty();

            

            if (Level >= Data.EvolveLevel && !IsEvolved && Data.Type != TurretType.Radar)
            {
                //GameManager.Instance.PushModalState(GameState.EvolveChoice);
                //EventBus.Publish(new ShowEvolveChoiceEvent { TurretId = InstanceId });
            }
        }
        public void Evolve(int path, bool publishEvent = true)
        {
            IsEvolved = true;
            EvolutionPath = path;

            // Evolution is treated as a new visual form. Keep gameplay Level/stats,
            // but restart visual scale progression from this evolved form's
            // inspector min/max/steps values.
            _upgradeVisualScaleStartLevel = Mathf.Max(1, Level);

            RefreshEvolutionAttackCache();
            RecalculateStats();
            ApplyUpgradeVisualScale(false);
            RefreshProjectileCache();
            if (IsSupportTurret)
                _turretManager?.MarkSupportAurasDirty();

            if (publishEvent)
            {
                _vfxConfig?.SpawnEvolve();
                _soundConfig?.PlayEvolve();

                EventBus.Publish(new TurretEvolvedEvent
                {
                    TurretId = InstanceId,
                    EvolutionPath = path
                });
            }
        }

        /// <summary>
        /// v1.0 level-25 shared second evolution tier. Unlike Evolve(), there is no
        /// path argument — Tier2 converges from whichever Path A/B was chosen at the
        /// first evolution, so it's a single confirm rather than an A/B choice.
        /// </summary>
        public void EvolveTier2(bool publishEvent = true)
        {
            if (!IsEvolved || IsEvolvedTier2 || Data == null || Data.Tier2 == null)
                return;

            IsEvolvedTier2 = true;

            RefreshEvolutionAttackCache();
            RecalculateStats();
            RefreshProjectileCache();
            if (IsSupportTurret)
                _turretManager?.MarkSupportAurasDirty();

            if (publishEvent)
            {
                _vfxConfig?.SpawnEvolve();
                _soundConfig?.PlayEvolve();

                EventBus.Publish(new TurretEvolvedEvent
                {
                    TurretId = InstanceId,
                    EvolutionPath = EvolutionPath
                });
            }
        }

        public int GetSellValue()
        {
            return Mathf.RoundToInt(TotalGoldInvested * GameConstants.TURRET_SELL_REFUND_PERCENT);
        }

        /// <summary>
        /// Gold multiplier from tile (Fragile Curse: +80% gold)
        /// </summary>
        public float GetGoldMultiplierFromTile()
        {
            if (_dynamicTile == null) return 1f;
            float mult = 1f;
            if (_dynamicTile.PrimaryEffect.Stat == TurretStatModifier.StatType.GoldFromKills)
                mult += _dynamicTile.PrimaryEffect.Value;
            if (_dynamicTile.Tradeoff.Stat == TurretStatModifier.StatType.GoldFromKills)
                mult += _dynamicTile.Tradeoff.Value;
            return mult;
        }

        private void OnDestroy()
        {
            ReleaseRadarReveals();
            TurretUpdateScheduler.Unregister(this);
            _soundConfig?.StopLaserLoop();
        }

        private int SpawnProjectileFromConfig(EnemyController target, float damage,
            StatusEffectType status, float statusValue, float statusDuration, bool isCritical = false)
        {
            if (target == null || target.IsDead)
                return 0;

            if (_projectileManager == null)
            {
                target.TakeDamage(damage, status, statusValue, statusDuration, isCritical: isCritical,
                    sourceTurretType: (int)Data.Type, sourceTurretId: InstanceId);
                ApplyOnHitSpecials(target, damage, status, statusValue, statusDuration);
                return 1;
            }

            Transform[] firePoints = _cachedFirePoints;
            bool hasFirePoints = _cachedHasFirePoints;
            int count = Mathf.Max(1, _cachedFirePointCount);
            float perShotDamage = count > 1 ? damage / count : damage;

            if (_cachedDoubleProjectile)
            {
                count = Mathf.Max(2, count);
                perShotDamage = damage * _cachedDoubleProjectileDamageMultiplier;
            }

            int firedCount = 0;
            int visualProjectilesSpawned = 0;
            int maxVisualProjectiles = Mathf.Max(0, _maxVisualProjectilesPerAttack);
            bool playedShootSound = false;
            bool playedMuzzleVFX = false;

            Profiler.BeginSample("DIAG.FallbackPosition");
            Vector3 fallbackPosition = _cachedTransform != null ? _cachedTransform.position : transform.position;
            Profiler.EndSample();

            Profiler.BeginSample("DIAG.PoolHandleRefresh");
            // Refresh stale cache only if the projectile manager was not available
            // when this turret initialized. This never runs in the normal fire path.
            if (!_cachedProjectilePoolHandle.IsValid && _cachedProjectilePrefab != null)
                _cachedProjectilePoolHandle = _projectileManager.GetPoolHandle(_cachedProjectilePrefab);
            Profiler.EndSample();

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = fallbackPosition;
                if (hasFirePoints && i < firePoints.Length && firePoints[i] != null)
                    spawnPos = firePoints[i].position;

                // At high fire rates a multi-shot volley needs one visual cue, not a
                // GameObject for every logical projectile. Extra shots resolve through
                // the same direct-hit path, preserving damage, statuses and on-hit traits.
                bool requestVisual = visualProjectilesSpawned < maxVisualProjectiles &&
                    _projectileManager.ShouldSpawnVisualProjectile(ref _visualProjectileSequence);

                Projectile projectile = null;
                if (requestVisual)
                {
                    Profiler.BeginSample("DIAG.SpawnProjectile.SpawnVisual");
                    projectile = _projectileManager.SpawnProjectile(
                        _cachedProjectilePoolHandle,
                        spawnPos,
                        target,
                        perShotDamage,
                        15f,
                        status,
                        statusValue,
                        statusDuration,
                        isCritical,
                        null,
                        this,
                        requestVisual: true,
                        sourceTurretType: (int)Data.Type,
                        sourceTurretId: InstanceId);
                    Profiler.EndSample();
                }
                else
                {
                    Profiler.BeginSample("DIAG.SpawnProjectile.ResolveDirectHit");
                    _projectileManager.ResolveDirectHit(
                        target,
                        perShotDamage,
                        status,
                        statusValue,
                        statusDuration,
                        isCritical,
                        null,
                        this,
                        sourceTurretType: (int)Data.Type,
                        sourceTurretId: InstanceId);
                    Profiler.EndSample();
                }

                if (projectile != null)
                {
                    visualProjectilesSpawned++;
                    if (_cachedHitVFX != null)
                    {
                        Profiler.BeginSample("DIAG.SpawnProjectile.HitVFXAssign");
                        projectile.HitVFXPrefab = _cachedHitVFX;
                        projectile.HitVFXAttachTarget = target.transform;
                        Profiler.EndSample();
                    }

                    if (!playedMuzzleVFX)
                    {
                        Profiler.BeginSample("DIAG.SpawnProjectile.MuzzleVFX");
                        _muzzleVFX?.OnFire(i);
                        Profiler.EndSample();
                        playedMuzzleVFX = true;
                    }
                }

                firedCount++;

                if (!playedShootSound)
                {
                    Profiler.BeginSample("DIAG.SpawnProjectile.PlayShootSound");
                    _soundConfig?.PlayShoot(spawnPos);
                    Profiler.EndSample();
                    playedShootSound = true;
                }
            }

            if (firedCount > 0 && Data.Type == TurretType.Basic && IsEvolved && EvolutionPath == 0)
                EventBus.Publish(new SniperDamageEvent { Damage = damage });

            return firedCount;
        }

        /// <summary>
        /// Restores state after a live evolution prefab swap. This is not a save-load
        /// restore: the evolved turret should visually start fresh from its own
        /// inspector scale values, even if the old gameplay level is preserved.
        /// </summary>
        public void RestoreState(int level, int totalGoldInvested, int evolutionPath)
        {
            RestoreSnapshotStateInternal(level, totalGoldInvested, true, evolutionPath, false,
                resetScaleProgressToCurrentLevel: true);
        }

        public void RestoreSnapshotState(int level, int totalGoldInvested, bool isEvolved, int evolutionPath,
            bool isEvolvedTier2 = false)
        {
            RestoreSnapshotStateInternal(level, totalGoldInvested, isEvolved, evolutionPath, isEvolvedTier2,
                resetScaleProgressToCurrentLevel: false);
        }

        private void RestoreSnapshotStateInternal(int level, int totalGoldInvested, bool isEvolved,
            int evolutionPath, bool isEvolvedTier2, bool resetScaleProgressToCurrentLevel)
        {
            Level = Mathf.Max(1, level);
            TotalGoldInvested = Mathf.Max(0, totalGoldInvested);
            IsEvolved = isEvolved;
            EvolutionPath = isEvolved ? evolutionPath : -1;
            IsEvolvedTier2 = isEvolved && isEvolvedTier2 && Data != null && Data.Tier2 != null;

            if (IsEvolved)
            {
                if (resetScaleProgressToCurrentLevel)
                {
                    // Live evolution: evolved visual starts at min scale right now.
                    _upgradeVisualScaleStartLevel = Level;
                }
                else
                {
                    // Save/load restore: we do not currently save the exact evolution
                    // level, so approximate visual progress from the configured evolve
                    // level. This avoids loading evolved turrets at base-turret scale.
                    int evolveLevel = Data != null ? Mathf.Max(1, Data.EvolveLevel) : Level;
                    _upgradeVisualScaleStartLevel = Mathf.Clamp(evolveLevel, 1, Level);
                }
            }
            else
            {
                _upgradeVisualScaleStartLevel = 1;
            }

            RefreshEvolutionAttackCache();

            RecalculateStats();
            ApplyUpgradeVisualScale(false);
            _visualConfig = GetComponent<TurretVisualConfig>();
            RefreshProjectileCache();
            if (IsSupportTurret)
                _turretManager?.MarkSupportAurasDirty();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugSetLevelForShowcase(int level)
        {
            Level = Mathf.Max(1, level);

            // Keep sell value sane enough for dev captures.
            if (TotalGoldInvested <= 0 && Data != null)
                TotalGoldInvested = Mathf.Max(0, Data.Cost);

            RecalculateStats();
            ApplyUpgradeVisualScale(false);
            if (IsSupportTurret)
                _turretManager?.MarkSupportAurasDirty();
        }
#endif

    }
}
