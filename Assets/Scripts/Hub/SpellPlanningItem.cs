// ============================================================================
// ETD.Hub - SpellPlanningItem.cs  (was SpellTabItem)
// v1.0 active spell system planning-tab picker (see [[etd-v1-full-release]]
// Phase 4). Single-select list item: click the body to select it directly (no
// separate checkmark button, unlike PlanningTabItem's multi-select traits —
// only one spell can ever be active, so "click = select" is unambiguous).
//
// Unlike a trait row (Icon | Name | Grade, with the real text living in the
// shared details panel), a spell row is self-contained: Icon | Name | Cooldown
// | Explanation. There are only 4 spells and no rarity/unlock axis, so every
// spell's full text fits on-screen at once and the tab needs no details panel.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Data;
using ETD.Core;

namespace ETD.Hub
{
    public class SpellPlanningItem : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [Tooltip("Effective cooldown, i.e. already reduced by the Arcane Focus shop upgrade.")]
        [SerializeField] private TMP_Text _cooldownText;
        [Tooltip("Localized spell explanation. Leave unassigned on compact layouts.")]
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Image _border;
        [SerializeField] private Button _button;

        [Header("Colors")]
        [SerializeField] private Color _selectedBorderColor = new Color(1f, 0.85f, 0f);
        [SerializeField] private Color _defaultBorderColor = new Color(0.4f, 0.4f, 0.4f);

        public string SpellId { get; private set; }
        public bool IsSelected { get; private set; }

        /// <summary>Fired after this item is clicked and selected. Used by
        /// HubTutorialAnimator to detect "player picked a spell" without coupling
        /// to SpellPlanningUI directly (mirrors PlanningTabItem.OnSelectedCallback).</summary>
        public System.Action<string> OnSelectedCallback;

        private System.Action<string> _onClicked;

        public void Setup(SpellData spell, int upgradeLevel, bool isSelected, System.Action<string> onClicked)
        {
            SpellId = spell.Id;
            _onClicked = onClicked;

            string baseKey = "spell_" + spell.LocalizationKey;

            if (_icon != null)
            {
                _icon.sprite = spell.Icon;
                _icon.enabled = spell.Icon != null;
            }

            if (_nameText != null)
                _nameText.text = SOLocalization.GetName(baseKey, spell.DisplayName);

            if (_cooldownText != null)
                _cooldownText.text = LocalizationManager.GetFormat(
                    "spell_cooldown_format", "Cooldown: {0}s",
                    Mathf.RoundToInt(spell.GetEffectiveCooldown(upgradeLevel)));

            if (_descriptionText != null)
                _descriptionText.text = SOLocalization.GetDesc(baseKey, spell.Description);

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() =>
                {
                    _onClicked?.Invoke(SpellId);
                    OnSelectedCallback?.Invoke(SpellId);
                });
            }

            SetSelected(isSelected);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (_border != null)
                _border.color = selected ? _selectedBorderColor : _defaultBorderColor;
        }
    }
}
