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
        [Tooltip("Button with a TMP_Text child. One per wiki page. Used for entries that have " +
                 "an icon — in practice the turret roster.")]
        [SerializeField] private GameObject _pageButtonPrefab;

        [Tooltip("Button for pages with no icon (the mechanic pages). Same role as the one " +
                 "above but designed for centred text with no icon slot, so the label is not " +
                 "left with a permanent empty indent. Falls back to the icon variant when unset.")]
        [SerializeField] private GameObject _textPageButtonPrefab;

        [Header("Detail Panel")]
        [SerializeField] private Transform _detailContainer;
        [Tooltip("A single TMP_Text. Reused for headings, body copy and rows via rich text.")]
        [SerializeField] private GameObject _textItemPrefab;

        [Tooltip("Row with an 'Icon' Image and a 'Value' TMP_Text. Used for stats so a " +
                 "damage glyph replaces the word 'damage'. Falls back to a text row when unset.")]
        [SerializeField] private GameObject _statRowPrefab;

        [Tooltip("Horizontal container the stat rows are packed into, so damage / range / " +
                 "speed sit side by side rather than stacked. Unset, stats fall back to one " +
                 "per line.")]
        [SerializeField] private GameObject _statBarPrefab;

        [Tooltip("Stat bar whose first cell is the level. Needs a 'Level' TMP_Text and an " +
                 "'Entries' child. Used wherever a bar of stats belongs to a specific level " +
                 "or evolution tier; falls back to a separate heading line when unset.")]
        [SerializeField] private GameObject _statBarWithLevelPrefab;

        [Tooltip("Strip showing a turret's base and evolved forms. Needs an 'Entries' child " +
                 "to parent the items under, plus an item prefab below.")]
        [SerializeField] private GameObject _iconStripPrefab;

        [Tooltip("One form inside the strip: 'Icon' Image, 'Caption' and 'Name' TMP_Texts.")]
        [SerializeField] private GameObject _iconStripItemPrefab;
        [SerializeField] private ScrollRect _detailScrollRect;

        [Header("Close")]
        [SerializeField] private Button _closeButton;

        [Header("List Presentation")]
        [Tooltip("Optional Image on the page-button prefab, filled with the turret icon. " +
                 "Left unassigned by name lookup, entries fall back to text only.")]
        [SerializeField] private string _iconChildName = "Icon";
        [Tooltip("Own prefab so category rows can be designed freely without touching the " +
                 "page rows. Falls back to a styled page button when unset.")]
        [SerializeField] private GameObject _categoryHeaderPrefab;
        [SerializeField] private Color _lockedPageColor = new Color(0.45f, 0.45f, 0.5f);
        [SerializeField] private Color _categoryColor = new Color(1f, 0.78f, 0.34f);

        [Header("Detail Header")]
        [Tooltip("Large icon shown above the page body. Optional.")]
        [SerializeField] private Image _detailIcon;
        [SerializeField] private TMP_Text _detailTitle;

        [Header("Style")]
        [SerializeField] private Color _selectedPageColor = new Color(0.3f, 0.85f, 1f);
        [SerializeField] private Color _defaultPageColor = new Color(0.75f, 0.75f, 0.75f);
        [SerializeField] private Color _noteColor = new Color(0.65f, 0.65f, 0.65f);
        [SerializeField] private Color _valueColor = new Color(1f, 0.78f, 0.34f);
        [SerializeField] private Color _headingColor = new Color(0.55f, 0.85f, 1f);
        [Tooltip("Colour for sections flagged IsAlert — currently the unlock requirement on a " +
                 "locked turret, which has to stand out from the stats around it.")]
        [SerializeField] private Color _alertColor = new Color(1f, 0.35f, 0.35f);

        [Header("Typography")]
        [Tooltip("Longest line the body text is allowed to occupy. Wide panels otherwise " +
                 "produce very long lines that are hard to track back to the next line.")]
        [SerializeField] private float _maxLineWidth = 860f;
        [Tooltip("Blank space above each section heading (first heading excluded).")]
        [SerializeField] private float _sectionSpacing = 22f;
        [Tooltip("Left inset for rows. Matches the stat bar's own inner padding so a " +
                 "level heading lines up with the stat glyphs beneath it.")]
        [SerializeField] private float _rowIndent = 14f;
        [SerializeField] private float _noteIndent = 28f;

        private readonly List<IWikiPage> _pages = new();
        private readonly List<Button> _pageButtons = new();

        // Category headers are instantiated into the same container as the buttons, so the
        // button list is kept sparse: index i is null where row i is a header, keeping page
        // indices and row indices aligned without a second lookup table.
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

            string currentCategory = null;

            for (int i = 0; i < _pages.Count; i++)
            {
                string category = WikiCatalog.CategoryOf(_pages[i]);
                if (category != currentCategory)
                {
                    currentCategory = category;
                    AddCategoryHeader(category);
                }

                int index = i; // captured per iteration for the click handler
                var entry = _pages[i] as IWikiListEntry;

                // Chosen on whether the entry actually has an icon rather than on the page
                // type, so a future mechanic page that gains one still gets the icon layout
                // without this needing to know what kind of page it is.
                bool hasIcon = entry != null && entry.Icon != null;
                GameObject rowPrefab = !hasIcon && _textPageButtonPrefab != null
                    ? _textPageButtonPrefab
                    : _pageButtonPrefab;

                var go = Instantiate(rowPrefab, _pageListContainer);
                go.SetActive(true);

                var label = go.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    // A padlock rather than hiding the entry: the roster is only useful for
                    // discovery if turrets you have not unlocked are still visible.
                    bool locked = entry != null && entry.IsLocked;
                    label.text = locked ? "<size=90%>🔒</size> " + _pages[i].Title : _pages[i].Title;
                }

                var icon = FindIconImage(go);
                if (icon != null)
                {
                    icon.sprite = entry != null ? entry.Icon : null;
                    icon.enabled = icon.sprite != null;
                    icon.color = entry != null && entry.IsLocked
                        ? new Color(1f, 1f, 1f, 0.35f)
                        : Color.white;
                }

                var button = go.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => ShowPage(index));
                }
                _pageButtons.Add(button);
            }
        }

        /// <summary>
        /// A non-interactive row that separates TURRETS from MECHANICS. Built from the same
        /// prefab so it inherits the list's styling, then stripped of its button.
        /// </summary>
        private void AddCategoryHeader(string title)
        {
            // Its own prefab when one is assigned, so the header can be designed
            // independently of the page rows.
            GameObject source = _categoryHeaderPrefab != null ? _categoryHeaderPrefab : _pageButtonPrefab;
            var go = Instantiate(source, _pageListContainer);
            go.SetActive(true);
            go.name = "Category_" + title;

            var button = go.GetComponent<Button>();
            if (button != null) button.interactable = false;

            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = title;

            // Only style the fallback. A dedicated prefab is assumed to be already styled.
            if (_categoryHeaderPrefab == null)
            {
                var icon = FindIconImage(go);
                if (icon != null) icon.enabled = false;
                if (label != null)
                {
                    label.text = $"<size=88%><b>{title}</b></size>";
                    label.color = _categoryColor;
                }
            }
        }

        private Image FindIconImage(GameObject row)
        {
            if (string.IsNullOrWhiteSpace(_iconChildName)) return null;
            Transform t = row.transform.Find(_iconChildName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private void ShowPage(int index)
        {
            if (_pages.Count == 0 || _detailContainer == null || _textItemPrefab == null)
                return;

            index = Mathf.Clamp(index, 0, _pages.Count - 1);
            _selectedIndex = index;

            for (int i = 0; i < _pageButtons.Count && i < _pages.Count; i++)
            {
                var text = _pageButtons[i] != null
                    ? _pageButtons[i].GetComponentInChildren<TMP_Text>(true)
                    : null;
                if (text == null) continue;

                bool locked = _pages[i] is IWikiListEntry e && e.IsLocked;
                text.color = i == index
                    ? _selectedPageColor
                    : (locked ? _lockedPageColor : _defaultPageColor);
            }

            UpdateDetailHeader(_pages[index]);

            ClearChildren(_detailContainer);

            // Live modifiers exist only during a run; null in the Hub, where pages
            // fall back to base values.
            var mods = WikiCatalog.TryGetLiveModifiers();
            var sections = _pages[index].BuildSections(mods);

            string valueHex = ColorUtility.ToHtmlStringRGB(_valueColor);
            string noteHex = ColorUtility.ToHtmlStringRGB(_noteColor);
            string headingHex = ColorUtility.ToHtmlStringRGB(_headingColor);
            string categoryHex = ColorUtility.ToHtmlStringRGB(_categoryColor);

            // Long lines are the main readability cost on a wide panel, so the text
            // column is capped and the surplus width becomes right margin.
            float available = _detailScrollRect != null && _detailScrollRect.viewport != null
                ? _detailScrollRect.viewport.rect.width
                : 0f;
            float rightMargin = available > _maxLineWidth ? available - _maxLineWidth : 0f;

            string alertHex = ColorUtility.ToHtmlStringRGB(_alertColor);

            bool firstHeading = true;
            foreach (var section in sections)
            {
                // An alert section is drawn entirely in the alert colour — heading, prose and
                // rows alike. Tinting only the heading reads as decoration; tinting the block
                // reads as a gate, which is what a locked turret's requirement is.
                string sectionHeadingHex = section.IsAlert ? alertHex : headingHex;
                string sectionValueHex = section.IsAlert ? alertHex : valueHex;

                if (!string.IsNullOrWhiteSpace(section.Heading))
                {
                    AddText($"<size=140%><b><color=#{sectionHeadingHex}>{section.Heading}</color></b></size>",
                            topMargin: firstHeading ? 0f : _sectionSpacing,
                            rightMargin: rightMargin);
                    firstHeading = false;
                }

                if (!string.IsNullOrWhiteSpace(section.Body))
                {
                    AddText(section.IsAlert
                                ? $"<color=#{alertHex}>{section.Body}</color>"
                                : section.Body,
                            topMargin: 2f, rightMargin: rightMargin);
                }

                if (section.Icons.Count > 0)
                    AddIconStrip(section.Icons);

                // Consecutive stat rows share one horizontal bar. Grouping only runs
                // while the rows are adjacent, so a stat row that appears after prose
                // still starts a fresh bar instead of being pulled out of order.
                Transform statBar = null;
                string pendingLevel = null;

                foreach (var row in section.Rows)
                {
                    bool isStat = row.Icon != null && _statRowPrefab != null;

                    if (isStat)
                    {
                        // The level is carried into the bar as its first cell rather than
                        // being drawn as a line above it.
                        if (statBar == null)
                        {
                            statBar = AddStatBar(pendingLevel);
                            pendingLevel = null;
                        }
                        AddStatRow(row, statBar);
                    }
                    else if (row.IsSubheading)
                    {
                        statBar = null;
                        FlushPendingLevel(ref pendingLevel, categoryHex, rightMargin);
                        pendingLevel = row.Label;
                    }
                    else
                    {
                        statBar = null;
                        FlushPendingLevel(ref pendingLevel, categoryHex, rightMargin);
                        string label = section.IsAlert
                            ? $"<color=#{alertHex}>{row.Label}</color>"
                            : row.Label;
                        AddText($"<b>{label}</b>   <color=#{sectionValueHex}>{row.Value}</color>",
                                topMargin: 4f, leftMargin: _rowIndent, rightMargin: rightMargin);
                    }

                    if (!string.IsNullOrWhiteSpace(row.Note))
                    {
                        // A note is its own full-width line, so it also ends the run.
                        statBar = null;
                        AddText($"<size=88%><color=#{(section.IsAlert ? alertHex : noteHex)}>{row.Note}</color></size>",
                                leftMargin: _noteIndent, rightMargin: rightMargin);
                    }
                }

                // A heading with no stats after it still has to appear.
                FlushPendingLevel(ref pendingLevel, categoryHex, rightMargin);
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

        /// <summary>
        /// Container the stat rows are laid into. When a level is supplied the bar variant
        /// carrying a level cell is used, so the level reads as the first stat. Falls back
        /// to the detail column itself when no bar prefab is assigned, which restores the
        /// old one-per-line behaviour.
        /// </summary>
        private Transform AddStatBar(string levelLabel = null)
        {
            bool withLevel = !string.IsNullOrEmpty(levelLabel) && _statBarWithLevelPrefab != null;
            GameObject source = withLevel ? _statBarWithLevelPrefab : _statBarPrefab;

            if (source == null)
                return _detailContainer;

            var bar = Instantiate(source, _detailContainer);
            bar.SetActive(true);

            if (withLevel)
            {
                var levelT = bar.transform.Find("Level");
                var level = levelT != null ? levelT.GetComponent<TMP_Text>() : null;
                if (level != null) level.text = levelLabel;
            }

            return bar.transform.Find("Entries") ?? bar.transform;
        }

        /// <summary>
        /// Draws a level heading that never found stats to lead — without this a section
        /// ending on a heading would silently drop it.
        /// </summary>
        private void FlushPendingLevel(ref string pendingLevel, string categoryHex, float rightMargin)
        {
            if (string.IsNullOrEmpty(pendingLevel)) return;

            AddText($"<size=112%><b><color=#{categoryHex}>{pendingLevel}</color></b></size>",
                    topMargin: _sectionSpacing * 0.5f, leftMargin: _rowIndent, rightMargin: rightMargin);
            pendingLevel = null;
        }

        /// <summary>Stat glyph plus its value, replacing a written stat name.</summary>
        private void AddStatRow(WikiRow row, Transform parent)
        {
            var go = Instantiate(_statRowPrefab, parent != null ? parent : _detailContainer);
            go.SetActive(true);

            var iconT = go.transform.Find("Icon");
            var icon = iconT != null ? iconT.GetComponent<Image>() : null;
            if (icon != null)
            {
                icon.sprite = row.Icon;
                icon.color = row.IconColor;
                icon.enabled = row.Icon != null;
            }

            var valueT = go.transform.Find("Value");
            var value = valueT != null ? valueT.GetComponent<TMP_Text>() : go.GetComponentInChildren<TMP_Text>(true);
            if (value != null) value.text = row.Value;
        }

        /// <summary>A turret's base and evolved forms side by side.</summary>
        private void AddIconStrip(IReadOnlyList<WikiIconEntry> entries)
        {
            if (_iconStripPrefab == null || _iconStripItemPrefab == null)
                return;

            var strip = Instantiate(_iconStripPrefab, _detailContainer);
            strip.SetActive(true);

            Transform host = strip.transform.Find("Entries") ?? strip.transform;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Icon == null) continue;

                // Fixed slot, not append order: the strip is a diamond whose positions mean
                // base / A / B / C, so a turret missing one form must leave that corner empty
                // rather than sliding the rest up and relabelling them.
                Transform slot = host.Find("Slot" + entries[i].Slot);
                if (slot == null) continue;

                var item = Instantiate(_iconStripItemPrefab, slot);
                item.SetActive(true);

                var itemRt = item.transform as RectTransform;
                if (itemRt != null)
                {
                    itemRt.anchorMin = Vector2.zero;
                    itemRt.anchorMax = Vector2.one;
                    itemRt.offsetMin = Vector2.zero;
                    itemRt.offsetMax = Vector2.zero;
                }

                var iconT = item.transform.Find("Icon");
                var icon = iconT != null ? iconT.GetComponent<Image>() : null;
                if (icon != null) { icon.sprite = entries[i].Icon; icon.enabled = true; }

                var capT = item.transform.Find("Caption");
                var cap = capT != null ? capT.GetComponent<TMP_Text>() : null;
                if (cap != null) cap.text = entries[i].Caption;

                // Optional second label: prefabs that only want the form name on top can
                // simply omit it.
                var nameT = item.transform.Find("Name");
                var nm = nameT != null ? nameT.GetComponent<TMP_Text>() : null;
                if (nm != null) nm.text = entries[i].Name;
            }
        }

        /// <summary>Large icon and title above the body, so the page reads as being about
        /// a specific turret rather than as an anonymous wall of text.</summary>
        private void UpdateDetailHeader(IWikiPage page)
        {
            if (_detailTitle != null)
                _detailTitle.text = page.Title;

            if (_detailIcon != null)
            {
                Sprite sprite = page is IWikiListEntry entry ? entry.Icon : null;
                _detailIcon.sprite = sprite;
                _detailIcon.enabled = sprite != null;
            }
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }
    }
}
