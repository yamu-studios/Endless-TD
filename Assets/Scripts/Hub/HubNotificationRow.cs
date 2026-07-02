using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ETD.Hub
{
    // =========================================================================
    // ROW
    // =========================================================================
    public class HubNotificationRow : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private TMP_Text _subtitleText;

        public void Setup(Sprite icon, string label, string subtitle)
        {
            if (_icon != null) { _icon.sprite = icon; _icon.enabled = icon != null; }
            if (_labelText != null) _labelText.text = label;
            if (_subtitleText != null) _subtitleText.text = subtitle;
        }
    }
}
