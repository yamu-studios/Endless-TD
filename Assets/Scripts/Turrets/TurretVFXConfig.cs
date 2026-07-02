// ============================================================================
// ETD.Turrets - TurretVFXConfig.cs  [UPDATED]
// Hit VFX spawns at hit point AND parents to the enemy transform
// so it moves with the enemy until the particle finishes.
// ============================================================================
using UnityEngine;
using ETD.Core;

namespace ETD.Turrets
{
    public class TurretVFXConfig : MonoBehaviour
    {
        [Header("Projectile Hit Effect")]
        [Tooltip("Spawned at hit point, parented to enemy so it moves with them")]
        public GameObject HitVFX;

        [Header("Chain Lightning Arc")]
        [Tooltip("LineRenderer prefab drawn between chained enemies")]
        public GameObject ChainArcVFX;

        [Header("Turret State Events")]
        public GameObject PlaceVFX;
        public GameObject SellVFX;
        public GameObject UpgradeVFX;
        public GameObject EvolveVFX;

        // =================================================================
        // HIT VFX — attaches to hit enemy, moves with it
        // =================================================================

        public void SpawnHitAttached(Vector3 hitPos, Transform enemyTransform)
        {
            if (VFXManager.Instance == null || HitVFX == null) return;

            // Spawn attached to enemy so VFX follows movement
            VFXManager.Instance.SpawnAttached(HitVFX, enemyTransform,
                Vector3.up * 0.3f); // slight upward offset from root
        }

        // =================================================================
        // TURRET EVENT VFX
        // =================================================================

        public void SpawnPlace()
        {
            if (VFXManager.Instance != null && PlaceVFX != null)
                VFXManager.Instance.Spawn(PlaceVFX, transform.position);
        }

        public void SpawnSell()
        {
            if (VFXManager.Instance != null && SellVFX != null)
                VFXManager.Instance.Spawn(SellVFX, transform.position);
        }

        public void SpawnUpgrade()
        {
            if (VFXManager.Instance != null && UpgradeVFX != null)
                VFXManager.Instance.Spawn(UpgradeVFX, transform.position);
        }

        public void SpawnEvolve()
        {
            if (VFXManager.Instance != null && EvolveVFX != null)
                VFXManager.Instance.Spawn(EvolveVFX, transform.position);
        }
    }
}
