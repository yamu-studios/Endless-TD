// ============================================================================
// ETD.Enemies - EnemyVFXConfig.cs  [UPDATED]
// Hit flash: one-shot white flash on projectile hit.
// Laser pulse: periodic flash while laser is hitting (not constant white).
// Death VFX: spawned from pool.
// ============================================================================
using System.Collections;
using UnityEngine;
using ETD.Core;

namespace ETD.Enemies
{
    public class EnemyVFXConfig : MonoBehaviour
    {
        [Header("Hit Flash (projectile impact)")]
        [SerializeField] private float _hitFlashDuration = 0.08f;

        [Header("Laser Pulse Flash (periodic while laser hits)")]
        [SerializeField] private float _laserPulseInterval = 0.25f;
        [SerializeField] private float _laserPulseDuration = 0.06f;

        [Header("Death VFX")]
        [SerializeField] private GameObject _deathVFX;

        private Renderer[] _renderers;
        private Material[][] _originalMaterials;
        private Material[][] _flashMaterials;
        [SerializeField]private Material _flashMat;
        [SerializeField]private Material _revealedMat;
        [SerializeField]private Renderer _ghostRenderer;
        private bool _isFlashing;
        private Coroutine _laserPulseCoroutine;
        private WaitForSeconds _hitFlashWait;
        private WaitForSeconds _laserPulseFlashWait;
        private WaitForSeconds _laserPulseIntervalWait;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _originalMaterials = new Material[_renderers.Length][];
            _flashMaterials = new Material[_renderers.Length][];

            // FIX (profiler-confirmed, wave-55+ capture): Renderer.materials (the
            // instance property) clones every material on every renderer the first
            // time it's touched — and can trigger shader variant compilation if that
            // shader hasn't been used yet. Confirmed in profiler: a single enemy's
            // first-ever activation cost 126.80ms self-time in
            // GameObject.ActivateAwakeRecursively, entirely inside this Awake(), with
            // no child sample to attribute it to.
            //
            // This array is only ever used to RESTORE appearance after a flash
            // (assigned wholesale back via sharedMaterials in RestoreMaterials)
            // — it is never mutated in place. sharedMaterials returns the
            // actual shared asset references with zero cloning, which is exactly what
            // a restore-only snapshot needs.
            //
            // FIX (native-memory leak): the flash arrays are prebuilt here so the
            // per-hit flash never touches Renderer.materials again. The old
            // FlashCoroutine read renderer.materials on EVERY flash, which clones
            // every material into "(Instance)" copies, then immediately orphaned
            // those copies by overwriting the slots with the flash material.
            // Orphaned Materials are native objects that survive until scene
            // unload, so every projectile hit flash leaked one Material per
            // renderer slot for the entire run.
            for (int i = 0; i < _renderers.Length; i++)
            {
                Material[] shared = _renderers[i].sharedMaterials;
                _originalMaterials[i] = shared;

                var flash = new Material[shared.Length];
                for (int m = 0; m < flash.Length; m++)
                    flash[m] = _flashMat;
                _flashMaterials[i] = flash;
            }

            _hitFlashWait = new WaitForSeconds(_hitFlashDuration);
            _laserPulseFlashWait = new WaitForSeconds(_laserPulseDuration);
            _laserPulseIntervalWait = new WaitForSeconds(Mathf.Max(0.01f, _laserPulseInterval - _laserPulseDuration));
        }

        // =================================================================
        // HIT FLASH — single brief flash on projectile impact
        // =================================================================

        public void FlashOnHit()
        {
            if (_isFlashing || !gameObject.activeInHierarchy) return;
            StartCoroutine(FlashCoroutine(_hitFlashWait));
        }

        // =================================================================
        // LASER PULSE — periodic flash while laser beam is active
        // Different from hit flash: it pulses on/off rather than one-shot
        // =================================================================

        public void RevealGhost()
        {
            _ghostRenderer.sharedMaterial = _revealedMat;
        }
        public void StartLaserPulse()
        {
            if (_laserPulseCoroutine != null) return; // already running
            if (!gameObject.activeInHierarchy) return;
            _laserPulseCoroutine = StartCoroutine(LaserPulseCoroutine());
        }

        public void StopLaserPulse()
        {
            if (_laserPulseCoroutine != null)
            {
                StopCoroutine(_laserPulseCoroutine);
                _laserPulseCoroutine = null;
            }
            RestoreMaterials();
        }

        private IEnumerator LaserPulseCoroutine()
        {
            while (true)
            {
                // Wait between pulses
                yield return _laserPulseIntervalWait;

                // Flash
                if (!_isFlashing)
                    yield return FlashCoroutine(_laserPulseFlashWait);
                else
                    yield return _laserPulseFlashWait;
            }
        }

        // =================================================================
        // SHARED FLASH COROUTINE
        // The previous version read renderer.materials here (clones + orphans
        // one Material per slot per flash — a native-memory leak on every hit)
        // and allocated two arrays plus a WaitForSeconds per flash. Swapping
        // prebuilt shared-asset arrays via sharedMaterials renders identically
        // (the flash always replaced every slot with _flashMat regardless of
        // the color parameter) with zero cloning and zero per-flash allocation.
        // =================================================================

        private IEnumerator FlashCoroutine(WaitForSeconds wait)
        {
            _isFlashing = true;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].sharedMaterials = _flashMaterials[i];
            }

            yield return wait;

            RestoreMaterials();
            _isFlashing = false;
        }

        private void RestoreMaterials()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null || _originalMaterials[i] == null) continue;
                _renderers[i].sharedMaterials = _originalMaterials[i];
            }
        }

        // =================================================================
        // DEATH VFX
        // =================================================================

        public void PlayDeathVFX()
        {
            if (VFXManager.Instance != null && _deathVFX != null)
                VFXManager.Instance.Spawn(_deathVFX, transform.position);
        }

        private void OnDisable()
        {
            // Stop pulse when enemy is returned to pool
            if (_laserPulseCoroutine != null)
            {
                StopCoroutine(_laserPulseCoroutine);
                _laserPulseCoroutine = null;
            }
            RestoreMaterials();
            _isFlashing = false;
        }
    }
}
