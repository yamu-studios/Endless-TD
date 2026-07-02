using UnityEngine;
using UnityEngine.UI;

namespace ETD.Hub
{
    // =========================================================================
    // SHOP ICON ITEM
    // =========================================================================
    public class ShopIconItem : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _border;
        [SerializeField] private Button _button;

        public ShopItemData Data { get; private set; }
        private Color _sel, _def;

        public void Setup(ShopItemData data, Color sel, Color def,
            System.Action<ShopIconItem> onClick)
        {
            Data = data; _sel = sel; _def = def;
            if (_icon != null) { _icon.sprite = data.Icon; _icon.enabled = data.Icon != null; }
            SetBorderSelected(false);
            if (_button == null) _button = GetComponent<Button>();
            _button?.onClick.RemoveAllListeners();
            _button?.onClick.AddListener(() => onClick?.Invoke(this));
        }

        public void SetBorderSelected(bool sel)
        {
            if (_border != null) _border.color = sel ? _sel : _def;
        }
    }

  

}
