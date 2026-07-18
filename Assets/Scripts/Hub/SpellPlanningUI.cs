// ============================================================================
// ETD.Hub - SpellPlanningUI.cs  [NEW]
// v1.0 active spell system planning-tab picker (see [[etd-v1-full-release]]
// Phase 4). Static slots (one per shipped spell, matched by array index) rather
// than a dynamically-instantiated list like PlanningWindowUI's traits — there
// are only 4 spells and no unlock/rarity system for them, so a data-driven list
// would be pure overhead for content this small.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Data;

namespace ETD.Hub
{
    public class SpellPlanningUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameDatabase _database;

        [Header("Spell Slots (index-matched to _database.Spells)")]
        [SerializeField] private SpellTabItem[] _items;

        [Header("Info Panel")]
        [SerializeField] private Image _infoIcon;
        [SerializeField] private TMP_Text _infoName;
        [SerializeField] private TMP_Text _infoDescription;
        [SerializeField] private TMP_Text _infoCooldownText;

        private string _selectedSpellId;

        private void OnEnable() => Refresh();

        public void Refresh()
        {
            var save = SaveSystem.Load();
            _selectedSpellId = save.SelectedSpellId;

            if (_database == null || _database.Spells == null || _items == null)
                return;

            SpellData toShow = null;

            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i] == null) continue;

                if (i >= _database.Spells.Length || _database.Spells[i] == null)
                {
                    _items[i].gameObject.SetActive(false);
                    continue;
                }

                var spell = _database.Spells[i];
                bool selected = spell.Id == _selectedSpellId;
                _items[i].Setup(spell, selected, OnSpellClicked);

                if (selected) toShow = spell;
            }

            if (toShow == null && _database.Spells.Length > 0)
                toShow = _database.Spells[0];

            if (toShow != null)
                ShowInfo(toShow);
        }

        private void OnSpellClicked(string spellId)
        {
            _selectedSpellId = spellId;
            SaveSystem.SaveSelectedSpell(spellId);

            for (int i = 0; i < _items.Length; i++)
                if (_items[i] != null)
                    _items[i].SetSelected(_items[i].SpellId == spellId);

            var spell = _database.GetSpell(spellId);
            if (spell != null) ShowInfo(spell);
        }

        private void ShowInfo(SpellData spell)
        {
            string baseKey = "spell_" + spell.LocalizationKey;

            if (_infoIcon != null)
            {
                _infoIcon.sprite = spell.Icon;
                _infoIcon.enabled = spell.Icon != null;
            }

            if (_infoName != null)
                _infoName.text = SOLocalization.GetName(baseKey, spell.DisplayName);

            if (_infoDescription != null)
                _infoDescription.text = SOLocalization.GetDesc(baseKey, spell.Description);

            if (_infoCooldownText != null)
                _infoCooldownText.text = LocalizationManager.GetFormat(
                    "spell_cooldown_format", "Cooldown: {0}s", Mathf.RoundToInt(spell.Cooldown));
        }
    }
}
