// ============================================================================
// ETD.UI - EndGameDamageStatRowUI.cs
// One League-like damage stat row: icon + name + value + horizontal bar.
// ============================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ETD.UI
{
    public sealed class EndGameDamageStatRowUI : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _damageText;
        [SerializeField] private Image _barFill;
        [SerializeField] private GameObject _root;

        public void Set(Sprite icon, string displayName, string damageText, float normalizedFill)
        {
            if (_root != null)
                _root.SetActive(true);
            else if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.enabled = icon != null;
            }

            if (_nameText != null)
                _nameText.text = displayName ?? string.Empty;

            if (_damageText != null)
                _damageText.text = damageText ?? string.Empty;

            if (_barFill != null)
                _barFill.fillAmount = Mathf.Clamp01(normalizedFill);
        }

        public void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
            else
                gameObject.SetActive(false);
        }
    }
}
