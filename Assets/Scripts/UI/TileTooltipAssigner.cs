// ============================================================================
// ETD.UI - TileTooltipAssigner.cs  [NEW]
// Listens for TileSpecialtyAppliedEvent and adds a TooltipTrigger to the
// tile's GameObject so players can hover to see what the specialty does.
// Lives in ETD.UI so it can reference both ETD.Grid and ETD.UI components.
// Add to [Managers] or any always-active GO in the Game scene.
// ============================================================================
using UnityEngine;
using UnityEngine.EventSystems;
using ETD.Core;
using ETD.Data;
using ETD.Grid;

namespace ETD.UI
{
    public class TileTooltipAssigner : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameDatabase _database;

        private GridSystem _grid;

       
        private void Awake()
        {
            Initialize();
           
        }

        public void Initialize()
        {
            ServiceLocator.TryGet(out _grid);
            EventBus.Subscribe<TileSpecialtyAppliedEvent>(OnSpecialtyApplied);
        }
        private void OnSpecialtyApplied(TileSpecialtyAppliedEvent evt)
        {
            if (_grid == null) ServiceLocator.TryGet(out _grid);
            if (_grid == null) return;
            var cell = _grid.GetCell(evt.GridPos);


            if (cell?.TileObject == null) return;

            var specialty = (TileSpecialty)evt.Specialty;

            // The event only carries the category, but the cell already holds the exact
            // tile that was placed. Previously this looked up "first tile in the database
            // with this category", which always resolved to the original Blessed/Cursed/
            // Greed and made every other variant invisible in the tooltip.
            DynamicTileData tileData = cell.DynamicTile ?? FindTileData(specialty);
            if (tileData == null) return;

            AssignTooltip(cell.TileObject, tileData);
        }

        /// <summary>
        /// Fallback only, for cells whose DynamicTile was not set (e.g. legacy saves).
        /// Returns the first tile of the category, so it cannot distinguish variants —
        /// prefer GridCell.DynamicTile.
        /// </summary>
        private DynamicTileData FindTileData(TileSpecialty specialty)
        {
            if (_database?.DynamicTiles == null) return null;

            for (int i = 0; i < _database.DynamicTiles.Length; i++)
            {
                var dt = _database.DynamicTiles[i];
                if (dt != null && dt.Category == specialty)
                    return dt;
            }
            return null;
        }

        private void AssignTooltip(GameObject tileGO, DynamicTileData tileData)
        {
            // Add physics collider for hover detection if needed
            // (tiles already have BoxCollider for ground raycast)
            // We use a world-space hover approach via TooltipTrigger3D
    
            var trigger = tileGO.GetComponent<TileTooltipTrigger3D>();
            if (trigger == null)
                trigger = tileGO.AddComponent<TileTooltipTrigger3D>();

            trigger.SetData(tileData);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TileSpecialtyAppliedEvent>(OnSpecialtyApplied);
        }
    }

   
}
