using UnityEngine;
using UnityEngine.UI;


namespace ETD.Hub
{
    // =========================================================================
    // SHOP ITEM UI — icon + progress squares
    // =========================================================================
    public class ShopItemUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Transform _progressContainer;
        [SerializeField] private GameObject _progressSquarePrefab;
        [SerializeField] private Button _button;
        [SerializeField] private Color _filledColor = new Color(0.3f, 0.85f, 0.3f);
        [SerializeField] private Color _emptyColor = new Color(0.25f, 0.25f, 0.25f);

        public ShopItemData Data { get; private set; }

        public void Setup(ShopItemData data, bool isMaxed, System.Action<ShopItemData> onClick)
        {
            Data = data;

            if (_icon != null) { _icon.sprite = data.Icon; _icon.enabled = data.Icon != null; }

            // Progress squares
            if (_progressContainer != null && _progressSquarePrefab != null)
            {
                for (int i = _progressContainer.childCount - 1; i >= 0; i--)
                    Destroy(_progressContainer.GetChild(i).gameObject);

                int displayMax = Mathf.Min(data.MaxLevel, 10);
                for (int i = 0; i < displayMax; i++)
                {
                    var sq = Instantiate(_progressSquarePrefab, _progressContainer);
                    var img = sq.GetComponent<Image>();
                    if (img != null)
                        img.color = i < data.CurrentLevel ? _filledColor : _emptyColor;
                }
            }

            if (_button == null) _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => onClick?.Invoke(data));
            }
        }
    }
}
