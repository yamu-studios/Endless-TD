// ============================================================================
// ETD.UI - DraggableUIPanel.cs
// Add to HUD panels that players should reposition during a run.
// Stores anchored position per panel id in PlayerPrefs.
// ============================================================================
using UnityEngine;
using UnityEngine.EventSystems;

namespace ETD.UI
{
    [DisallowMultipleComponent]
    public sealed class DraggableUIPanel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private string _panelId = "panel";
        [SerializeField] private RectTransform _dragHandle;
        [SerializeField] private bool _savePosition = true;
        [SerializeField] private bool _clampToParent = true;
        [SerializeField] private float _edgePadding = 8f;

        private RectTransform _rect;
        private RectTransform _parent;
        private Canvas _canvas;
        private Vector2 _dragStartPointerLocal;
        private Vector2 _dragStartAnchoredPosition;
        private bool _dragging;

        private string PrefKeyX => "ETD_UI_" + _panelId + "_x";
        private string PrefKeyY => "ETD_UI_" + _panelId + "_y";

        private void Awake()
        {
            _rect = transform as RectTransform;
            _parent = _rect != null ? _rect.parent as RectTransform : null;
            _canvas = GetComponentInParent<Canvas>();

            if (_dragHandle == null)
                _dragHandle = _rect;

            if (string.IsNullOrWhiteSpace(_panelId))
                _panelId = gameObject.name;

            LoadPosition();
        }

        private void OnEnable()
        {
            // Layout/anchors can change while the panel is disabled. Re-clamp when it opens
            // so middle-right, top-right, bottom-left, etc. anchored panels cannot restore outside screen.
            ClampCurrentPosition();
        }

        public void Configure(string panelId, RectTransform dragHandle = null)
        {
            if (!string.IsNullOrWhiteSpace(panelId))
                _panelId = panelId;
            if (dragHandle != null)
                _dragHandle = dragHandle;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = false;
            if (_rect == null || _parent == null)
                return;

            if (_dragHandle != null && eventData.pointerPressRaycast.gameObject != null)
            {
                Transform pressed = eventData.pointerPressRaycast.gameObject.transform;
                if (pressed != _dragHandle && !pressed.IsChildOf(_dragHandle))
                    return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parent,
                    eventData.position,
                    eventData.pressEventCamera,
                    out _dragStartPointerLocal))
                return;

            _dragStartAnchoredPosition = _rect.anchoredPosition;
            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _rect == null || _parent == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parent,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 pointerLocal))
                return;

            Vector2 delta = pointerLocal - _dragStartPointerLocal;
            Vector2 next = _dragStartAnchoredPosition + delta;
            _rect.anchoredPosition = _clampToParent ? ClampToParent(next) : next;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging)
                return;

            _dragging = false;
            SavePosition();
        }

        public void ResetSavedPosition()
        {
            PlayerPrefs.DeleteKey(PrefKeyX);
            PlayerPrefs.DeleteKey(PrefKeyY);
            PlayerPrefs.Save();
        }

        private Vector2 ClampToParent(Vector2 anchored)
        {
            if (_rect == null || _parent == null)
                return anchored;

            Canvas.ForceUpdateCanvases();

            Vector2 oldAnchored = _rect.anchoredPosition;
            _rect.anchoredPosition = anchored;

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_parent, _rect);
            Rect parentRect = _parent.rect;

            float minX = parentRect.xMin + _edgePadding;
            float maxX = parentRect.xMax - _edgePadding;
            float minY = parentRect.yMin + _edgePadding;
            float maxY = parentRect.yMax - _edgePadding;

            Vector2 correction = Vector2.zero;

            if (bounds.min.x < minX)
                correction.x += minX - bounds.min.x;
            if (bounds.max.x > maxX)
                correction.x -= bounds.max.x - maxX;

            if (bounds.min.y < minY)
                correction.y += minY - bounds.min.y;
            if (bounds.max.y > maxY)
                correction.y -= bounds.max.y - maxY;

            _rect.anchoredPosition = oldAnchored;
            return anchored + correction;
        }

        private void ClampCurrentPosition()
        {
            if (!_clampToParent || _rect == null || _parent == null)
                return;

            _rect.anchoredPosition = ClampToParent(_rect.anchoredPosition);
        }

        private void LoadPosition()
        {
            if (!_savePosition || _rect == null || !PlayerPrefs.HasKey(PrefKeyX) || !PlayerPrefs.HasKey(PrefKeyY))
                return;

            Vector2 saved = new Vector2(PlayerPrefs.GetFloat(PrefKeyX), PlayerPrefs.GetFloat(PrefKeyY));
            _rect.anchoredPosition = _clampToParent ? ClampToParent(saved) : saved;
        }

        private void SavePosition()
        {
            if (!_savePosition || _rect == null)
                return;

            PlayerPrefs.SetFloat(PrefKeyX, _rect.anchoredPosition.x);
            PlayerPrefs.SetFloat(PrefKeyY, _rect.anchoredPosition.y);
            PlayerPrefs.Save();
        }
    }
}
