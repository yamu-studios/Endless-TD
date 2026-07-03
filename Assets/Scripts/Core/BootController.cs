// ============================================================================
// ETD.Core - BootController.cs  [UPDATED]
// Creates a camera if none exists (fixes "no camera" warning).
// Initializes SceneLoader. Transitions to Hub.
// ============================================================================
using System.Collections;
using UnityEngine;

namespace ETD.Core
{
    public class BootController : MonoBehaviour
    {
        [SerializeField] private float _minSplashTime = 1.5f;

        [Header("Splash UI (optional)")]
        [SerializeField] private Canvas _splashCanvas;

        private IEnumerator Start()
        {
            // Ensure a camera exists (fixes "no camera rendering" warning)
            if (Camera.main == null)
            {
                var camGO = new GameObject("[Boot Camera]");
                var cam = camGO.AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.depth = -10;
            }

            // Initialize save system and apply saved video settings immediately.
            // Without this, a fresh launch runs at the platform-default quality
            // tier (High) until the settings window is opened for the first time.
            VideoSettingsApplier.Apply(SaveSystem.Load());

            // Ensure GameManager exists
            if (GameManager.Instance == null)
            {
                var go = new GameObject("[GameManager]");
                go.AddComponent<GameManager>();
            }

            // Minimum splash duration
            yield return new WaitForSeconds(_minSplashTime);

            // Transition to Hub using SceneLoader if available, else direct
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadScene("Hub");
            else
                GameManager.Instance.LoadHub();
        }
    }
}
