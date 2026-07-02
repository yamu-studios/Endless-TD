using UnityEngine;

namespace ETD.Waves
{
    /// <summary>
    /// Development-build-only wave jump hotkeys.
    /// Attach this to the same GameObject as WaveManager, or assign _waveManager manually.
    /// K = spawn wave 120, L = spawn wave 720.
    /// </summary>
    public sealed class DevelopmentWaveHotkeys : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("References")]
        [SerializeField] private WaveManager _waveManager;

        [Header("Hotkeys")]
        [SerializeField] private KeyCode _wave120Key = KeyCode.K;
        [SerializeField] private KeyCode _wave720Key = KeyCode.L;
        [SerializeField] private int wave1;
        [SerializeField] private int wave2;

        [Header("Behavior")]
        [SerializeField] private bool _clearExistingEnemies = true;
        [SerializeField] private bool _publishWaveStartedEvent = true;
        [SerializeField] private bool _forceWaveActiveState = true;

        private void Awake()
        {
            ResolveWaveManager();
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(_wave120Key))
                JumpToWave(wave1);

            if (UnityEngine.Input.GetKeyDown(_wave720Key))
                JumpToWave(wave2);
        }

        private void ResolveWaveManager()
        {
            if (_waveManager != null)
                return;

            _waveManager = GetComponent<WaveManager>();

            if (_waveManager == null)
                _waveManager = FindFirstObjectByType<WaveManager>();
        }

        private void JumpToWave(int wave)
        {
            ResolveWaveManager();

            if (_waveManager == null)
            {
                Debug.LogError($"[DevelopmentWaveHotkeys] Cannot jump to wave {wave}. No WaveManager found.");
                return;
            }

            _waveManager.DebugSpawnSpecificWaveForMarketing(
                wave,
                clearExistingEnemies: _clearExistingEnemies,
                publishWaveStartedEvent: _publishWaveStartedEvent,
                forceWaveActiveState: _forceWaveActiveState);

            Debug.Log($"[DevelopmentWaveHotkeys] Jumped to wave {wave}.");
        }
#else
        private void Awake()
        {
            enabled = false;
        }
#endif
    }
}
