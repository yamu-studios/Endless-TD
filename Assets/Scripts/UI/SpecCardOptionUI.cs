using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Data;
using ETD.Core;

namespace ETD.UI
{

    [System.Serializable]
    public class SpecCardOptionUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _rarityBorder;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _rarityText;
        [SerializeField] private Button _button;

        public void Setup(SpecCardData data, System.Action onSelect)
        {

            if (_icon != null) _icon.sprite = data.Icon;
            if (_nameText != null) _nameText.text = SOLocalization.GetName("speccard_"+data.LocalizationKey, data.DisplayName);
            if (_descriptionText != null) _descriptionText.text = BalanceDescriptionFormatter.AppendSpecCardNumbers(data, SOLocalization.GetDesc("speccard_" + data.LocalizationKey, data.Description));
            if (_rarityText != null) _rarityText.text = RarityColorHelper.GetLocalizedName(data.Rarity) ;

            Color rarityColor = data.GetRarityColor();
            if (_rarityBorder != null) _rarityBorder.color = rarityColor;
            if (_rarityText != null) _rarityText.color = rarityColor;

            if (_button == null) _button = GetComponent<Button>();
            _button?.onClick.RemoveAllListeners();
            _button?.onClick.AddListener(() => onSelect?.Invoke());
            if (_button != null) _button.interactable = true;
        }

        public void SetInteractable(bool interactable)
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button != null) _button.interactable = interactable;
        }
    }
}
