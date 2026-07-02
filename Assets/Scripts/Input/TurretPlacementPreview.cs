// ============================================================================
// ETD.Input - TurretPlacementPreview.cs  [NEW]
// Shows a ghost/blueprint of the turret before placing it.
// Snaps to grid, shows valid/invalid colors, rotates with mouse.
// ============================================================================
using UnityEngine;
using ETD.Core;
using ETD.Data;
using ETD.Grid;
using ETD.Turrets;

namespace ETD.Input
{
    public class TurretPlacementPreview : MonoBehaviour
    {
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private LayerMask _groundLayer;

        [Header("Preview Settings")]
        [SerializeField] private Material _validMaterial;
        [SerializeField] private Material _invalidMaterial;
        [SerializeField] private float _ghostAlpha = 0.5f;
        [SerializeField] private Color _validTint = new Color(1f, 1f, 1f, 0.5f);
        [SerializeField] private Color _invalidTint = new Color(1f, 0.3f, 0.3f, 0.5f);

        private GameObject _ghostInstance;
        private Renderer[] _ghostRenderers;
        private Material[] _originalMaterials;
        private TurretData _currentData;
        private GridSystem _grid;
        private TurretManager _turretManager;
        private bool _isActive;
        private Vector2Int _lastGridPos;
        private bool _lastValid;

        private void Start()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
        }

        public void Show(TurretData data)
        {
            _currentData = data;
            _grid = ServiceLocator.Get<GridSystem>();
            _turretManager = ServiceLocator.Get<TurretManager>();

            // Destroy old ghost
            Hide();

            if (data.Prefab == null) return;

            // Instantiate ghost
            _ghostInstance = Instantiate(data.Prefab);
            _ghostInstance.name = "[Blueprint] " + data.DisplayName;

            // Disable all gameplay components on the ghost
            DisableComponents(_ghostInstance);

            // Collect renderers and make them transparent
            _ghostRenderers = _ghostInstance.GetComponentsInChildren<Renderer>();
            _originalMaterials = new Material[_ghostRenderers.Length];
            for (int i = 0; i < _ghostRenderers.Length; i++)
            {
                _originalMaterials[i] = _ghostRenderers[i].material;
                SetGhostColor(_validTint);
            }

            // Disable colliders on ghost
            var colliders = _ghostInstance.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            _isActive = true;
        }

        public void Hide()
        {
            if (_ghostInstance != null)
            {
                Destroy(_ghostInstance);
                _ghostInstance = null;
            }
            _ghostRenderers = null;
            _originalMaterials = null;
            _isActive = false;
            _currentData = null;
        }

        private void Update()
        {
            if (!_isActive || _ghostInstance == null || _mainCamera == null) return;

            // Raycast to ground
            var ray = _mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 200f, _groundLayer))
            {
                _ghostInstance.SetActive(false);
                return;
            }

            _ghostInstance.SetActive(true);

            // Snap to grid
            if (_grid == null) return;
            Vector2Int gridPos = _grid.WorldToNearestCell(hit.point);
            Vector3 worldPos = _grid.GridToWorld(gridPos);
            worldPos.y = 0;
            _ghostInstance.transform.position = worldPos;

            // Check validity
            if (gridPos != _lastGridPos)
            {
                _lastGridPos = gridPos;
                bool canPlace = _turretManager != null && _turretManager.CanPlaceTurret(gridPos);
                _lastValid = canPlace;
                SetGhostColor(canPlace ? _validTint : _invalidTint);
            }
        }

        public bool IsValid => _lastValid;
        public Vector2Int CurrentGridPos => _lastGridPos;

        private void SetGhostColor(Color tint)
        {
            if (_ghostRenderers == null) return;
            for (int i = 0; i < _ghostRenderers.Length; i++)
            {
                if (_ghostRenderers[i] == null) continue;
                var mat = _ghostRenderers[i].material;
                // Set rendering mode to transparent
                SetMaterialTransparent(mat);

                    mat.color = tint;
            }
        }

        private void SetMaterialTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1); // URP: 0=Opaque, 1=Transparent
            mat.SetFloat("_Blend", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        private void DisableComponents(GameObject go)
        {
            // Disable all MonoBehaviours on the ghost (gameplay scripts)
            var behaviours = go.GetComponentsInChildren<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
                behaviours[i].enabled = false;
        }
    }
}
