// ============================================================================
// ETD.Enemies - EnemyHealthBar.cs  [AAA HOTFIX]
// Robust pooled enemy health bar.
// Fixes high-wave debug testing where HP bars stay hidden because damage is a
// tiny fraction of huge scaled HP (ratio remains above the old 0.999 threshold).
// Also fixes unsafe canvas initialization order and refreshes Camera.main.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using ETD.Data;

namespace ETD.Enemies
{
    public class EnemyHealthBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Image _fillImage;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _delayedFillImage;

        [Header("Settings")]
        [SerializeField] private Vector3 _offset = new Vector3(0, .5f, 0);
        [SerializeField] private float _barWidth = 1f;
        [SerializeField] private float _barHeight = 0.12f;
        [SerializeField] private bool _hideWhenFull = true;
        [SerializeField] private float _delayedFillSpeed = 2f;

        [Header("Visibility")]
        [Tooltip("When true, the bar becomes visible briefly after ANY health loss, even if HP ratio is still almost full. Important for very high waves.")]
        [SerializeField] private bool _showOnAnyDamage = true;

        [Tooltip("Seconds to keep the health bar visible after damage when Hide When Full is enabled.")]
        [SerializeField] private float _showAfterDamageSeconds = 1.25f;

        [Tooltip("Normal damaged-state threshold. If HP ratio is below this, the bar remains visible.")]
        [Range(0.95f, 1f)]
        [SerializeField] private float _damagedVisibleThreshold = 0.99995f;

        [Tooltip("Minimum absolute HP change that counts as damage for showing the bar.")]
        [SerializeField] private float _damageDetectEpsilon = 0.0001f;

        [Header("Tier Indicators")]
        [Tooltip("Elite and boss health bars stay visible from spawn, even at full HP.")]
        [SerializeField] private bool _showEliteBossAtFullHealth = true;
        [SerializeField] private Image _tierIconImage;
        [SerializeField] private Sprite _eliteIcon;
        [SerializeField] private Sprite _bossIcon;
        [SerializeField] private Vector2 _tierIconSize = new Vector2(0.18f, 0.18f);
        [SerializeField] private Vector2 _tierIconOffset = new Vector2(0.08f, 0f);

        [Header("Sizing")]
        [SerializeField] private bool _scaleWidthByBaseHealth = true;
        [SerializeField] private float _healthForDefaultWidth = 55f;
        [SerializeField] private float _maxWidthMultiplier = 2.5f;

        [Header("Colors")]
        [SerializeField] private Color _fullColor = Color.green;
        [SerializeField] private Color _midColor = Color.yellow;
        [SerializeField] private Color _lowColor = Color.red;
        [SerializeField] private float _lowThreshold = 0.3f;
        [SerializeField] private float _midThreshold = 0.6f;

        private EnemyController _enemy;
        private Transform _cameraTransform;
        private RectTransform _canvasRect;
        private float _baseBarWidth;
        private float _delayedFillAmount = 1f;
        private float _visibleTimer;
        private float _lastObservedHealth = float.NaN;
        private bool _initialized;

        private void Awake()
        {
            _baseBarWidth = Mathf.Max(0.01f, _barWidth);
            ResolveEnemy();
            ResolveCanvas();
            ApplySize();
        }

        private void Start()
        {
            ResolveCamera();
        }

        private void OnEnable()
        {
            ResolveEnemy();
            ResolveCanvas();
            ApplySize();

            _delayedFillAmount = 1f;
            _visibleTimer = 0f;
            _lastObservedHealth = _enemy != null ? _enemy.CurrentHealth : float.NaN;

            if (_fillImage != null)
                _fillImage.fillAmount = 1f;

            if (_delayedFillImage != null)
                _delayedFillImage.fillAmount = 1f;

            RefreshTierIcon();

            if (_canvas != null)
                _canvas.enabled = !_hideWhenFull || IsPriorityTier();

            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized)
                return;

            ResolveEnemy();
            if (_enemy == null)
                return;

            ResolveCanvas();
            if (_canvas == null)
                return;

            if (_cameraTransform == null)
                ResolveCamera();

