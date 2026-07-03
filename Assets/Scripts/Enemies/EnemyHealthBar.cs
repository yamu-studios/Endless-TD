// ============================================================================
// ETD.Enemies - EnemyHealthBar.cs  [SHARED-CANVAS]
// Pooled enemy health bar driven centrally by EnemyHealthBarSystem.
// The per-enemy world-space Canvas is stripped at runtime and the bar content
// (the prefab's own images, so styling is preserved exactly) is reparented
// under one shared canvas: one canvas rebuild + batched draws instead of one
// canvas per enemy, and one system LateUpdate instead of one per bar.
// Behavior preserved from the previous per-canvas version: show-on-any-damage
// timer, hide-when-full, elite/boss always visible with tier icons, delayed
// fill, width scaling by base health, camera billboarding.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using ETD.Data;

namespace ETD.Enemies
{
    public class EnemyHealthBar : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Prefab-authored world-space canvas. At runtime its Canvas/CanvasScaler/GraphicRaycaster components are removed and the content is moved under the shared health bar canvas.")]
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
        private RectTransform _barRoot;
        // Original parent of the bar content. The old code assigned the canvas
        // localPosition = _offset each frame; TransformPoint on this anchor is
        // the same world position now that the content lives elsewhere.
        private Transform _barAnchor;
        private float _baseBarWidth;
        private float _delayedFillAmount = 1f;
        private float _visibleTimer;
        private float _lastObservedHealth = float.NaN;
        private bool _initialized;
        private bool _barVisible;

        /// <summary>Slot index inside EnemyHealthBarSystem. Managed by the system.</summary>
        internal int RegisteredIndex = -1;

        private void Awake()
        {
            _baseBarWidth = Mathf.Max(0.01f, _barWidth);
            ResolveEnemy();
            EnsureBarRoot();
            ApplySize();
        }

        private void OnEnable()
        {
            ResolveEnemy();
            EnsureBarRoot();
            ApplySize();

            _delayedFillAmount = 1f;
            _visibleTimer = 0f;
            _lastObservedHealth = _enemy != null ? _enemy.CurrentHealth : float.NaN;

            if (_fillImage != null)
                _fillImage.fillAmount = 1f;

            if (_delayedFillImage != null)
                _delayedFillImage.fillAmount = 1f;

            RefreshTierIcon();
            SetBarVisible(!_hideWhenFull || IsPriorityTier());

            _initialized = true;
            EnemyHealthBarSystem.Register(this);
        }

        private void OnDisable()
        {
            EnemyHealthBarSystem.Unregister(this);

            // The bar content lives under the shared canvas, not under this
            // pooled enemy, so it must be hidden explicitly on release.
            SetBarVisible(false);
        }

        private void OnDestroy()
        {
            // The reparented content is no longer a child of the enemy and
            // would otherwise leak under the shared canvas.
            if (_barRoot != null)
                Destroy(_barRoot.gameObject);
        }

        /// <summary>
        /// Called once per frame by EnemyHealthBarSystem. Logic is identical to
        /// the previous per-component LateUpdate; only the visibility toggle
        /// (root active state instead of canvas.enabled) and the billboard
        /// source (shared per-frame rotation) changed.
        /// </summary>
        internal void ManagedLateUpdate(float deltaTime, Quaternion billboardRotation, bool hasBillboardRotation)
        {
            if (!_initialized || _barRoot == null)
                return;

            if (_enemy == null)
            {
                ResolveEnemy();
                if (_enemy == null)
                    return;
            }

            float maxHealth = SanitizeHealth(_enemy.MaxHealth, 0f, 1e30f);

            // Pooled enemies are enabled before Initialize() finishes. Wait until health is valid.
            if (maxHealth <= 0f)
            {
                SetBarVisible(false);
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
                _visibleTimer -= deltaTime;

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
                    _delayedFillSpeed * deltaTime);
                _delayedFillImage.fillAmount = _delayedFillAmount;
            }

            RefreshTierIcon();

            if (_hideWhenFull)
            {
                bool visiblyDamaged = ratio < _damagedVisibleThreshold;
                bool recentlyDamaged = _visibleTimer > 0f;
                bool priorityTier = IsPriorityTier();
                SetBarVisible(priorityTier || visiblyDamaged || recentlyDamaged);
            }
            else
            {
                SetBarVisible(true);
            }

            if (!_barVisible)
                return;

            Transform anchor = _barAnchor != null ? _barAnchor : transform;
            Vector3 worldPosition = anchor.TransformPoint(SanitizeVector3(_offset, Vector3.zero));
            if (hasBillboardRotation)
                _barRoot.SetPositionAndRotation(worldPosition, billboardRotation);
            else
                _barRoot.position = worldPosition;
        }

        private void SetBarVisible(bool visible)
        {
            if (_barRoot == null || _barVisible == visible)
                return;

            _barVisible = visible;
            _barRoot.gameObject.SetActive(visible);
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

        /// <summary>
        /// Resolves the bar content root, strips the per-enemy canvas components
        /// so the content batches into the shared canvas, and reparents it there.
        /// Runs the strip exactly once per instance; later calls only re-attach.
        /// </summary>
        private void EnsureBarRoot()
        {
            if (_barRoot != null)
            {
                EnemyHealthBarSystem.AttachBar(_barRoot);
                return;
            }

            if (_canvas == null)
                _canvas = GetComponentInChildren<Canvas>(true);

            if (_canvas == null)
                SetupCanvas();

            if (_canvas == null)
                return;

            RectTransform rect = _canvas.GetComponent<RectTransform>();
            if (rect == null)
                return;

            _barRoot = rect;
            _barAnchor = rect.parent != null ? rect.parent : transform;
            _barVisible = rect.gameObject.activeSelf;

            if (_tierIconImage == null)
                CreateTierIcon(rect);

            // Dependent components must go before the Canvas itself
            // (CanvasScaler and GraphicRaycaster both require Canvas).
            var scaler = _canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
                Destroy(scaler);

            var raycaster = _canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                Destroy(raycaster);

            Destroy(_canvas);
            _canvas = null;

            EnemyHealthBarSystem.AttachBar(_barRoot);
        }

        private void ApplySize()
        {
            if (_barRoot == null)
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
            _barRoot.sizeDelta = new Vector2(safeWidth, safeHeight);
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
        /// Auto-creates the world-space bar hierarchy when the prefab does not
        /// have one. The Canvas created here is stripped again by EnsureBarRoot;
        /// it only exists to keep this path identical to the prefab-authored one.
        /// </summary>
        private void SetupCanvas()
        {
            var canvasGO = new GameObject("HealthBar_Canvas");
            canvasGO.transform.SetParent(transform, false);
            canvasGO.transform.localPosition = SanitizeVector3(_offset, Vector3.zero);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 10;

            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.localScale = Vector3.one * 0.01f;

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

            CreateTierIcon(canvasGO.transform);
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
