using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ETD.Hub
{
    // =========================================================================
    // TURRET LIST ITEM — icon + name
    // =========================================================================
    public class TurretListItemUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private GameObject _lockOverlay;
        [SerializeField] private Button _button;

        public string ItemId { get; private set; }

        public void Setup(string id, Sprite icon, string displayName, bool unlocked,
            System.Action<string> onClick)
        {
            ItemId = id;

            if (_icon != null) _icon.sprite = icon;
            if (_nameText != null) _nameText.text = unlocked ? displayName : "???";
            if (_lockOverlay != null) _lockOverlay.SetActive(!unlocked);

            if (_button == null) _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => onClick?.Invoke(id));
            }
        }
    }
}
