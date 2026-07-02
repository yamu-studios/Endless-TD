using UnityEngine;
using ETD.Core;
using ETD.Waves;

namespace ETD.Gameplay
{
    /// <summary>
    /// Self-bootstrapping demo limiter.
    /// Active in ETD_DEMO_BUILD, and also in Editor when ETD/Demo/Enable Demo Mode In Editor is enabled.
    /// The hard block is also inside WaveManager.StartNextWave so Always Skip Prep cannot start wave 26.
    /// </summary>
    public sealed class DemoRunLimiter : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!DemoMode.IsDemo)
                return;

            if (FindFirstObjectByType<DemoRunLimiter>() != null)
                return;

            var go = new GameObject("ETD Demo Run Limiter");
            DontDestroyOnLoad(go);
            go.AddComponent<DemoRunLimiter>();
        }

        private void Awake()
        {
            if (!DemoMode.IsDemo)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Update()
        {
            if (!DemoMode.IsDemo || DemoMode.DemoCompletedThisSession)
                return;

            if (GameManager.Instance == null)
                return;

            var state = GameManager.Instance.CurrentState;
            if (state == GameState.GameOver || state == GameState.Hub || state == GameState.Boot)
                return;

            if (!ServiceLocator.TryGet<WaveManager>(out var waveManager) || waveManager == null)
                return;

            // Safety net: if wave 25 is finished and the game reaches prep, end the demo.
            // WaveManager also blocks StartNextWave, so AlwaysSkipPrep cannot continue into wave 26.
            if (waveManager.CurrentWave >= DemoMode.MaxPlayableWave && state == GameState.Preparation)
                DemoMode.CompleteDemo(waveManager.CurrentWave);
        }
    }
}
