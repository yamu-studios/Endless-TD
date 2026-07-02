// ============================================================================
// ETD.Turrets - TurretRangeIndicator.cs  [UPDATED]
// Now also listens for TurretUpgradedEvent and TurretEvolvedEvent
// so the circle redraws with the updated range after each upgrade.
// ============================================================================
using UnityEngine;
using ETD.Core;

namespace ETD.Turrets
{
    public class TurretRangeIndicator : MonoBehaviour
    {
        [Header("Appearance")]
        [SerializeField] private int _segments = 64;
        [SerializeField] private float _lineWidth = 0.08f;
        [SerializeField] private Color _selectedColor = new Color(0.3f, 0.8f, 1f, 0.8f);
        [SerializeField] private float _yOffset = 0.05f;

        private LineRenderer _lineRenderer;
        private TurretController _turretController;
        private bool _isVisible;

        private void Awake()
        {
            _turretController = GetComponent<TurretController>();
            SetupLineRenderer();
            Hide();

            EventBus.Subscribe<TurretSelectedEvent>(OnTurretSelected);
            EventBus.Subscribe<TurretDeselectedEvent>(OnTurretDeselected);
            EventBus.Subscribe<TurretUpgradedEvent>(OnTurretUpgraded);   // ← NEW
            EventBus.Subscribe<TurretEvolvedEvent>(OnTurretEvolved);     // ← NEW
        }

        private void SetupLineRenderer()
        {
            var go = new GameObject("RangeCircle");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;

            _lineRenderer = go.AddComponent<LineRenderer>();
            _lineRenderer.loop = true;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;
            _lineRenderer.positionCount = _segments;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;

            var mat = new Material(Shader.Find("Sprites/Default"));
            _lineRenderer.material = mat;
        }

        // =================================================================
        // SHOW / HIDE
        // =================================================================

        public void ShowSelected(float range)
        {
            DrawCircle(_turretController.transform.position, range, _selectedColor);
            _isVisible = true;
        }

        public void Hide()
        {
            if (_lineRenderer != null)
                _lineRenderer.enabled = false;
            _isVisible = false;
        }

        private void DrawCircle(Vector3 center, float radius, Color color)
        {
            if (_lineRenderer == null) return;

            _lineRenderer.enabled = true;
            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;

            float angleStep = 360f / _segments;
            for (int i = 0; i < _segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = center.x + Mathf.Cos(angle) * radius;
                float z = center.z + Mathf.Sin(angle) * radius;
                _lineRenderer.SetPosition(i, new Vector3(x, center.y + _yOffset, z));
            }
        }

        // =================================================================
        // EVENT HANDLERS
        // =================================================================

        private void OnTurretSelected(TurretSelectedEvent evt)
        {
            if (_turretController == null || _turretController.InstanceId != evt.TurretId)
            {
                Hide();
                return;
            }
            ShowSelected(_turretController.Range);
        }

        private void OnTurretDeselected(TurretDeselectedEvent evt)
        {
            Hide();
        }

        private void OnTurretUpgraded(TurretUpgradedEvent evt)
        {
            // If this turret is currently selected and it's the one that upgraded, redraw
            if (_isVisible && _turretController != null
                && _turretController.InstanceId == evt.TurretId)
            {
                ShowSelected(_turretController.Range); // Range already recalculated by RecalculateStats
            }
        }

        private void OnTurretEvolved(TurretEvolvedEvent evt)
        {
            if (_isVisible && _turretController != null
                && _turretController.InstanceId == evt.TurretId)
            {
                ShowSelected(_turretController.Range);
            }
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TurretSelectedEvent>(OnTurretSelected);
            EventBus.Unsubscribe<TurretDeselectedEvent>(OnTurretDeselected);
            EventBus.Unsubscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Unsubscribe<TurretEvolvedEvent>(OnTurretEvolved);
        }

        // =================================================================
        // STATIC PLACEMENT INDICATOR (unchanged)
        // =================================================================

        private static GameObject _sharedIndicatorGO;
        private static LineRenderer _sharedLineRenderer;

        public static void ShowPlacementRange(Vector3 center, float range,
            int segments = 64, float width = 0.08f)
        {
            if (_sharedIndicatorGO == null)
                CreateSharedIndicator(segments, width);

            _sharedIndicatorGO.SetActive(true);

            var color = new Color(0.3f, 1f, 0.3f, 0.6f);
            _sharedLineRenderer.startColor = color;
            _sharedLineRenderer.endColor = color;

            float angleStep = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = center.x + Mathf.Cos(angle) * range;
                float z = center.z + Mathf.Sin(angle) * range;
                _sharedLineRenderer.SetPosition(i, new Vector3(x, center.y + 0.05f, z));
            }
        }

        public static void HidePlacementRange()
        {
            if (_sharedIndicatorGO != null)
                _sharedIndicatorGO.SetActive(false);
        }

        private static void CreateSharedIndicator(int segments, float width)
        {
            _sharedIndicatorGO = new GameObject("[PlacementRangeIndicator]");
            _sharedLineRenderer = _sharedIndicatorGO.AddComponent<LineRenderer>();
            _sharedLineRenderer.loop = true;
            _sharedLineRenderer.useWorldSpace = true;
            _sharedLineRenderer.startWidth = width;
            _sharedLineRenderer.endWidth = width;
            _sharedLineRenderer.positionCount = segments;
            _sharedLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _sharedLineRenderer.receiveShadows = false;
            _sharedLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _sharedIndicatorGO.SetActive(false);
        }
    }
}