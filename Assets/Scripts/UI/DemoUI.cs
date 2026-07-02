using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ETD.Core;

namespace ETD.UI
{
    /// <summary>
    /// Controller for the user's hand-built Demo Complete panel.
    /// This script does not create the overlay layout at runtime. Assign your existing panel,
    /// localized text fields, Wishlist button, and Return Menu button in the Inspector.
    ///
    /// Demo completion must appear above the normal Game Over UI, so this controller also
    /// hides GameOverUI and promotes the assigned panel to a high sorting order when shown.
    /// </summary>
    public sealed class DemoUI : MonoBehaviour
    {
        [Header("Existing Demo Complete Panel")]
        [Tooltip("Your existing DemoUI / Demo Complete panel. If empty, this GameObject is used as the panel.")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _bodyText;
        [SerializeField] private TMP_Text _reachedWaveText;

        [Header("Buttons")]
        [SerializeField] private Button _wishlistButton;
        [SerializeField] private TMP_Text _wishlistButtonText;
        [SerializeField] private Button _returnMenuButton;
        [SerializeField] private TMP_Text _returnMenuButtonText;

        [Header("Steam")]
        [Tooltip("Optional. If empty, DemoMode.FullGameStoreUrl is used. You can paste your full game's Steam store URL here.")]
        [SerializeField] private string _steamStoreUrlOverride = "";

        [Header("Behavior")]
        [Tooltip("Hide the assigned panel in Awake so the panel can start active in the scene and still initialize correctly.")]
        [SerializeField] private bool _hideOnAwake = true;
        [SerializeField] private bool _pauseGameWhileVisible = false;

        [Header("Priority")]
        [Tooltip("Hide the normal Game Over UI when the demo-complete panel is shown.")]
        [SerializeField] private bool _hideGameOverUiOnShow = true;
        [Tooltip("Move the assigned panel to the end of its parent hierarchy when shown.")]
        [SerializeField] private bool _setAsLastSiblingOnShow = true;
        [Tooltip("Add/use a Canvas on the assigned panel and give it a high sorting order so it appears over other UI canvases.")]
        [SerializeField] private bool _forceTopSortingCanvas = true;
        [SerializeField] private int _sortingOrderWhenVisible = 10000;

        private bool _isVisible;
        private Canvas _priorityCanvas;
        private GraphicRaycaster _priorityRaycaster;
        private bool _addedPriorityCanvas;
        private bool _addedPriorityRaycaster;
        private bool _previousOverrideSorting;
        private int _previousSortingOrder;

        private void Awake()
        {
            if (_panel == null)
                _panel = gameObject;

            WireButtons();
            RefreshText(DemoMode.CompletedAtWave > 0 ? DemoMode.CompletedAtWave : DemoMode.MaxPlayableWave);

            if (_hideOnAwake)
                Hide();

            EventBus.Subscribe<DemoCompletedEvent>(OnDemoCompleted);
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);

            if (DemoMode.IsDemo && DemoMode.DemoCompletedThisSession)
                Show(DemoMode.CompletedAtWave);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<DemoCompletedEvent>(OnDemoCompleted);
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);

            if (_wishlistButton != null)
                _wishlistButton.onClick.RemoveListener(OnWishlistClicked);

            if (_returnMenuButton != null)
                _returnMenuButton.onClick.RemoveListener(OnReturnMenuClicked);
        }

        private void OnDemoCompleted(DemoCompletedEvent evt)
        {
            Show(evt.ReachedWave);
        }

        private void OnLanguageChanged(LanguageChangedEvent _)
        {
            if (_isVisible)
                RefreshText(DemoMode.CompletedAtWave > 0 ? DemoMode.CompletedAtWave : DemoMode.MaxPlayableWave);
        }

        private void WireButtons()
        {
            if (_wishlistButton != null)
            {
                _wishlistButton.onClick.RemoveListener(OnWishlistClicked);
                _wishlistButton.onClick.AddListener(OnWishlistClicked);
            }

            if (_returnMenuButton != null)
            {
                _returnMenuButton.onClick.RemoveListener(OnReturnMenuClicked);
                _returnMenuButton.onClick.AddListener(OnReturnMenuClicked);
            }
        }

