using UnityEngine;
using TMPro;
namespace ETD.UI
{
    // =========================================================================
    // STAT ROW
    // =========================================================================
    public class SpecCardStatRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private TMP_Text _valueText;

        public void Setup(string label, string value, Color labelColor, Color valueColor)
        {
            if (_labelText != null) { _labelText.text = label; _labelText.color = labelColor; }
            if (_valueText != null) { _valueText.text = value; _valueText.color = valueColor; }
        }
    }
}
