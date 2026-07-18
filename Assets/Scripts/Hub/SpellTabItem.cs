// ============================================================================
// ETD.Hub - SpellTabItem.cs  [NEW]
// v1.0 active spell system planning-tab picker (see [[etd-v1-full-release]]
// Phase 4). Single-select list item: click the body to select it directly (no
// separate checkmark button, unlike PlanningTabItem's multi-select traits —
// only one spell can ever be active, so "click = select" is unambiguous).
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Data;
using ETD.Core;

namespace ETD.Hub
{
    public class SpellTabItem : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private Image _border;
        [SerializeField] private Button _button;

        [Header("Colors")]
        [SerializeField] private Color _selectedBorderColor = new Color(1f, 0.85f, 0f);
        [SerializeField] private Color _defaultBorderColor = new Color(0.4f, 0.4f, 0.4f);

        public string SpellId { get; private set; }
        public bool IsSelected { get; private set; }

        private System.Action<string> _onClicked;

        public void Setup(SpellData spell, bool isSelected, System.Action<string> onClicked)
        {
            SpellId = spell.Id;
            _onClicked = onClicked;

            if (_icon != null)
                _icon.sprite = spell.Icon;

            if (_nameText != null)
                _nameText.text = SOLocalization.GetName("spell_" + spell.LocalizationKey, spell.DisplayName);

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => _onClicked?.Invoke(SpellId));
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
