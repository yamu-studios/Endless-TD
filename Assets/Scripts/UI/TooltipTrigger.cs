// ============================================================================
// ETD.UI - TooltipTrigger.cs
// Attach to any UI element. On hover, shows tooltip via TooltipManager.
// Supports three modes:
//   1. Static: set title/body in inspector
//   2. Dynamic: call SetContent() from code
//   3. Data-driven: assign a ScriptableObject and it auto-generates content
// ============================================================================
using UnityEngine;
using UnityEngine.EventSystems;
using ETD.Data;

namespace ETD.UI
{
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Static Content (optional)")]
        [SerializeField] private string _title;
        [SerializeField][TextArea] private string _body;

        [Header("Data-Driven (optional — overrides static)")]
        [SerializeField] private ScriptableObject _dataSource;

        [Header("Delay")]
        [SerializeField] private float _showDelay = 0.3f;

        private TooltipContent? _dynamicContent;
        private float _hoverTimer;
        private bool _isHovering;
        private bool _isShowing;

        /// <summary>
        /// Set tooltip content from code. Overrides inspector and data source.
        /// </summary>
        /// 
        public void SetData(ScriptableObject _data)
        {
            _dataSource = _data;
        }
        public void SetContent(TooltipContent content)
        {
            _dynamicContent = content;
        }

        /// <summary>
        /// Re-pushes the current content if this tooltip is on screen right now. Needed
        /// for content that changes while hovered (a ticking cooldown): ShowTooltip only
        /// runs once when the hover delay elapses, so without this the panel would keep
        /// displaying whatever the values were at that instant. No-op when not showing,
        /// so callers can call it unconditionally.
        /// </summary>
        public void RefreshIfShowing()
        {
            if (_isShowing)
                ShowTooltip();
        }

        /// <summary>
        /// Clear dynamic content, falls back to inspector/data source.
        /// </summary>
        public void ClearContent()
        {
            _dynamicContent = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
            _hoverTimer = 0f;
            _isShowing = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            _isShowing = false;
            TooltipManager.Instance?.Hide();
        }

        private void Update()
        {
            if (!_isHovering || _isShowing) return;

            _hoverTimer += Time.unscaledDeltaTime;
            if (_hoverTimer >= _showDelay)
            {
                ShowTooltip();
                _isShowing = true;
            }
        }

        private void ShowTooltip()
        {
            if (TooltipManager.Instance == null) return;

            TooltipContent content;

            // Priority: dynamic > data source > static
            if (_dynamicContent.HasValue)
            {
                content = _dynamicContent.Value;
            }
            else if (_dataSource != null)
            {
                content = TooltipContentBuilder.FromScriptableObject(_dataSource);
            }
            else
            {
                content = TooltipContent.Simple(_title, _body);
            }

            TooltipManager.Instance.Show(content);
        }

        private void OnDisable()
        {
            if (_isShowing)
                TooltipManager.Instance?.Hide();
            _isHovering = false;
            _isShowing = false;
        }
    }
}
