// ============================================================================
// ETD.Hub - LanguageWindowUI.cs  [NEW]
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;

namespace ETD.Hub
{
    public class LanguageWindowUI : MonoBehaviour
    {
        [SerializeField] private Transform _buttonContainer;
        [SerializeField] private GameObject _languageButtonPrefab;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _titleText;

        private Button[] _buttons;

        private void OnEnable()
        {
            _closeButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.AddListener(() => gameObject.SetActive(false));
            if (_titleText != null)
                _titleText.text = LocalizationManager.Get("language_select", "Select Language");
            GenerateButtons();
        }

        private void GenerateButtons()
        {
            if (_buttonContainer != null)
                for (int i = _buttonContainer.childCount - 1; i >= 0; i--)
                    Destroy(_buttonContainer.GetChild(i).gameObject);

            var langs   = LocalizationManager.SupportedLanguages;
            var names   = LocalizationManager.LanguageDisplayNames;
            _buttons    = new Button[langs.Length];
            string cur  = LocalizationManager.CurrentLanguage;

            for (int i = 0; i < langs.Length; i++)
            {
                var go  = Instantiate(_languageButtonPrefab, _buttonContainer);
                var btn = go.GetComponent<Button>();
                var txt = go.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = names[i];
                Highlight(btn, langs[i] == cur);
                string code = langs[i];
                btn?.onClick.AddListener(() => Select(code));
                _buttons[i] = btn;
            }
        }

        private void Select(string code)
        {
            LocalizationManager.Instance?.SetLanguage(code);
            var langs = LocalizationManager.SupportedLanguages;
            for (int i = 0; i < _buttons.Length && i < langs.Length; i++)
                Highlight(_buttons[i], langs[i] == code);
            if (_titleText != null)
                _titleText.text = LocalizationManager.Get("language_select", "Select Language");
            AudioManager.Instance?.PlaySFX(GameSoundConfig.Instance?.ButtonClick, SoundCategory.UI);
        }

        private void Highlight(Button btn, bool active)
        {
            if (btn == null) return;
            var c = btn.colors;
            c.normalColor = active ? new Color(0.3f, 0.85f, 0.3f) : new Color(0.9f, 0.9f, 0.9f);
            btn.colors = c;
        }
    }
}
