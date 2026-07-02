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

        private RectTransform _panelRect;
        private RectTransform _canvasRect;
        private Canvas _canvas;
        private bool _isShowing;

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

            // Force layout rebuild so size is correct before positioning
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
            UpdatePosition();
        }

        public void Hide()
        {
            _tooltipPanel.SetActive(false);
            _isShowing = false;
        }

        private void UpdatePosition()
        {
            Vector2 mousePos = UnityEngine.Input.mousePosition;

            // Convert to canvas space
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, mousePos, _canvas.worldCamera, out mousePos);
            }

            Vector2 pos = mousePos + _offset;

            // Clamp to screen edges
            if (_panelRect != null && _canvasRect != null)
            {
                Vector2 panelSize = _panelRect.sizeDelta;
                Vector2 canvasSize = _canvasRect.sizeDelta;
                float halfW = canvasSize.x * 0.5f;
                float halfH = canvasSize.y * 0.5f;

                // Right edge
                if (pos.x + panelSize.x > halfW - _edgePadding)
                    pos.x = mousePos.x - panelSize.x - _offset.x;

                // Bottom edge
                if (pos.y - panelSize.y < halfH + _edgePadding)
                    pos.y = mousePos.y + panelSize.y + Mathf.Abs(_offset.y);

                // Left edge
                if (pos.x < -halfW + _edgePadding)
                    pos.x = -halfW + _edgePadding;

                // Top edge
                if (pos.y > canvasSize.y - _edgePadding)
                    pos.y = canvasSize.y - _edgePadding;
            }

            _panelRect.anchoredPosition = pos;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
