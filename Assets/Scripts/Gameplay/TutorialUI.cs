// ============================================================================
// ETD.UI - TutorialUI.cs  [NEW]
// Displays tutorial overlay: dim panel, highlight frame, text bubble, pointer.
// Attach to TutorialUI_GO in Game scene Canvas.
// ============================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Gameplay;

namespace ETD.Gameplay
{
    public class TutorialUI : MonoBehaviour
    {
        [Header("Overlay")]
        [SerializeField] private GameObject _root;
        [SerializeField] private Image      _dimPanel;

        [Header("Text Bubble")]
        [SerializeField] private RectTransform _bubble;
        [SerializeField] private TMP_Text      _titleText;
        [SerializeField] private TMP_Text      _bodyText;
        [SerializeField] private Button        _nextButton;
        [SerializeField] private TMP_Text      _nextButtonText;
        [SerializeField] private Button        _skipButton;

        [Header("Highlight Frame")]
        [SerializeField] private RectTransform _highlightFrame;
        [SerializeField] private float         _highlightPadding = 12f;

        [Header("Pointer Arrow")]
        [SerializeField] private RectTransform _pointer;

        [Header("Animation")]
        [SerializeField] private float _fadeInDuration = 0.25f;
        [SerializeField] private float _bubblePunchScale = 1.08f;

        // Target references — wire in Inspector to UI elements
        [Header("Tutorial Targets (wire to your UI elements)")]
        public RectTransform TurretBarTarget;
        public RectTransform TurretInfoTarget;
        public RectTransform SpeedButtonTarget;
        public RectTransform SkipButtonTarget;
        public RectTransform SpecCardTarget;
        public RectTransform EvolveTarget;

        private System.Action _onNext;
        private Coroutine     _animCoroutine;

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);
            _nextButton?.onClick.AddListener(OnNextClicked);
            _skipButton?.onClick.AddListener(OnSkipClicked);
        }

        // =================================================================
        // SHOW
        // =================================================================

        public void Show(string title, string body, RectTransform target,
            bool canSkip, System.Action onNext)
        {
            _onNext = onNext;

            if (_titleText != null) _titleText.text = title;
            if (_bodyText  != null) _bodyText.text  = body;
            if (_skipButton != null) _skipButton.gameObject.SetActive(canSkip);

            // Next button label
            if (_nextButtonText != null)
                _nextButtonText.text = onNext != null ? "Got it →" : "";
            if (_nextButton != null)
                _nextButton.gameObject.SetActive(onNext != null);

            // Show/position highlight frame
            if (_highlightFrame != null)
            {
                bool hasTarget = target != null;
                _highlightFrame.gameObject.SetActive(hasTarget);
                if (hasTarget)
                    PositionHighlight(target);
            }

            // Show/position pointer
            if (_pointer != null)
            {
                bool hasTarget = target != null;
                _pointer.gameObject.SetActive(hasTarget);
                if (hasTarget)
                    PositionPointer(target);
            }

            if (_root != null) _root.SetActive(true);

            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(ShowAnimation());
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            if (_highlightFrame != null) _highlightFrame.gameObject.SetActive(false);
            if (_pointer != null) _pointer.gameObject.SetActive(false);
        }

        // =================================================================
        // BUTTONS
        // =================================================================

        private void OnNextClicked()
        {
            var cb = _onNext;
            _onNext = null;
            cb?.Invoke();
        }

        private void OnSkipClicked()
        {
            TutorialManager.Instance?.SkipTutorial();
        }

        // =================================================================
        // POSITION HELPERS
        // =================================================================

        private void PositionHighlight(RectTransform target)
        {
            if (_highlightFrame == null || target == null) return;

            // Copy target's world corners to highlight frame
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);

            // Convert to local space of highlight frame's parent
            var canvas = _highlightFrame.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, corner);
                if (screen.x < minX) minX = screen.x;
                if (screen.y < minY) minY = screen.y;
                if (screen.x > maxX) maxX = screen.x;
                if (screen.y > maxY) maxY = screen.y;
            }

            float p = _highlightPadding;
            _highlightFrame.position = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, 0f);
            _highlightFrame.sizeDelta = new Vector2(maxX - minX + p * 2f, maxY - minY + p * 2f);
        }

        private void PositionPointer(RectTransform target)
        {
            if (_pointer == null || target == null) return;

            // Position pointer between bubble and target
            Vector3 targetCenter = target.TransformPoint(target.rect.center);
            Vector3 bubbleCenter = _bubble != null
                ? _bubble.TransformPoint(_bubble.rect.center)
                : Vector3.zero;

            Vector3 dir = (targetCenter - bubbleCenter).normalized;
            _pointer.position  = bubbleCenter + dir * 80f;
            float angle        = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _pointer.rotation  = Quaternion.Euler(0f, 0f, angle);
        }

        // =================================================================
        // ANIMATION
        // =================================================================

        private IEnumerator ShowAnimation()
        {
            if (_bubble == null) yield break;

            // Punch scale
            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _fadeInDuration;
                float scale = Mathf.Lerp(_bubblePunchScale, 1f, t);
                _bubble.localScale = Vector3.one * scale;
                yield return null;
            }
            _bubble.localScale = Vector3.one;
        }
    }
}
