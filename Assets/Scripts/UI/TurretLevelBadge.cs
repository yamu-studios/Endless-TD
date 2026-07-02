// ============================================================================
// ETD.UI - TurretLevelBadge.cs
// World-space 3D canvas level badge for placed turrets. Can be attached to a
// turret prefab or added automatically by TurretLevelBadgeManager.
// ============================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ETD.Core;
using ETD.Turrets;

namespace ETD.UI
{
    [DisallowMultipleComponent]
    public sealed class TurretLevelBadge : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Vector3 _localOffset = new(0f, 1.45f, 0f);
        [SerializeField] private float _worldScale = 0.0125f;
        [SerializeField] private bool _hideAtLevelOne = false;

        [Header("Style")]
        [SerializeField] private Color _backgroundColor = new(0.05f, 0.06f, 0.07f, 0.86f);
        [SerializeField] private Color _textColor = new(1f, 0.91f, 0.54f, 1f);
        [SerializeField] private int _sortingOrder = 40;

        private TurretController _turret;
        private Camera _camera;
        private Canvas _canvas;
        private TMP_Text _text;
        private RectTransform _canvasRect;
        private float _pulseEndTime;
        private bool _managerEnabled = true;

        private void OnEnable()
        {
            EventBus.Subscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Subscribe<TurretEvolvedEvent>(OnTurretEvolved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Unsubscribe<TurretEvolvedEvent>(OnTurretEvolved);
        }

        public void Bind(TurretController turret, Camera worldCamera = null)
        {
            _turret = turret != null ? turret : GetComponent<TurretController>();
            _camera = worldCamera != null ? worldCamera : Camera.main;
            EnsureVisual();
            Refresh();
        }

        public void SetManagerEnabled(bool enabled)
        {
            _managerEnabled = enabled;
            Refresh();
        }

        private void Start()
        {
            if (_turret == null)
                Bind(GetComponent<TurretController>(), _camera);
        }

        private void LateUpdate()
        {
            if (_canvas == null)
                return;

            _canvas.transform.localPosition = _localOffset;
            float pulseT = _pulseEndTime > Time.unscaledTime
                ? Mathf.Clamp01((_pulseEndTime - Time.unscaledTime) / 0.22f)
                : 0f;
            float pulseScale = 1f + Mathf.Sin(pulseT * Mathf.PI) * 0.25f;
            _canvas.transform.localScale = Vector3.one * Mathf.Max(0.001f, _worldScale) * pulseScale;

            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam != null)
                _canvas.transform.rotation = cam.transform.rotation;
        }

        private void OnTurretUpgraded(TurretUpgradedEvent evt)
        {
            if (_turret != null && evt.TurretId == _turret.InstanceId)
                Refresh(true);
        }

        private void OnTurretEvolved(TurretEvolvedEvent evt)
        {
            if (_turret != null && evt.TurretId == _turret.InstanceId)
                Refresh(true);
        }

        private void Refresh(bool pulse = false)
        {
            if (_turret == null || _text == null || _canvas == null)
                return;

            bool visible = _managerEnabled && (!_hideAtLevelOne || _turret.Level > 1);
            _canvas.gameObject.SetActive(visible);
            if (!visible)
                return;

            _text.text = LocalizationManager.GetFormat("ui_level_short_format", "Lv {0}", _turret.Level);
            _text.color = _textColor;

            if (pulse)
                _pulseEndTime = Time.unscaledTime + 0.22f;
        }

        private void EnsureVisual()
        {
            if (_canvas != null)
                return;

            GameObject canvasObject = new("LevelBadge_WorldCanvas");
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localPosition = _localOffset;
            canvasObject.transform.localScale = Vector3.one * Mathf.Max(0.001f, _worldScale);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = _sortingOrder;

            _canvasRect = canvasObject.GetComponent<RectTransform>();
            _canvasRect.sizeDelta = new Vector2(120f, 48f);

            GameObject backgroundObject = new("Background");
            backgroundObject.transform.SetParent(canvasObject.transform, false);
            RectTransform bgRect = backgroundObject.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bg = backgroundObject.AddComponent<Image>();
            bg.color = _backgroundColor;
            bg.raycastTarget = false;

            GameObject textObject = new("Text");
            textObject.transform.SetParent(canvasObject.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObject.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 28f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 18f;
            tmp.fontSizeMax = 32f;
            tmp.raycastTarget = false;
            _text = tmp;
        }
    }
}
