// ============================================================================
// ETD.Core - SceneLoader.cs  [NEW]
// Async scene loading with a loading screen. DontDestroyOnLoad.
// Shows a canvas with progress bar during transitions.
// ============================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace ETD.Core
{
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("Loading Screen UI")]
        [SerializeField] private GameObject _loadingCanvas;
        [SerializeField] private Image _progressFill;
        [SerializeField] private TMP_Text _loadingText;
        [SerializeField] private TMP_Text _tipText;
        [SerializeField] private Image _loadingImage;
        [SerializeField] private Sprite[] _loadingImages;

        [Header("Settings")]
        [SerializeField] private float _minimumLoadTime = 0.5f;

        [Header("Tips")]
        [SerializeField] private string[] _tipKeys = new[]
        {
            "loading_tip_spec_card_shortcuts",
            "loading_tip_reroll_shortcut",
            "loading_tip_tab_stats",
            "loading_tip_escape_pause",
            "loading_tip_escape_hub",
            "loading_tip_upgrade_shortcut",
            "loading_tip_sell_shortcut",
            "loading_tip_evolve_shortcut",
            "loading_tip_shift_placement",
            "loading_tip_skip_prep",
            "loading_tip_support_range",
            "loading_tip_support_upgrade",
            "loading_tip_traits",
            "loading_tip_spec_cards",
            "loading_tip_evolution",
            "loading_tip_burn",
            "loading_tip_frost",
            "loading_tip_lightning",
            "loading_tip_laser",
            "loading_tip_boss_damage",
            "loading_tip_swarm_control",
            "loading_tip_economy"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_loadingCanvas != null)
                _loadingCanvas.SetActive(false);
        }

        public void LoadScene(string sceneName)
        {
            StartCoroutine(LoadSceneAsync(sceneName, false, 1f));
        }

        public void LoadScene(string sceneName, bool freezeDuringLoad, float timeScaleAfterLoad = 1f)
        {
            StartCoroutine(LoadSceneAsync(sceneName, freezeDuringLoad, timeScaleAfterLoad));
        }

        public void LoadScene(int sceneIndex)
        {
            StartCoroutine(LoadSceneAsync(SceneManager.GetSceneByBuildIndex(sceneIndex).name, false, 1f));
        }

        public void LoadScene(int sceneIndex, bool freezeDuringLoad, float timeScaleAfterLoad = 1f)
        {
            StartCoroutine(LoadSceneAsync(SceneManager.GetSceneByBuildIndex(sceneIndex).name, freezeDuringLoad, timeScaleAfterLoad));
        }

        private IEnumerator LoadSceneAsync(string sceneName, bool freezeDuringLoad, float timeScaleAfterLoad)
        {
            if (freezeDuringLoad)
                Time.timeScale = 0f;

            // Show loading screen
            if (_loadingCanvas != null)
                _loadingCanvas.SetActive(true);

            // Random localized tip
            if (_tipText != null && _tipKeys.Length > 0)
            {
                string tipKey = _tipKeys[Random.Range(0, _tipKeys.Length)];
                string tipLabel = LocalizationManager.Get("loading_tip_label", "TIP:");
                string tipText = LocalizationManager.Get(tipKey, "");
                _tipText.text = $"<color=#FFC857>{tipLabel}</color> {tipText}";
            }

            if (_loadingImage != null && _loadingImages.Length > 0)
                _loadingImage.sprite = _loadingImages[Random.Range(0, _loadingImages.Length)];

            if (_loadingText != null)
                _loadingText.text = LocalizationManager.Get("loading_text", "Loading...");

            if (_progressFill != null)
                _progressFill.fillAmount = 0f;

            // Wait a frame so canvas renders
            yield return null;

            float startTime = Time.unscaledTime;

            // Start async load
            var operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false;

            while (!operation.isDone)
            {
                // Progress goes 0 to 0.9 while loading, then jumps to 1 on activation
                float progress = Mathf.Clamp01(operation.progress / 0.9f);

                if (_progressFill != null)
                    _progressFill.fillAmount = progress;

                if (_loadingText != null)
                {
                    string loadingText = LocalizationManager.Get("loading_text", "Loading...");
                    _loadingText.text = $"{loadingText} {Mathf.RoundToInt(progress * 100)}%";
                }

                // Ready to activate
                if (operation.progress >= 0.9f)
                {
                    // Ensure minimum load time (so player sees the screen)
                    float elapsed = Time.unscaledTime - startTime;
                    if (elapsed < _minimumLoadTime)
                    {
                        yield return new WaitForSecondsRealtime(_minimumLoadTime - elapsed);
                    }

                    if (_progressFill != null)
                        _progressFill.fillAmount = 1f;

                    // Activate scene
                    operation.allowSceneActivation = true;
                }

                yield return null;
            }

            // Hide loading screen after a frame (so new scene renders first)
            yield return null;

            if (freezeDuringLoad)
                Time.timeScale = timeScaleAfterLoad;

            if (_loadingCanvas != null)
                _loadingCanvas.SetActive(false);
        }
    }
}
