// ============================================================================
// ETD.Hub - PlanningTabSwitcher.cs  [NEW]
// v1.0 active spell system (see [[etd-v1-full-release]] Phase 4). Toggles the
// Planning window between its existing Traits view and the new Spell view
// in-place, instead of adding a whole new Hub nav button + top-level window —
// keeps the main nav bar (a fixed decorative panel, not layout-driven) untouched.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;

namespace ETD.Hub
{
    public class PlanningTabSwitcher : MonoBehaviour
    {
        [Tooltip("Existing trait-view GameObjects (TraitsContent, SelectedTraits, TraitDetails) — toggled off while the spell view is showing.")]
        [SerializeField] private GameObject[] _traitsGroup;
        [Tooltip("New spell-view GameObjects (SpellsContent, SpellDetails) — toggled off while the trait view is showing.")]
        [SerializeField] private GameObject[] _spellsGroup;
        [SerializeField] private Button _toggleButton;
        [SerializeField] private TMP_Text _toggleButtonText;
        [SerializeField] private SpellPlanningUI _spellPlanningUI;

        private bool _showingSpells;

        private void OnEnable()
        {
            _showingSpells = false;
            Apply();
            _toggleButton?.onClick.RemoveAllListeners();
            _toggleButton?.onClick.AddListener(Toggle);
        }

        private void Toggle()
        {
            _showingSpells = !_showingSpells;
            Apply();
        }

        private void Apply()
        {
            if (_traitsGroup != null)
                for (int i = 0; i < _traitsGroup.Length; i++)
                    if (_traitsGroup[i] != null) _traitsGroup[i].SetActive(!_showingSpells);

            if (_spellsGroup != null)
                for (int i = 0; i < _spellsGroup.Length; i++)
                    if (_spellsGroup[i] != null) _spellsGroup[i].SetActive(_showingSpells);

            if (_showingSpells)
                _spellPlanningUI?.Refresh();

            if (_toggleButtonText != null)
                _toggleButtonText.text = _showingSpells
                    ? LocalizationManager.Get("planning_traits", "Traits")
                    : LocalizationManager.Get("planning_spells", "Spells");
        }
    }
}