            float maxHealth = SanitizeHealth(_enemy.MaxHealth, 0f, 1e30f);

            // Pooled enemies are enabled before Initialize() finishes. Wait until health is valid.
            if (maxHealth <= 0f)
            {
                _canvas.enabled = false;
                return;
            }

            float currentHealth = SanitizeHealth(_enemy.CurrentHealth, 0f, maxHealth);
            float ratio = SafeHealthRatio(currentHealth, maxHealth);

            if (float.IsNaN(_lastObservedHealth))
            {
                _lastObservedHealth = currentHealth;
            }
            else if (_showOnAnyDamage && currentHealth < _lastObservedHealth - _damageDetectEpsilon)
            {
                _visibleTimer = Mathf.Max(_visibleTimer, _showAfterDamageSeconds);
            }

            _lastObservedHealth = currentHealth;

            if (_visibleTimer > 0f)
                _visibleTimer -= Time.deltaTime;

            if (_fillImage != null)
            {
                _fillImage.fillAmount = ratio;
                _fillImage.color = GetHealthColor(ratio);
            }

            if (_delayedFillImage != null)
            {
                _delayedFillAmount = Mathf.MoveTowards(
                    _delayedFillAmount,
                    ratio,
                    _delayedFillSpeed * Time.deltaTime);
                _delayedFillImage.fillAmount = _delayedFillAmount;
            }

            RefreshTierIcon();

            if (_hideWhenFull)
            {
                bool visiblyDamaged = ratio < _damagedVisibleThreshold;
                bool recentlyDamaged = _visibleTimer > 0f;
                bool priorityTier = IsPriorityTier();
                _canvas.enabled = priorityTier || visiblyDamaged || recentlyDamaged;
            }
            else
            {
                _canvas.enabled = true;
            }

            _canvas.transform.localPosition = SanitizeVector3(_offset, Vector3.zero);

