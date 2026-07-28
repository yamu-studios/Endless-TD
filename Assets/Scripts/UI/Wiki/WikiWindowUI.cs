// ============================================================================
// ETD.UI.Wiki - WikiWindowUI.cs  [NEW]
// Renders the wiki. Mirrors TurretsWindowUI's shape: a page list on the left,
// an always-open detail panel on the right, rebuilt on language change.
//
// The window knows nothing about individual pages — it renders whatever
// WikiCatalog hands it, so adding a page never touches this file.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Data;

namespace ETD.UI.Wiki
{
    public class WikiWindowUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameDatabase _database;

        [Header("Page List")]
        [SerializeField] private Transform _pageListContainer;
        [Tooltip("Button with a TMP_Text child. One per wiki page.")]
        [SerializeField] private GameObject _pageButtonPrefab;

        [Header("Detail Panel")]
        [SerializeField] private Transform _detailContainer;
        [Tooltip("A single TMP_Text. Reused for headings, body copy and rows via rich text.")]
        [SerializeField] private GameObject _textItemPrefab;
        [SerializeField] private ScrollRect _detailScrollRect;

        [Header("Close")]
        [SerializeField] private Button _closeButton;

        [Header("Style")]
        [SerializeField] private Color _selectedPageColor = new Color(0.3f, 0.85f, 1f);
        [SerializeField] private Color _defaultPageColor = new Color(0.75f, 0.75f, 0.75f);
        [SerializeField] private Color _noteColor = new Color(0.65f, 0.65f, 0.65f);
        [SerializeField] private Color _valueColor = new Color(1f, 0.78f, 0.34f);
        [SerializeField] private Color _headingColor = new Color(0.55f, 0.85f, 1f);

        [Header("Typography")]
        [Tooltip("Longest line the body text is allowed to occupy. Wide panels otherwise " +
                 "produce very long lines that are hard to track back to the next line.")]
        [SerializeField] private float _maxLineWidth = 860f;
        [Tooltip("Blank space above each section heading (first heading excluded).")]
        [SerializeField] private float _sectionSpacing = 22f;
        [SerializeField] private float _rowIndent = 14f;
        [SerializeField] private float _noteIndent = 28f;

        private readonly List<IWikiPage> _pages = new();
        private readonly List<Button> _pageButtons = new();
        private int _selectedIndex = -1;

        private void Awake()
        {
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
            EventBus.Subscribe<CloseWikiRequestEvent>(OnCloseRequested);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
            EventBus.Unsubscribe<CloseWikiRequestEvent>(OnCloseRequested);
        }

        // Escape in-run routes here rather than through the pause menu, because
        // input lives in an assembly that cannot reference the UI one.
        private void OnCloseRequested(CloseWikiRequestEvent evt)
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _closeButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.AddListener(() => gameObject.SetActive(false));
            Rebuild();

            // Announced here rather than by the opener so every path is covered —
            // the close button, the pause menu, or a parent being deactivated.
            // In-run this tells GameInputHandler that Escape should close the wiki
            // instead of unpausing. Harmless in the Hub, which has no input handler.
            EventBus.Publish(new WikiToggleEvent { IsActive = true });
        }

        private void OnDisable()
        {
            EventBus.Publish(new WikiToggleEvent { IsActive = false });
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            // Page titles and all prose are localized, so the whole window is rebuilt.
            if (gameObject.activeSelf) Rebuild();
        }

        private void Rebuild()
        {
            BuildPageList();
            // Keep the reader where they were across a language switch.
            ShowPage(_selectedIndex >= 0 && _selectedIndex < _pages.Count ? _selectedIndex : 0);
        }

        private void BuildPageList()
        {
            _pages.Clear();
            _pageButtons.Clear();
            ClearChildren(_pageListContainer);

            if (_database == null || _pageButtonPrefab == null || _pageListContainer == null)
                return;

            _pages.AddRange(WikiCatalog.BuildPages(_database));

            for (int i = 0; i < _pages.Count; i++)
            {
                int index = i; // captured per iteration for the click handler
                var go = Instantiate(_pageButtonPrefab, _pageListContainer);
                go.SetActive(true);

                var label = go.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = _pages[i].Title;

                var button = go.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => ShowPage(index));
                }
                _pageButtons.Add(button);
            }
        }

        private void ShowPage(int index)
        {
            if (_pages.Count == 0 || _detailContainer == null || _textItemPrefab == null)
                return;

            index = Mathf.Clamp(index, 0, _pages.Count - 1);
            _selectedIndex = index;

            for (int i = 0; i < _pageButtons.Count; i++)
            {
                var text = _pageButtons[i] != null
                    ? _pageButtons[i].GetComponentInChildren<TMP_Text>(true)
                    : null;
                if (text != null) text.color = i == index ? _selectedPageColor : _defaultPageColor;
            }

            ClearChildren(_detailContainer);

            // Live modifiers exist only during a run; null in the Hub, where pages
            // fall back to base values.
            var mods = WikiCatalog.TryGetLiveModifiers();
            var sections = _pages[index].BuildSections(mods);

            string valueHex = ColorUtility.ToHtmlStringRGB(_valueColor);
            string noteHex = ColorUtility.ToHtmlStringRGB(_noteColor);
            string headingHex = ColorUtility.ToHtmlStringRGB(_headingColor);

            // Long lines are the main readability cost on a wide panel, so the text
            // column is capped and the surplus width becomes right margin.
            float available = _detailScrollRect != null && _detailScrollRect.viewport != null
                ? _detailScrollRect.viewport.rect.width
                : 0f;
            float rightMargin = available > _maxLineWidth ? available - _maxLineWidth : 0f;

            bool firstHeading = true;
            foreach (var section in sections)
            {
                if (!string.IsNullOrWhiteSpace(section.Heading))
                {
                    AddText($"<size=140%><b><color=#{headingHex}>{section.Heading}</color></b></size>",
                            topMargin: firstHeading ? 0f : _sectionSpacing,
                            rightMargin: rightMargin);
                    firstHeading = false;
                }

                if (!string.IsNullOrWhiteSpace(section.Body))
                    AddText(section.Body, topMargin: 2f, rightMargin: rightMargin);

                foreach (var row in section.Rows)
                {
                    AddText($"<b>{row.Label}</b>   <color=#{valueHex}>{row.Value}</color>",
                            topMargin: 4f, leftMargin: _rowIndent, rightMargin: rightMargin);
                    if (!string.IsNullOrWhiteSpace(row.Note))
                        AddText($"<size=88%><color=#{noteHex}>{row.Note}</color></size>",
                                leftMargin: _noteIndent, rightMargin: rightMargin);
                }
            }

            if (_detailScrollRect != null)
                _detailScrollRect.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// TMP's margin (left, top, right, bottom) carries indentation and section
        /// spacing, so no spacer objects are needed and the layout group stays simple.
        /// </summary>
        private void AddText(string content, float topMargin = 0f, float leftMargin = 0f,
                             float rightMargin = 0f, float bottomMargin = 0f)
        {
            var go = Instantiate(_textItemPrefab, _detailContainer);
            go.SetActive(true);
            var text = go.GetComponent<TMP_Text>() ?? go.GetComponentInChildren<TMP_Text>(true);
            if (text == null) return;

            text.text = content;
            text.margin = new Vector4(leftMargin, topMargin, rightMargin, bottomMargin);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }
    }
}
