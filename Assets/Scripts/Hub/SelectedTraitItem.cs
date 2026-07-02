// ============================================================================
// ETD.Hub - SelectedTraitItem.cs  [NEW]
// Shows a selected trait in the "Selected Traits" section.
// Has: Icon, Name, Grade text (grade-colored), Deselect button.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Data;
using ETD.Core;

namespace ETD.Hub
{
    public class SelectedTraitItem : MonoBehaviour
    {
        [SerializeField] private Image   _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _gradeText;
        [SerializeField] private Button  _deselectButton;

        public string ItemId { get; private set; }

        public void Setup(string id, Sprite icon, string displayName,
            SpecCardRarity grade, System.Action<string> onDeselect, TraitData data = null)
        {
            ItemId = id;

            if (_icon != null) _icon.sprite = icon;
            if (_nameText  != null) _nameText.text = SOLocalization.GetName("trait_" + data.LocalizationKey, data.DisplayName);

            if (_gradeText != null)
            {
                _gradeText.text  = RarityColorHelper.GetLocalizedName(grade);
                _gradeText.color = HubController.Instance.GetColor(grade);//grade.GetRarityColor();
            }

            if (_deselectButton != null)
            {
                _deselectButton.onClick.RemoveAllListeners();
                _deselectButton.onClick.AddListener(() => onDeselect?.Invoke(id));
            }
        }
    }
}