            if (_cameraTransform != null && IsFinite(_cameraTransform.forward))
                _canvas.transform.forward = _cameraTransform.forward;
        }

        private bool IsPriorityTier()
        {
            return _showEliteBossAtFullHealth && _enemy != null &&
                (_enemy.Tier == EnemyTier.Elite || _enemy.Tier == EnemyTier.Boss);
        }

        private void RefreshTierIcon()
        {
            if (_tierIconImage == null)
                return;

            Sprite sprite = null;
            if (_enemy != null)
            {
                if (_enemy.Tier == EnemyTier.Boss)
                    sprite = _bossIcon;
                else if (_enemy.Tier == EnemyTier.Elite)
                    sprite = _eliteIcon;
            }

            _tierIconImage.sprite = sprite;
            _tierIconImage.enabled = sprite != null;
        }

        private void ResolveEnemy()
        {
            if (_enemy == null)
                _enemy = GetComponentInParent<EnemyController>();
        }

        private void ResolveCamera()
        {
            _cameraTransform = Camera.main != null ? Camera.main.transform : null;
        }

        private void ResolveCanvas()
        {
            if (_canvas == null)
                _canvas = GetComponentInChildren<Canvas>(true);

            if (_canvas == null)
                SetupCanvas();

            if (_canvas != null && _canvasRect == null)
                _canvasRect = _canvas.GetComponent<RectTransform>();

            if (_canvas != null && _tierIconImage == null)
                CreateTierIcon(_canvas.transform);
        }

        private void ApplySize()
        {
            if (_canvasRect == null)
                return;

            float widthMultiplier = 1f;

            if (_scaleWidthByBaseHealth && _enemy != null && _enemy.Data != null && _healthForDefaultWidth > 0f)
            {
                widthMultiplier = Mathf.Clamp(
                    _enemy.Data.MaxHealth / _healthForDefaultWidth,
                    1f,
                    Mathf.Max(1f, _maxWidthMultiplier));
            }

            float safeWidth = SanitizeHealth(_baseBarWidth * widthMultiplier, _baseBarWidth, _baseBarWidth * Mathf.Max(1f, _maxWidthMultiplier));
            float safeHeight = SanitizeHealth(_barHeight, 0.01f, 10f);
            _canvasRect.sizeDelta = new Vector2(safeWidth, safeHeight);
        }

        private static float SafeHealthRatio(float current, float max)
        {
            if (!IsFinite(max) || max <= 0f)
                return 0f;

            if (!IsFinite(current))
                return current > 0f ? 1f : 0f;

            return Safe01(current / max);
        }

        private static float Safe01(float value)
        {
            if (float.IsNaN(value))
                return 0f;

            if (float.IsPositiveInfinity(value))
                return 1f;

            if (float.IsNegativeInfinity(value))
                return 0f;

            return Mathf.Clamp01(value);
        }

        private static float SanitizeHealth(float value, float min, float max)
        {
            if (float.IsNaN(value))
                return min;

            if (float.IsPositiveInfinity(value))
                return max;

            if (float.IsNegativeInfinity(value))
                return min;

            return Mathf.Clamp(value, min, Mathf.Max(min, max));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Vector3 SanitizeVector3(Vector3 value, Vector3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private Color GetHealthColor(float ratio)
        {
            ratio = Safe01(ratio);

            if (ratio <= _lowThreshold)
                return _lowColor;

            if (ratio <= _midThreshold)
            {
                float t = Mathf.InverseLerp(_lowThreshold, _midThreshold, ratio);
                return Color.Lerp(_lowColor, _midColor, t);
            }

            float highT = Mathf.InverseLerp(_midThreshold, 1f, ratio);
            return Color.Lerp(_midColor, _fullColor, highT);
        }

        private void CreateTierIcon(Transform parent)
        {
            if (parent == null || _tierIconImage != null)
                return;

            var iconGO = new GameObject("TierIcon");
            iconGO.transform.SetParent(parent, false);
            _tierIconImage = iconGO.AddComponent<Image>();
            _tierIconImage.raycastTarget = false;
            RectTransform iconRect = _tierIconImage.rectTransform;
            iconRect.anchorMin = new Vector2(1f, 0.5f);
            iconRect.anchorMax = new Vector2(1f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = _tierIconSize;
            iconRect.anchoredPosition = _tierIconOffset;
            _tierIconImage.enabled = false;
        }

        /// <summary>
        /// Auto-creates the world-space canvas and UI elements when the prefab does not have one.
        /// </summary>
        private void SetupCanvas()
        {
            var canvasGO = new GameObject("HealthBar_Canvas");
            canvasGO.transform.SetParent(transform, false);
            canvasGO.transform.localPosition = SanitizeVector3(_offset, Vector3.zero);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100;

            _canvasRect = canvasGO.GetComponent<RectTransform>();
            _canvasRect.localScale = Vector3.one * 0.01f;

            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(canvasGO.transform, false);
            _backgroundImage = bgGO.AddComponent<Image>();
            _backgroundImage.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            Stretch(_backgroundImage.rectTransform);

            var delayGO = new GameObject("DelayedFill");
            delayGO.transform.SetParent(canvasGO.transform, false);
            _delayedFillImage = delayGO.AddComponent<Image>();
            _delayedFillImage.color = new Color(0.8f, 0.2f, 0.2f, 0.7f);
            _delayedFillImage.type = Image.Type.Filled;
            _delayedFillImage.fillMethod = Image.FillMethod.Horizontal;
            Stretch(_delayedFillImage.rectTransform);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(canvasGO.transform, false);
            _fillImage = fillGO.AddComponent<Image>();
            _fillImage.color = _fullColor;
            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Horizontal;
            Stretch(_fillImage.rectTransform);

            var iconGO = new GameObject("TierIcon");
            iconGO.transform.SetParent(canvasGO.transform, false);
            _tierIconImage = iconGO.AddComponent<Image>();
            _tierIconImage.raycastTarget = false;
            RectTransform iconRect = _tierIconImage.rectTransform;
            iconRect.anchorMin = new Vector2(1f, 0.5f);
            iconRect.anchorMax = new Vector2(1f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = _tierIconSize;
            iconRect.anchoredPosition = _tierIconOffset;
            _tierIconImage.enabled = false;

            ApplySize();
            RefreshTierIcon();
        }

        private static void Stretch(RectTransform rt)
        {
            if (rt == null) return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
