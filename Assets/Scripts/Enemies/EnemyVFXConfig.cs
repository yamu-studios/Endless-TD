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
        [SerializeField] private Color _hitFlashColor = Color.white;

        [Header("Laser Pulse Flash (periodic while laser hits)")]
        [SerializeField] private float _laserPulseInterval = 0.25f;
        [SerializeField] private float _laserPulseDuration = 0.06f;
        [SerializeField] private Color _laserPulseColor = new Color(0.8f, 1f, 1f); // slight cyan tint

        [Header("Death VFX")]
        [SerializeField] private GameObject _deathVFX;

        private Renderer[] _renderers;
        private Material[][] _originalMaterials;
        [SerializeField]private Material _flashMat;
        [SerializeField]private Material _revealedMat;
        [SerializeField]private Renderer _ghostRenderer;
        private bool _isFlashing;
        private Coroutine _laserPulseCoroutine;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _originalMaterials = new Material[_renderers.Length][];

            // FIX (profiler-confirmed, wave-55+ capture): Renderer.materials (the
            // instance property) clones every material on every renderer the first
            // time it's touched — and can trigger shader variant compilation if that
            // shader hasn't been used yet. Confirmed in profiler: a single enemy's
            // first-ever activation cost 126.80ms self-time in
            // GameObject.ActivateAwakeRecursively, entirely inside this Awake(), with
            // no child sample to attribute it to.
            //
            // This array is only ever used to RESTORE appearance after a flash
            // (assigned wholesale back via "_renderers[i].materials = _originalMaterials[i]"
            // in EndFlash) — it is never mutated in place. sharedMaterials returns the
            // actual shared asset references with zero cloning, which is exactly what
            // a restore-only snapshot needs.
            for (int i = 0; i < _renderers.Length; i++)
                _originalMaterials[i] = _renderers[i].sharedMaterials;
        }

        // =================================================================
        // HIT FLASH — single brief flash on projectile impact
        // =================================================================

        public void FlashOnHit()
        {
            if (_isFlashing || !gameObject.activeInHierarchy) return;
            StartCoroutine(FlashCoroutine(_hitFlashColor, _hitFlashDuration));
        }

        // =================================================================
        // LASER PULSE — periodic flash while laser beam is active
        // Different from hit flash: it pulses on/off rather than one-shot
        // =================================================================

        public void RevealGhost()
        {
            _ghostRenderer.material = _revealedMat;
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
                yield return new WaitForSeconds(_laserPulseInterval - _laserPulseDuration);

                // Flash
                if (!_isFlashing)
                    yield return FlashCoroutine(_laserPulseColor, _laserPulseDuration);
                else
                    yield return new WaitForSeconds(_laserPulseDuration);
            }
        }

        // =================================================================
        // SHARED FLASH COROUTINE
        // =================================================================

        private IEnumerator FlashCoroutine(Color flashColor, float duration)
        {
            _isFlashing = true;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                var mats = _renderers[i].materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    mats[m] = _flashMat;
                   
                }
                _renderers[i].materials = mats;
            }

            yield return new WaitForSeconds(duration);

            RestoreMaterials();
            _isFlashing = false;
        }

        private void RestoreMaterials()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null || _originalMaterials[i] == null) continue;
                _renderers[i].materials = _originalMaterials[i];
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
