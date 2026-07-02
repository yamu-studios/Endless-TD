// ============================================================================
// ETD.Grid - GridVisualizer.cs
// Works with existing tile GameObjects from the scene.
// Changes materials based on tile specialty and shows placement preview.
// Does NOT create tiles — reads them from GridSystem.
// ============================================================================
using UnityEngine;
using ETD.Core;
using ETD.Data;

namespace ETD.Grid
{
    public class GridVisualizer : MonoBehaviour
    {
        [Header("Specialty Materials")]
        [SerializeField] private Material _cursedTileMat;
        [SerializeField] private Material _blessedTileMat;
        [SerializeField] private Material _greedTileMat;

        [Header("Preview")]
        [SerializeField] private GameObject _previewIndicator;
        [SerializeField] private Material _validPreviewMat;
        [SerializeField] private Material _invalidPreviewMat;

        private GridSystem _gridSystem;

        // Cache original materials so we can restore them
        private readonly System.Collections.Generic.Dictionary<Vector2Int, Material> _originalMaterials = new();

        public void Initialize(GridSystem gridSystem)
        {
            _gridSystem = gridSystem;

            // Cache original materials for all tiles
            foreach (var pos in gridSystem.AllPositions)
            {
                var cell = gridSystem.GetCell(pos);
                if (cell?.TileRenderer != null)
                    _originalMaterials[pos] = cell.TileRenderer.sharedMaterial;
            }

            // Listen for specialty events to update visuals
            EventBus.Subscribe<TileSpecialtyAppliedEvent>(OnSpecialtyApplied);

            if (_previewIndicator != null)
                _previewIndicator.SetActive(false);
        }

        private void OnSpecialtyApplied(TileSpecialtyAppliedEvent evt)
        {
            var specialty = (TileSpecialty)evt.Specialty;
            var cell = _gridSystem?.GetCell(evt.GridPos);
            if (cell?.TileRenderer == null) return;

            Material mat = specialty switch
            {
                TileSpecialty.Cursed => _cursedTileMat,
                TileSpecialty.Blessed => _blessedTileMat,
                TileSpecialty.Greed => _greedTileMat,
                _ => null
            };

            if (mat != null)
                cell.TileRenderer.material = mat;
        }

        /// <summary>
        /// Shows placement preview on a grid cell.
        /// Snaps to the tile's actual world position.
        /// </summary>
        public void ShowPlacementPreview(Vector2Int gridPos, bool isValid)
        {
            if (_previewIndicator == null) return;

            var cell = _gridSystem?.GetCell(gridPos);
            if (cell == null)
            {
                HidePlacementPreview();
                return;
            }

            _previewIndicator.SetActive(true);
            _previewIndicator.transform.position = cell.WorldCenter + Vector3.up * 0.01f;

            var renderer = _previewIndicator.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = isValid ? _validPreviewMat : _invalidPreviewMat;
        }

        public void HidePlacementPreview()
        {
            if (_previewIndicator != null)
                _previewIndicator.SetActive(false);
        }

        /// <summary>
        /// Restores a tile to its original material.
        /// </summary>
        public void RestoreTileMaterial(Vector2Int pos)
        {
            var cell = _gridSystem?.GetCell(pos);
            if (cell?.TileRenderer == null) return;

            if (_originalMaterials.TryGetValue(pos, out var original))
                cell.TileRenderer.material = original;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TileSpecialtyAppliedEvent>(OnSpecialtyApplied);
        }
    }
}
