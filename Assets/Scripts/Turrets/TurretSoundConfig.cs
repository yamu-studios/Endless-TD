// ============================================================================
// ETD.Turrets - TurretSoundConfig.cs  [NEW]
// Attach to each turret prefab alongside TurretVisualConfig.
// Holds all AudioClips for this turret type.
// TurretController reads this to play sounds.
// ============================================================================
using UnityEngine;
using ETD.Core;

namespace ETD.Turrets
{
    [RequireComponent(typeof(TurretVisualConfig))]
    public class TurretSoundConfig : MonoBehaviour
    {
        [Header("Shooting")]
        [Tooltip("Played from muzzle on each shot. Use multiple for variation.")]
        public AudioClip[] ShootSounds;

        [Header("Placement & Management")]
        public AudioClip PlaceSound;
        public AudioClip SellSound;
        public AudioClip UpgradeSound;
        public AudioClip EvolveSound;

        [Header("Hit Sounds (when projectile hits enemy)")]
        public AudioClip[] HitSounds;

        [Header("Special Sounds")]
        [Tooltip("Laser hum — loops while beam is active")]
        public AudioClip LaserLoopSound;
        [Tooltip("Chain lightning crackle")]
        public AudioClip ChainLightningSound;
        [Tooltip("Flamethrower roar — loops")]
        public AudioClip FlameLoopSound;
        [Tooltip("Support aura pulse")]
        public AudioClip SupportPulseSound;
        [Tooltip("Radar sweep beep")]
        public AudioClip RadarBeepSound;

        [Header("Pitch Variation")]
        public float PitchMin = 0.92f;
        public float PitchMax = 1.08f;

        private void Start()
        {
            PreloadAllClips();
        }

        private void PreloadAllClips()
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null)
                return;

            audio.PreloadClips(ShootSounds);
            audio.PreloadClips(HitSounds);
            audio.PreloadClip(PlaceSound);
            audio.PreloadClip(SellSound);
            audio.PreloadClip(UpgradeSound);
            audio.PreloadClip(EvolveSound);
            audio.PreloadClip(LaserLoopSound);
            audio.PreloadClip(ChainLightningSound);
            audio.PreloadClip(FlameLoopSound);
            audio.PreloadClip(SupportPulseSound);
            audio.PreloadClip(RadarBeepSound);
        }

        // ================================================================
        // HELPERS — called by TurretController
        // ================================================================

        public void PlayShoot(Vector3 pos)
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null || ShootSounds == null || ShootSounds.Length == 0) return;
            //audio.PlaySFXRandom(ShootSounds, pos,
            //    SoundCategory.TurretShoot, 1f, PitchMin, PitchMax);
        }

        public void PlayHit(Vector3 pos)
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null || HitSounds == null || HitSounds.Length == 0) return;
           // audio.PlaySFXRandom(HitSounds, pos,
           //     SoundCategory.ProjectileHit, 0.8f, PitchMin, PitchMax);
        }

        public void PlayPlace()
        {
            if (AudioManager.Instance == null || PlaceSound == null) return;
            AudioManager.Instance.PlaySFX(PlaceSound, SoundCategory.TurretPlace);
        }

        public void PlaySell()
        {
            if (AudioManager.Instance == null || SellSound == null) return;
            AudioManager.Instance.PlaySFX(SellSound, SoundCategory.TurretSell);
        }

        public void PlayUpgrade()
        {
            if (AudioManager.Instance == null || UpgradeSound == null) return;
            AudioManager.Instance.PlaySFX(UpgradeSound, SoundCategory.TurretUpgrade);
        }

        public void PlayEvolve()
        {
            if (AudioManager.Instance == null || EvolveSound == null) return;
            AudioManager.Instance.PlaySFX(EvolveSound, SoundCategory.TurretEvolve);
        }

        public void PlayChainLightning(Vector3 pos)
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null || ChainLightningSound == null) return;
            audio.PlaySFX3D(ChainLightningSound, pos,
                SoundCategory.ChainLightning, 1f, Random.Range(PitchMin, PitchMax));
        }

        public void PlayRadarBeep()
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null || RadarBeepSound == null) return;
            audio.PlaySFX(RadarBeepSound, SoundCategory.Ambient, 0.5f);
        }

        // =================================================================
        // LOOPING SOUNDS — laser hum, flame roar
        // =================================================================

        /// <summary>Start laser hum loop. Safe to call every frame — won't restart if already playing.</summary>
        public void StartLaserLoop()
            => AudioManager.Instance?.StartLoop(LaserLoopSound, gameObject, 0.8f);

        /// <summary>Stop laser hum.</summary>
        public void StopLaserLoop()
            => AudioManager.Instance?.StopLoop(gameObject);

        /// <summary>Start flame roar loop.</summary>
        public void StartFlameLoop()
            => AudioManager.Instance?.StartLoop(FlameLoopSound, gameObject, 0.9f);

        /// <summary>Stop flame roar.</summary>
        public void StopFlameLoop()
            => AudioManager.Instance?.StopLoop(gameObject);

        
        private void OnDestroy()
        {
            AudioManager.Instance?.StopLoop(gameObject);
        }
    }
}
