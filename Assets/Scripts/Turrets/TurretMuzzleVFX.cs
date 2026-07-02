// ============================================================================
// ETD.Turrets - TurretMuzzleVFX.cs  [SPAWN PIPELINE OPTIMIZATION]
// Uses timestamps instead of creating/stopping a Coroutine per shot.
// ============================================================================
using UnityEngine;

namespace ETD.Turrets
{
    public class TurretMuzzleVFX : MonoBehaviour
    {
        [Header("Muzzle VFX GameObjects (disabled children on prefab)")]
        [Tooltip("One entry per fire point. Set the disabled child GO here.")]
        [SerializeField] private GameObject[] _muzzleVFXObjects;

        [Header("Settings")]
        [SerializeField] private float _muzzleOnDuration = 0.08f;

        private float[] _hideAt;
        private int _activeFlashCount;

        private void Awake()
        {
            int count = _muzzleVFXObjects != null ? _muzzleVFXObjects.Length : 0;
            _hideAt = new float[count];

            if (_muzzleVFXObjects != null)
            {
                for (int i = 0; i < _muzzleVFXObjects.Length; i++)
                    _muzzleVFXObjects[i]?.SetActive(false);
            }

            // This component only updates while a flash is visible.
            enabled = false;
        }

        /// <summary>
        /// Shows a muzzle flash without allocating an IEnumerator/Coroutine.
        /// Repeated fire simply extends the existing flash's expiry time.
        /// </summary>
        public void OnFire(int firePointIndex = 0)
        {
            if (_muzzleVFXObjects == null ||
                firePointIndex < 0 ||
                firePointIndex >= _muzzleVFXObjects.Length)
                return;

            GameObject vfx = _muzzleVFXObjects[firePointIndex];
            if (vfx == null)
                return;

            if (!vfx.activeSelf)
            {
                vfx.SetActive(true);
                _activeFlashCount++;
            }

            _hideAt[firePointIndex] = Time.time + Mathf.Max(0.001f, _muzzleOnDuration);
            enabled = true;
        }

        public void OnFireAll()
        {
            if (_muzzleVFXObjects == null) return;
            for (int i = 0; i < _muzzleVFXObjects.Length; i++)
                OnFire(i);
        }

        /// <summary>
        /// Continuous effects are controlled explicitly by laser turrets.
        /// </summary>
        public void SetContinuous(bool active)
        {
            if (_muzzleVFXObjects == null) return;

            _activeFlashCount = 0;
            for (int i = 0; i < _muzzleVFXObjects.Length; i++)
            {
                GameObject vfx = _muzzleVFXObjects[i];
                if (vfx == null) continue;
                vfx.SetActive(active);
                if (active)
                    _activeFlashCount++;
                _hideAt[i] = 0f;
            }

            // Continuous laser VFX never uses the flash timeout update.
            enabled = false;
        }

        private void Update()
        {
            if (_activeFlashCount <= 0 || _muzzleVFXObjects == null)
            {
                enabled = false;
                return;
            }

            float now = Time.time;
            for (int i = 0; i < _muzzleVFXObjects.Length; i++)
            {
                GameObject vfx = _muzzleVFXObjects[i];
                if (vfx == null || !vfx.activeSelf || _hideAt[i] <= 0f || now < _hideAt[i])
                    continue;

                vfx.SetActive(false);
                _hideAt[i] = 0f;
                _activeFlashCount = Mathf.Max(0, _activeFlashCount - 1);
            }

            if (_activeFlashCount == 0)
                enabled = false;
        }
    }
}
