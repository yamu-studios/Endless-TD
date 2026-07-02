// ============================================================================
// ETD.Turrets - TurretVisualConfig.cs  [NEW]
// Attach to each turret prefab. Defines per-turret visual behavior:
// projectile prefab, fire points, VFX, beam settings.
// TurretController reads this for visual attacks.
// ============================================================================
using UnityEngine;

namespace ETD.Turrets
{
    public enum TurretVisualType
    {
        Projectile,        // Basic, Frost, Inferno (base), Lightning
        DoubleProjectile,  // Basic Path A (Double Gun)
        BigProjectile,     // Basic Path B (Sniper), Frost evolutions, Inferno Path B
        FlameVFX,          // Inferno Path A (Flamethrower)
        ContinuousLaser,   // Laser base, Laser Path B (Stacker)
        MultiLaser,        // Laser Path A (Multitarget)
        SupportAura,       // Support base + evolutions
        RadarSpin          // Radar
    }

    public class TurretVisualConfig : MonoBehaviour
    {
        [Header("Visual Type")]
        public TurretVisualType VisualType = TurretVisualType.Projectile;

        [Header("Projectile")]
        [Tooltip("The projectile prefab this turret fires")]
        public GameObject ProjectilePrefab;

        [Header("Fire Points")]
        [Tooltip("Where projectiles spawn. Single point for most, two for Double Gun")]
        public Transform[] FirePoints;


        [Header("Evolved Visuals (optional overrides)")]
        [Tooltip("Override projectile for evolved form (e.g., bigger version)")]
        public GameObject EvolvedProjectilePrefab;
        [Tooltip("Override fire points for evolved form")]
        public Transform[] EvolvedFirePoints;

        [Header("Flame VFX (Flamethrower only)")]
        [Tooltip("Flame particle system or VFX object")]
        public GameObject FlameVFXObject;
        [Tooltip("BoxCollider on the flame for damage detection")]
        public BoxCollider FlameDamageCollider;

        [Header("Laser (Laser types only)")]
        [Tooltip("LineRenderer(s) for beam visuals")]
        public LineRenderer[] LaserRenderers;

        [Header("Support Aura VFX")]
        [Tooltip("Continuous VFX that plays while support is active (shrink/enlarge anim)")]
        public GameObject AuraVFXObject;

        [Header("Radar VFX")]
        [Tooltip("Radar sweep effect that rotates continuously")]
        public GameObject RadarVFXObject;
        public float RadarRotationSpeed = 90f; // degrees per second

        /// <summary>
        /// Get the correct projectile prefab based on evolution state.
        /// </summary>
        public GameObject GetProjectilePrefab(bool isEvolved)
        {
            if (isEvolved && EvolvedProjectilePrefab != null)
                return EvolvedProjectilePrefab;
            return ProjectilePrefab;
        }

        /// <summary>
        /// Get the correct fire points based on evolution state.
        /// </summary>
        public Transform[] GetFirePoints(bool isEvolved)
        {
            if (isEvolved && EvolvedFirePoints != null && EvolvedFirePoints.Length > 0)
                return EvolvedFirePoints;
            return FirePoints;
        }

        private void Update()
        {
            // Radar continuous rotation
            if (VisualType == TurretVisualType.RadarSpin && RadarVFXObject != null)
            {
                RadarVFXObject.transform.Rotate(Vector3.up, RadarRotationSpeed * Time.deltaTime);
            }
        }
    }
}
