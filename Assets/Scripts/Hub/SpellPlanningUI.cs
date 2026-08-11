// ============================================================================
// ETD.Hub - SpellPlanningUI.cs
// v1.0 active spell system planning-tab picker (see [[etd-v1-full-release]]
// Phase 4). Instantiates one SpellPlanningItem per shipped spell from a prefab,
// the same data-driven shape as PlanningWindowUI's trait list — adding a spell
// to the GameDatabase is now the only step needed to see it in the hub.
//
// No shared details panel: each row carries its own cooldown + explanation, so
// unlike the trait tab there is nothing left for a side panel to show.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;

namespace ETD.Hub
{
    public class SpellPlanningUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameDatabase _database;

        [Header("Spawning")]
        [SerializeField] private SpellPlanningItem _itemPrefab;
        [Tooltip("Parent for the spawned rows — normally the layout group under SpellsContent.")]
        [SerializeField] private Transform _itemContainer;

        private readonly List<SpellPlanningItem> _items = new List<SpellPlanningItem>();
        private string _selectedSpellId;
        private int _upgradeLevel;

        /// <summary>Exposed for HubTutorialAnimator to find an item to pulse.</summary>
        public IReadOnlyList<SpellPlanningItem> Items => _items;

        private void OnEnable() => Refresh();

        public void Refresh()
        {
            if (_database == null || _database.Spells == null ||
                _itemPrefab == null || _itemContainer == null)
                return;

            var save = SaveSystem.Load();
            _upgradeLevel = Mathf.Max(0, save.ShopSpellUpgradeLevel);

            // The planning pick is always free to change: it is the choice for the NEXT
            // fresh run. A resumable run does not own this tab — it carries its own spell
            // on its snapshot, and RunSnapshotManager re-applies that on resume, so
            // changing the pick here can never affect a run already in progress.
            _selectedSpellId = save.SelectedSpellId;

            // No saved pick (or it points at a spell that no longer ships): fall
            // back to the first entry so the tab is never shown with nothing
            // highlighted, and persist it so the run agrees with the hub.
            if (_database.GetSpell(_selectedSpellId) == null
                && _database.Spells.Length > 0)
            {
                var fallback = _database.Spells[0];
                if (fallback != null)
                {
                    _selectedSpellId = fallback.Id;
                    SaveSystem.SaveSelectedSpell(_selectedSpellId);
                }
            }

            Rebuild();
        }

        private void Rebuild()
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i] != null)
                    Destroy(_items[i].gameObject);
            _items.Clear();

            for (int i = 0; i < _database.Spells.Length; i++)
            {
                var spell = _database.Spells[i];
                if (spell == null) continue;

                var item = Instantiate(_itemPrefab, _itemContainer);
                item.gameObject.name = "SpellItem_" + spell.Id;
                item.Setup(spell, _upgradeLevel, spell.Id == _selectedSpellId, OnSpellClicked);
                _items.Add(item);
            }
        }

        private void OnSpellClicked(string spellId)
        {
            _selectedSpellId = spellId;
            SaveSystem.SaveSelectedSpell(spellId);

            for (int i = 0; i < _items.Count; i++)
                if (_items[i] != null)
                    _items[i].SetSelected(_items[i].SpellId == spellId);
        }
    }
}
