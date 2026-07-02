using ETD.Core;
using ETD.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ETD.UI
{
    public class TurretCardUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private Button _button;
        [SerializeField] private Image _canvasGroup;
        [SerializeField] private Color cannotAffordColor;

        public TurretData Data { get; private set; }
        private System.Action<TurretData> _onClick;

        public void Initialize(TurretData data, System.Action<TurretData> onClick)
        {
            Data = data;
            _onClick = onClick;

            if (_icon != null) _icon.sprite = data.Icon;
            if (_nameText != null) _nameText.text = SOLocalization.GetName("turret_" + data.LocalizationKey, data.DisplayName);
            if (_costText != null) _costText.text = data.Cost.ToString();

            if (_button == null) _button = GetComponent<Button>();
            _button?.onClick.AddListener(() => _onClick?.Invoke(Data));
        }

        public void SetAffordable(bool canAfford)
        {
            if (_canvasGroup != null && !canAfford)
            {
                _canvasGroup.enabled = true;
                _canvasGroup.color = cannotAffordColor;
            }else if(_canvasGroup != null && canAfford)
            {
                _canvasGroup.enabled = false;
            }


            if (_button != null)
                _button.interactable = canAfford;
        }
    }
}
