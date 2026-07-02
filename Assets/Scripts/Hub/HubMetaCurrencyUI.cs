// ============================================================================
// ETD.Hub - HubMetaCurrencyUI.cs  [NEW]
// Displays meta-currency amount on the main hub panel (near Play, Planning etc.)
// Replaces the old reference that was inside ShopWindowUI.
// Subscribe to MetaCurrencyChangedEvent so it updates automatically.
// Place the component on the same GO as the meta-currency TMP_Text.
// ============================================================================
using UnityEngine;
using TMPro;
using ETD.Core;

namespace ETD.Hub
{
    public class HubMetaCurrencyUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text _metaText;
        [SerializeField] private UnityEngine.UI.Image _metaIcon; // optional coin icon

        [Header("Prefix")]
        [SerializeField] private string _prefix = "";      // e.g. "⬡ " or leave empty

        private void Awake()
        {
            EventBus.Subscribe<MetaCurrencyChangedEvent>(OnMetaChanged);
        }

        private void Start()
        {
            Refresh();
        }

        private void OnMetaChanged(MetaCurrencyChangedEvent evt)
        {
            if (_metaText != null)
                _metaText.text = _prefix + evt.Current.ToString();
        }

        public void Refresh()
        {
            int current = SaveSystem.Load().MetaCurrency;
            if (_metaText != null)
                _metaText.text = _prefix + current.ToString();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<MetaCurrencyChangedEvent>(OnMetaChanged);
        }
    }
}
