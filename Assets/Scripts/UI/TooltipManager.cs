// ============================================================================
// ETD.UI - TooltipManager.cs
// Singleton controlling a single shared tooltip panel. Follows mouse with
// smart edge clamping. Any UI can call Show/Hide with TooltipContent.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ETD.UI
{
    public struct TooltipContent
    {
        public string Title;
        public string Body;
        public string Stats;       // Optional: formatted stat lines
        public string Footer;      // Optional: unlock condition, synergy info, etc.
        public Color TitleColor;   // Rarity/grade color
        public bool HasTitleColor;

        public static TooltipContent Simple(string title, string body)
        {
            return new TooltipContent { Title = title, Body = body };
        }
    }

    public class TooltipManager : MonoBehaviour
    {
        public static TooltipManager Instance { get; private set; }

        [Header("Panel References")]
        [SerializeField] private GameObject _tooltipPanel;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _bodyText;
        [SerializeField] private TMP_Text _statsText;
        [SerializeField] private TMP_Text _footerText;
        [SerializeField] private Image _titleBackground;

        [Header("Positioning")]
        [SerializeField] private Vector2 _offset = new(15f, -15f);
        [SerializeField] private float _edgePadding = 10f;
        [SerializeField, Min(1f)] private float _maximumWidth = 634f;

        private RectTransform _panelRect;
        private RectTransform _canvasRect;
        private Canvas _canvas;
        private bool _isShowing;
        private readonly Vector3[] _cornerBuffer = new Vector3[4];

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _panelRect = _tooltipPanel.GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null)
                _canvasRect = _canvas.GetComponent<RectTransform>();

            Hide();
        }

        private void Update()
        {
            if (_isShowing)
                UpdatePosition();
        }

        public void Show(TooltipContent content)
        {
            // Title
            if (_titleText != null)
            {
                _titleText.text = content.Title ?? "";
                if (content.HasTitleColor)
                    _titleText.color = content.TitleColor;
                else
                    _titleText.color = Color.white;
            }

            // Body
            if (_bodyText != null)
            {
                _bodyText.text = content.Body ?? "";
                _bodyText.gameObject.SetActive(!string.IsNullOrEmpty(content.Body));
            }

            // Stats
            if (_statsText != null)
            {
                _statsText.text = content.Stats ?? "";
                _statsText.gameObject.SetActive(!string.IsNullOrEmpty(content.Stats));
            }

            // Footer
            if (_footerText != null)
            {
                _footerText.text = content.Footer ?? "";
                _footerText.gameObject.SetActive(!string.IsNullOrEmpty(content.Footer));
            }

            // Title background color
            if (_titleBackground != null && content.HasTitleColor)
            {
                Color bg = content.TitleColor;
                bg.a = 0.3f;
                _titleBackground.color = bg;
            }

            _tooltipPanel.SetActive(true);
            _isShowing = true;

            // Keep all tooltip text within a predictable readable width. Layout then grows
            // vertically as text wraps instead of allowing a long description or stat line
            // to expand the panel past the screen edge.
            ConstrainPanelWidth();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
            ConfigureTextWrapping();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
            UpdatePosition();
        }

        public void Hide()
        {
            _tooltipPanel.SetActive(false);
            _isShowing = false;
        }

        private void ConstrainPanelWidth()
        {
            if (_panelRect == null || _canvasRect == null)
                return;

            float edgePadding = Mathf.Max(0f, _edgePadding);
            float availableWidth = Mathf.Max(1f, _canvasRect.rect.width - edgePadding * 2f);
            _panelRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Min(_maximumWidth, availableWidth));
        }

        private void ConfigureTextWrapping()
        {
            VerticalLayoutGroup contentLayout = _bodyText != null
                ? _bodyText.GetComponentInParent<VerticalLayoutGroup>()
                : null;

            if (contentLayout == null)
                return;

            RectTransform contentRect = contentLayout.GetComponent<RectTransform>();
            float availableWidth = Mathf.Max(
                1f,
                contentRect.rect.width - contentLayout.padding.horizontal);

            ConfigureTextBlock(_titleText, availableWidth);
            ConfigureTextBlock(_bodyText, availableWidth);
            ConfigureTextBlock(_statsText, availableWidth);
            ConfigureTextBlock(_footerText, availableWidth);
        }

        private static void ConfigureTextBlock(TMP_Text text, float availableWidth)
        {
            if (text == null || !text.gameObject.activeInHierarchy)
                return;

            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;

            var layoutElement = text.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = text.gameObject.AddComponent<LayoutElement>();

            layoutElement.minWidth = 0f;
            layoutElement.preferredWidth = availableWidth;
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = 0f;
            layoutElement.preferredHeight = text.GetPreferredValues(
                text.text, availableWidth, 0f).y;
            layoutElement.flexibleHeight = 0f;
        }

        /// <summary>
        /// Places the panel below-right of the cursor, and <em>flips</em> it to the other
        /// side when that would run off screen — a card at the right edge of the build bar
        /// gets its tooltip on the left of the cursor instead of a squashed, clipped one.
        /// Clamping is only the last resort for a panel too large to fit either way.
        ///
        /// Works from measured world corners rather than sizeDelta/anchoredPosition math,
        /// so it is correct for any pivot, anchor preset or layout-driven size on the panel
        /// (sizeDelta is not the rendered size once anchors stretch or a fitter drives it).
        /// </summary>
        private void UpdatePosition()
        {
            if (_panelRect == null || _canvasRect == null) return;

            Vector2 mouse = GetMouseInCanvasSpace();

            _panelRect.GetWorldCorners(_cornerBuffer);
            Vector2 bottomLeft = WorldToCanvas(_cornerBuffer[0]);
            Vector2 topRight = WorldToCanvas(_cornerBuffer[2]);
            Vector2 size = topRight - bottomLeft;
            Vector2 currentTopLeft = new(bottomLeft.x, topRight.y);

            Rect bounds = _canvasRect.rect;
            // A negative inspector value would expand the bounds beyond the canvas and
            // defeat the safety clamp, so treat it as zero rather than letting a tooltip
            // leave the visible area.
            float edgePadding = Mathf.Max(0f, _edgePadding);
            float minX = bounds.xMin + edgePadding;
            float maxX = bounds.xMax - edgePadding;
            float minY = bounds.yMin + edgePadding;
            float maxY = bounds.yMax - edgePadding;

            // Horizontal: preferred side is right of the cursor; flip to the left if the
            // panel would cross the right edge, and only then clamp.
            float left = mouse.x + _offset.x;
            if (left + size.x > maxX)
                left = mouse.x - _offset.x - size.x;
            if (left < minX)
                left = Mathf.Min(minX, maxX - size.x);

            // Vertical: preferred side is below the cursor (_offset.y is negative), flipping
            // above it near the bottom edge.
            float top = mouse.y + _offset.y;
            if (top - size.y < minY)
                top = mouse.y - _offset.y + size.y;
            if (top > maxY)
                top = Mathf.Max(maxY, minY + size.y);

            // Shift by the delta between where the panel is and where it should be, which
            // needs no assumption about how anchoredPosition maps to canvas space.
            _panelRect.anchoredPosition += new Vector2(left, top) - currentTopLeft;
        }

        private Vector2 GetMouseInCanvasSpace()
        {
            Vector2 screenPos = UnityEngine.Input.mousePosition;
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenPos, cam, out Vector2 local);
            return local;
        }

        private Vector2 WorldToCanvas(Vector3 world)
            => _canvasRect.InverseTransformPoint(world);

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