        public void Show(int reachedWave)
        {
            _isVisible = true;
            RefreshText(reachedWave);

            if (_hideGameOverUiOnShow)
                HideGameOverPanels();

            if (_panel != null)
            {
                _panel.SetActive(true);
                BringPanelToFront();
            }

            if (_pauseGameWhileVisible)
                Time.timeScale = 0f;
        }

        public void Hide()
        {
            _isVisible = false;

            RestorePriorityCanvas();

            if (_panel != null)
                _panel.SetActive(false);
        }

        private void BringPanelToFront()
        {
            if (_panel == null)
                return;

            if (_setAsLastSiblingOnShow)
                _panel.transform.SetAsLastSibling();

            if (!_forceTopSortingCanvas)
                return;

            _priorityCanvas = _panel.GetComponent<Canvas>();
            if (_priorityCanvas == null)
            {
                _priorityCanvas = _panel.AddComponent<Canvas>();
                _addedPriorityCanvas = true;
            }

            _priorityRaycaster = _panel.GetComponent<GraphicRaycaster>();
            if (_priorityRaycaster == null)
            {
                _priorityRaycaster = _panel.AddComponent<GraphicRaycaster>();
                _addedPriorityRaycaster = true;
            }

            _previousOverrideSorting = _priorityCanvas.overrideSorting;
            _previousSortingOrder = _priorityCanvas.sortingOrder;

            _priorityCanvas.overrideSorting = true;
            _priorityCanvas.sortingOrder = _sortingOrderWhenVisible;
        }

        private void RestorePriorityCanvas()
        {
            if (_priorityCanvas != null)
            {
                _priorityCanvas.overrideSorting = _previousOverrideSorting;
                _priorityCanvas.sortingOrder = _previousSortingOrder;
            }

            if (_addedPriorityRaycaster && _priorityRaycaster != null)
                Destroy(_priorityRaycaster);

            if (_addedPriorityCanvas && _priorityCanvas != null)
                Destroy(_priorityCanvas);

            _priorityCanvas = null;
            _priorityRaycaster = null;
            _addedPriorityCanvas = false;
            _addedPriorityRaycaster = false;
        }

        private static void HideGameOverPanels()
        {
#if UNITY_2023_1_OR_NEWER
            var gameOverUis = Object.FindObjectsByType<GameOverUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var gameOverUis = Object.FindObjectsOfType<GameOverUI>(true);
#endif
            for (int i = 0; i < gameOverUis.Length; i++)
                gameOverUis[i]?.HideForDemoComplete();
        }

        private void RefreshText(int reachedWave)
        {
            if (_titleText != null)
                _titleText.text = LocalizationManager.Get(DemoMode.DemoCompletedTitleKey, DemoMode.DemoCompletedTitleFallback);

            if (_bodyText != null)
                _bodyText.text = LocalizationManager.Get(DemoMode.DemoCompletedBodyKey, DemoMode.DemoCompletedBodyFallback);

            if (_reachedWaveText != null)
            {
                _reachedWaveText.text = LocalizationManager.GetFormat(
                    DemoMode.DemoCompletedReachedWaveKey,
                    DemoMode.DemoCompletedReachedWaveFallback,
                    reachedWave);
            }

            if (_wishlistButtonText != null)
                _wishlistButtonText.text = LocalizationManager.Get(DemoMode.WishlistButtonKey, DemoMode.WishlistButtonFallback);

            if (_returnMenuButtonText != null)
                _returnMenuButtonText.text = LocalizationManager.Get(DemoMode.ReturnMenuButtonKey, DemoMode.ReturnMenuButtonFallback);
        }

        private void OnWishlistClicked()
        {
            string url = string.IsNullOrWhiteSpace(_steamStoreUrlOverride)
                ? DemoMode.FullGameStoreUrl
                : _steamStoreUrlOverride.Trim();

            if (!string.IsNullOrWhiteSpace(url))
                Application.OpenURL(url);
        }

        private void OnReturnMenuClicked()
        {
            Time.timeScale = 1f;
            Hide();

            if (GameManager.Instance != null)
                GameManager.Instance.LoadHub();
        }
    }
}
