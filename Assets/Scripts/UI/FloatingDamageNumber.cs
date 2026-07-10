// ============================================================================
// ETD.UI - FloatingDamageNumber.cs
// Pooled floating combat text element. It follows a world anchor, floats upward,
// scales slightly, and fades without allocating per frame.
//
// Screen Space Overlay fix:
// - World -> screen conversion must use the game/world camera.
// - Screen -> RectTransform local conversion must use null for Overlay canvas.
// Passing null for both conversions makes world positions behave like UI pixels.
// ============================================================================
using System;
using TMPro;
using UnityEngine;

namespace ETD.UI
{
    [DisallowMultipleComponent]
    public sealed class FloatingDamageNumber : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Motion")]
        [SerializeField] private float _duration = 0.75f;
        [SerializeField] private float _risePixels = 44f;
        [SerializeField] private float _sidePixels = 12f;
        [SerializeField] private float _criticalScale = 1.2f;
        [SerializeField] private AnimationCurve _alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.EaseInOut(0f, 0.9f, 1f, 1f);

        [Header("Screen Space Safety")]
        [SerializeField] private bool _clampToRoot = true;
        [SerializeField] private float _edgePadding = 24f;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = new(1f, 0.92f, 0.62f, 1f);
        [SerializeField] private Color _criticalColor = new(1f, 0.45f, 0.28f, 1f);
        [SerializeField] private Color _burnColor = new(1f, 0.35f, 0.12f, 1f);
        [SerializeField] private Color _chainColor = new(0.45f, 0.9f, 1f, 1f);
        [SerializeField] private Color _laserColor = new(0.9f, 0.65f, 1f, 1f);
        [SerializeField] private Color _pureColor = new(1f, 1f, 1f, 1f);

        private RectTransform _root;
        private Camera _worldCamera;
        private Camera _uiCamera;
        private Vector3 _worldPosition;
        private Vector2 _jitter;
        private float _startTime;
        private float _durationRuntime;
        private float _scaleRuntime;
        private bool _active;

        private void Awake()
        {
            if (_rectTransform == null)
                _rectTransform = transform as RectTransform;
            if (_text == null)
                _text = GetComponentInChildren<TMP_Text>(true);
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        public void Play(RectTransform root, Camera worldCamera, Camera uiCamera, Vector3 worldPosition, float amount,
            bool critical, int damageKind)
        {
            _root = root;
            _worldCamera = worldCamera != null ? worldCamera : Camera.main;
            _uiCamera = uiCamera;
            _worldPosition = worldPosition;
            _startTime = Time.unscaledTime;
            _durationRuntime = Mathf.Max(0.1f, _duration) * (critical ? 1.08f : 1f);
            _scaleRuntime = critical ? _criticalScale : 1f;
            _jitter = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, _sidePixels);
            _active = true;

            if (_text != null)
            {
                // Number-only combat text. Damage type/critical state is communicated by color and scale.
                _text.text = FormatAmount(amount);
                _text.color = GetColor(critical, damageKind);
                _text.raycastTarget = false;
            }

            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;

            gameObject.SetActive(true);
            UpdateVisual(0f);
        }

        /// <summary>
        /// Called once per frame by FloatingDamageNumberManager instead of a
        /// per-instance Unity Update() (profiler: 99 separate Update calls cost
        /// ~1ms in invocation overhead alone). Returns true when the number has
        /// finished and should be recycled by the manager.
        /// </summary>
        internal bool ManagedUpdate(float now)
        {
            if (!_active)
                return true;

            float t = Mathf.Clamp01((now - _startTime) / _durationRuntime);
            UpdateVisual(t);

            if (t >= 1f)
            {
                _active = false;
                return true;
            }

            return false;
        }

        private void UpdateVisual(float t)
        {
            if (_rectTransform == null || _root == null)
                return;

            if (!TryGetRootPosition(out Vector2 localPoint))
            {
                if (_canvasGroup != null)
                    _canvasGroup.alpha = 0f;
                return;
            }

            localPoint += _jitter;
            localPoint.y += Mathf.Lerp(0f, _risePixels, EaseOutCubic(t));
            _rectTransform.anchoredPosition = localPoint;

            float scale = _scaleRuntime * Mathf.Max(0.01f, _scaleCurve.Evaluate(t));
            _rectTransform.localScale = Vector3.one * scale;

            if (_canvasGroup != null)
                _canvasGroup.alpha = Mathf.Clamp01(_alphaCurve.Evaluate(t));
        }

        private bool TryGetRootPosition(out Vector2 localPoint)
        {
            localPoint = Vector2.zero;

            Vector3 screenPoint;
            if (_worldCamera != null)
            {
                screenPoint = _worldCamera.WorldToScreenPoint(_worldPosition);
                if (screenPoint.z < 0f)
                    return false;
            }
            else
            {
                // Last-resort fallback. This should rarely happen; assign World Camera on the manager.
                screenPoint = new Vector3(_worldPosition.x, _worldPosition.y, 0f);
            }

            if (_uiCamera == null)
            {
                // Screen Space Overlay fast path (profiler: the generic helper below
                // builds a Ray and runs Plane.Raycast per number per frame — ~1ms at
                // 99 live numbers). For an overlay canvas, screen coordinates ARE the
                // canvas world coordinates, so a single InverseTransformPoint gives
                // the identical local point.
                Vector3 local = _root.InverseTransformPoint(screenPoint.x, screenPoint.y, 0f);
                localPoint = new Vector2(local.x, local.y);
            }
            else
            {
                bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _root,
                    new Vector2(screenPoint.x, screenPoint.y),
                    _uiCamera,
                    out localPoint);

                if (!converted)
                    return false;
            }

            if (_clampToRoot)
            {
                Rect rect = _root.rect;
                float minX = rect.xMin + _edgePadding;
                float maxX = rect.xMax - _edgePadding;
                float minY = rect.yMin + _edgePadding;
                float maxY = rect.yMax - _edgePadding;

                if (minX <= maxX)
                    localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
                if (minY <= maxY)
                    localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);
            }

            return true;
        }

        private Color GetColor(bool critical, int damageKind)
        {
            if (critical)
                return _criticalColor;

            switch (damageKind)
            {
                case 2: return _pureColor;
                case 3: return _burnColor;
                case 4: return _chainColor;
                case 5: return _laserColor;
                default: return _normalColor;
            }
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        private static string FormatAmount(float value)
        {
            // Canonical formatter so late-game combat text stays readable (…B/T/Qa…)
            // instead of showing five- and six-digit "M" numbers.
            return ETD.Core.NumberFormat.Compact(Mathf.Max(0f, value));
        }
    }
}
