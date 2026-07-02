// ============================================================================
// ETD.Core - LocalizationManager.cs  [FULL REWRITE - 18 languages]
// Usage: LocalizationManager.Get("key", "fallback")
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace ETD.Core
{
    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        [Header("Language Files (assign in the same order as SupportedLanguages)")]
        [SerializeField] private TextAsset[] _languageFiles;
        [SerializeField] private string _defaultLanguage = "en";

        public static readonly string[] SupportedLanguages =
        {
            "tr", "en", "zh-cn", "zh-tw", "es", "es-419", "fr", "de", "it", "pt-br", "ru", "pl", "uk", "ja", "ko", "nl", "sv", "cs"
        };

        public static readonly string[] LanguageDisplayNames =
        {
            "Türkçe", "English", "中文(简体)", "中文(繁體)", "Español", "Español (LatAm)", "Français", "Deutsch", "Italiano", "Português (BR)", "Русский", "Polski", "Українська", "日本語", "한국어", "Nederlands", "Svenska", "Čeština"
        };

        private string _currentLanguage = "en";
        private Dictionary<string, string> _strings = new();

        public static string CurrentLanguage => Instance?._currentLanguage ?? "en";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            string saved = SaveSystem.Load().SelectedLanguage;
            SetLanguage(string.IsNullOrEmpty(saved) ? DetectSystemLanguage() : saved);
        }

        public static string Get(string key, string fallback = "")
        {
            if (Instance == null || !Instance._strings.TryGetValue(key, out string val))
            {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[Localization] Missing key: " + key);
#endif
                return string.IsNullOrEmpty(fallback) ? $"[{key}]" : fallback;
             
            }
     
            return val;
        }

        public static string GetFormat(string key, string fallback, params object[] args)
        {
            try { return string.Format(Get(key, fallback), args); }
            catch { return Get(key, fallback); }
        }

        public void SetLanguage(string code)
        {
            int idx = System.Array.IndexOf(SupportedLanguages, code);
            if (idx < 0) idx = System.Array.IndexOf(SupportedLanguages, _defaultLanguage);
            if (idx < 0 || _languageFiles == null || idx >= _languageFiles.Length || _languageFiles[idx] == null)
            {
                Debug.LogWarning($"[Localization] Cannot load language: {code}");
                return;
            }

            _currentLanguage = SupportedLanguages[idx];
            _strings = ParseJSON(_languageFiles[idx].text);

            var save = SaveSystem.Load();
            save.SelectedLanguage = _currentLanguage;
            SaveSystem.Save(save);

            EventBus.Publish(new LanguageChangedEvent { LanguageCode = _currentLanguage });
            Debug.Log($"[Localization] Loaded: {_currentLanguage} ({_strings.Count} keys)");
        }

        public string GetDisplayName(string code)
        {
            int idx = System.Array.IndexOf(SupportedLanguages, code);
            return idx >= 0 ? LanguageDisplayNames[idx] : code;
        }

        private Dictionary<string, string> ParseJSON(string json)
        {
            var r = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json)) return r;
            json = json.Trim().TrimStart('{').TrimEnd('}');
            int i = 0;
            while (i < json.Length)
            {
                while (i < json.Length && ",\n\r \t".IndexOf(json[i]) >= 0) i++;
                if (i >= json.Length || json[i] != '"') { i++; continue; }
                string key = ReadQuoted(json, ref i);
                while (i < json.Length && json[i] != ':') i++;
                i++;
                while (i < json.Length && " \t".IndexOf(json[i]) >= 0) i++;
                if (i >= json.Length) break;
                string val = json[i] == '"' ? ReadQuoted(json, ref i) : ReadRaw(json, ref i);
                r[key] = val;
            }
            return r;
        }

        private string ReadQuoted(string s, ref int i)
        {
            i++;
            var sb = new System.Text.StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length) { i++; sb.Append(s[i] == 'n' ? '\n' : s[i] == 't' ? '\t' : s[i]); }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }

        private string ReadRaw(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && ",}\n".IndexOf(s[i]) < 0) i++;
            return s.Substring(start, i - start).Trim();
        }

        private string DetectSystemLanguage() => Application.systemLanguage switch
        {
            SystemLanguage.Turkish   => "tr",
            SystemLanguage.ChineseSimplified => "zh-cn",
            SystemLanguage.ChineseTraditional => "zh-tw",
            SystemLanguage.Chinese => "zh-cn",
            SystemLanguage.Spanish => "es",
            SystemLanguage.French => "fr",
            SystemLanguage.German => "de",
            SystemLanguage.Italian => "it",
            SystemLanguage.Russian => "ru",
            SystemLanguage.Polish => "pl",
            SystemLanguage.Ukrainian => "uk",
            SystemLanguage.Japanese => "ja",
            SystemLanguage.Korean => "ko",
            SystemLanguage.Dutch => "nl",
            SystemLanguage.Swedish => "sv",
            SystemLanguage.Czech => "cs",
            SystemLanguage.Portuguese => "pt-br",
            _ => _defaultLanguage
        };
    }
}
