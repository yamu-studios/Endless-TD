// ============================================================================
// ETD.UI - LocalizedText.cs  [NEW]
// Add to any TMP_Text. Auto-updates on language change.
// ============================================================================
using UnityEngine;
using TMPro;
using ETD.Core;

namespace ETD.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string _key;
        [SerializeField] private string _fallback;

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void Start() => Refresh();

        public void SetKey(string key, string fallback = "")
        {
            _key = key; _fallback = fallback; Refresh();
        }

        private void Refresh()
        {
            if (_text != null && !string.IsNullOrEmpty(_key))
                _text.text = LocalizationManager.Get(_key, _fallback);
        }

        private void OnLanguageChanged(LanguageChangedEvent _) => Refresh();
        private void OnDestroy() => EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
    }
}
