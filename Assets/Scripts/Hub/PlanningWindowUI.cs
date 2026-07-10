// ============================================================================
// ETD.Hub - PlanningWindowUI.cs  [REWRITTEN]
//
// Tab items: Icon + Name + Grade (grade-colored text)
// Checkmark button: select/deselect with color changes, child image toggle
// Border: selected item border = assigned color, others = default
//
// Info panel: always visible, auto-shows first item if nothing clicked
//   Icon | Name | Description or Unlock Condition | Grade text + border (grade-colored)
//
// Selected traits section: list of active traits, each with deselect button
// Stack overflow: selecting when full auto-deselects oldest (FIFO)
//
// Close button exists.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.UI;
using ETD.Data;
using ETD.Meta;

namespace ETD.Hub
{
    public class PlanningWindowUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameDatabase _database;

        [Header("Trait List")]
        [SerializeField] private Transform _traitListContainer;
        [SerializeField] private GameObject _tabItemPrefab;
        [SerializeField] private TMP_Text _traitSlotsText;

        [Header("Colors")]
        [SerializeField] private Color _selectedColor      = new Color(0.3f, 0.85f, 0.3f);
        [SerializeField] private Color _defaultBorderColor = new Color(0.35f, 0.35f, 0.35f);

        [Header("Info Panel (always visible)")]
        [SerializeField] private Image    _infoIcon;
        [SerializeField] private TMP_Text _infoName;
        [SerializeField] private TMP_Text _infoDescription;
        [SerializeField] private TMP_Text _infoGradeText;
        [SerializeField] private Image    _infoGradeBorder;

        [Header("Selected Traits Section")]
        [SerializeField] private Transform _selectedTraitsContainer;
        [SerializeField] private GameObject _selectedTraitItemPrefab;

        [Header("Close")]
        [SerializeField] private Button _closeButton;

        // Runtime
        private MetaProgressionManager _metaManager;
        private readonly List<string>  _selectedTraitIds = new();
        private int _maxTraitSlots;

        private readonly List<PlanningTabItem>  _tabItems       = new();
        private readonly List<SelectedTraitItem> _selectedItems  = new();
        private string _currentInfoTraitId;
        private PlanningTabItem _currentTraitItem;

        public IReadOnlyList<PlanningTabItem> TabItems => _tabItems;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void OnEnable()
        {
            _metaManager = ServiceLocator.Get<MetaProgressionManager>();
            var save = SaveSystem.Load();
            _maxTraitSlots = save.TraitSlotCount;

            // Restore saved selections
            _selectedTraitIds.Clear();
            if (save.SelectedTraitIds != null)
                foreach (var id in save.SelectedTraitIds)
                    if (!string.IsNullOrEmpty(id) && _database.GetTrait(id) != null)
                        _selectedTraitIds.Add(id);

            _closeButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.AddListener(() => gameObject.SetActive(false));

            GenerateTraitList();
            RefreshSelectedSection();
            UpdateSlotsText();

            // Auto-show first trait in info panel
            if (_tabItems.Count > 0)
                ShowInfo(_tabItems[0].ItemId, _tabItems[0]);
        }

        private void Awake()
        {
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            if (gameObject.activeSelf) OnEnable(); // regenerate list with new language
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        // =================================================================
        // GENERATE TRAIT LIST
        // =================================================================

        private void GenerateTraitList()
        {
            ClearList(_tabItems, _traitListContainer);
            if (_database?.Traits == null) return;

            foreach (var trait in _database.Traits)
            {
                var go   = Instantiate(_tabItemPrefab, _traitListContainer);
                var item = go.GetComponent<PlanningTabItem>();
                if (item == null) item = go.AddComponent<PlanningTabItem>();

                bool unlocked = _metaManager == null || _metaManager.IsTraitUnlocked(trait.Id);
                bool selected = _selectedTraitIds.Contains(trait.Id);

                item.Setup(
                    id:           trait.Id,
                    icon:         trait.Icon,
                    displayName:  trait.DisplayName,
                    grade:        trait.Grade,
                    isUnlocked:   unlocked,
                    isSelected:   selected,
                    assignedColor: _selectedColor,
                    onToggled:    OnTraitToggled,
                    onInfoClicked: ShowInfo,
                    trait
                );

                _tabItems.Add(item);
            }
        }

        // =================================================================
        // TOGGLE SELECTION
        // =================================================================

        private void OnTraitToggled(string traitId, bool wantsSelected)
        {
            if (wantsSelected)
            {
                if (_selectedTraitIds.Contains(traitId)) return;
                if (_metaManager != null && !_metaManager.IsTraitUnlocked(traitId)) return;

                // Keystone Covenants are mutually exclusive: selecting one deselects
                // any other selected covenant.
                var newTrait = _database != null ? _database.GetTrait(traitId) : null;
                if (newTrait != null && newTrait.IsKeystone)
                {
                    for (int i = _selectedTraitIds.Count - 1; i >= 0; i--)
                    {
                        var existing = _database.GetTrait(_selectedTraitIds[i]);
                        if (existing != null && existing.IsKeystone)
                        {
                            FindTabItem(_selectedTraitIds[i])?.SetSelected(false);
                            _selectedTraitIds.RemoveAt(i);
                        }
                    }
                }

                // Stack overflow: remove oldest if at capacity
                while (_selectedTraitIds.Count >= _maxTraitSlots && _selectedTraitIds.Count > 0)
                {
                    string oldest = _selectedTraitIds[0];
                    _selectedTraitIds.RemoveAt(0);
                    FindTabItem(oldest)?.SetSelected(false);
                }

                _selectedTraitIds.Add(traitId);
            }
            else
            {
                _selectedTraitIds.Remove(traitId);
            }

            SaveSystem.SavePlanningSelections(_selectedTraitIds.ToArray());
            RefreshSelectedSection();
            UpdateSlotsText();

            //// Refresh info panel if showing this trait
            //if (_currentInfoTraitId == traitId)
            //    ShowInfo(traitId);
        }

        // =================================================================
        // INFO PANEL
        // =================================================================

        private void ShowInfo(string traitId,PlanningTabItem traitItem)
        {
            if (_currentTraitItem != null)
                _currentTraitItem.Deselect();

            _currentTraitItem = traitItem;
            _currentInfoTraitId = traitId;
            var trait = _database.GetTrait(traitId);
            if (trait == null) return;

            bool unlocked = _metaManager == null || _metaManager.IsTraitUnlocked(traitId);

            if (_infoIcon != null)
            {
                _infoIcon.sprite  = trait.Icon;
                _infoIcon.enabled = trait.Icon != null;
            }

            if (_infoName != null)
                _infoName.text = SOLocalization.GetName("trait_"+trait.LocalizationKey, trait.DisplayName);

            if (_infoDescription != null)
            {
                if (unlocked)
                {
                    _infoDescription.text = BalanceDescriptionFormatter.AppendTraitNumbers(trait, SOLocalization.GetDesc("trait_" + trait.LocalizationKey, trait.Description));
                    
                   
                }
                    
                else
                {
                    string descText = BalanceDescriptionFormatter.AppendTraitNumbers(trait, SOLocalization.GetDesc("trait_" + trait.LocalizationKey, trait.Description));
                    string unlockText = SOLocalization.GetUnlock("trait_" + trait.LocalizationKey, trait.UnlockCondition);
                    _infoDescription.text = descText +"\n" + "<color=#FF5A5F>"+BuildUnlockConditionText(trait, unlockText)+"</color>";
                }
            }

           // if (_infoName != null) _infoName.text = trait.DisplayName;

            //if (_infoDescription != null)
            //    _infoDescription.text = unlocked
            //        ? trait.Description
            //        : $"Unlock: {trait.UnlockCondition}";

            //if (_infoDescription != null)
            //{
            //    if (unlocked)
            //    {
            //        _infoDescription.text = trait.Description;
            //    }
            //    else
            //    {
            //        // Build unlock condition text with inline progress
            //        string conditionText = BuildUnlockConditionText(trait);
            //        _infoDescription.text = conditionText;
            //    }
            //}


            // Grade text + border — both colored by grade
            Color gradeColor = HubController.Instance.GetColor(trait.Grade);//trait.Grade.GetRarityColor();

            if (_infoGradeText != null)
            {
                _infoGradeText.text  = RarityColorHelper.GetLocalizedName(trait.Grade);
                _infoGradeText.color = gradeColor;
            }

            if (_infoGradeBorder != null)
                _infoGradeBorder.color = gradeColor;
        }

        // =================================================================
        // SELECTED TRAITS SECTION
        // =================================================================

        private void RefreshSelectedSection()
        {
            ClearList(_selectedItems, _selectedTraitsContainer);

            foreach (var id in _selectedTraitIds)
            {
                var trait = _database.GetTrait(id);
                if (trait == null) continue;

                var go   = Instantiate(_selectedTraitItemPrefab, _selectedTraitsContainer);
                var item = go.GetComponent<SelectedTraitItem>();
                if (item == null) item = go.AddComponent<SelectedTraitItem>();

                item.Setup(id, trait.Icon, trait.DisplayName, trait.Grade, OnDeselectFromSection,trait);
                _selectedItems.Add(item);
            }
        }

        private void OnDeselectFromSection(string traitId)
        {
            _selectedTraitIds.Remove(traitId);
            FindTabItem(traitId)?.SetSelected(false);
            SaveSystem.SavePlanningSelections(_selectedTraitIds.ToArray());
            RefreshSelectedSection();
            UpdateSlotsText();
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private string BuildUnlockConditionText(TraitData trait,string text)
        {
            string baseText = text;

            // No progress tracking set up — just show raw condition
            if (trait.UnlockConditionTarget <= 0)
            {
                Debug.Log("No trakicg set up");
                return $"{baseText}";
            }
              

            // Read current progress from save
            var save = SaveSystem.Load();
            float current = SaveSystem.GetChallengeProgress(save, (int)trait.UnlockConditionType);
            current = Mathf.Min(current, trait.UnlockConditionTarget);

            // Format: insert (currentProgress) after the target number in the string
            // Strategy: find the target number in the string and append progress after it
            string targetStr = FormatProgressNum(trait.UnlockConditionTarget);
            int idx = baseText.IndexOf(targetStr, System.StringComparison.OrdinalIgnoreCase);

            if (idx >= 0)
            {
                // Insert "(current)" right after the target number
                string progressStr = $"({FormatProgressNum(current)})";
                string result = baseText.Substring(0, idx + targetStr.Length)
                              + progressStr
                              + baseText.Substring(idx + targetStr.Length);
                return $"{result}";
            }

            // Fallback: append progress at end
            return $"{baseText} ({FormatProgressNum(current)}/{targetStr})";
        }

        private static string FormatProgressNum(float n)
        {
            if (n >= 1_000_000) return $"{n / 1_000_000f:F1}M";
            if (n >= 1_000) return $"{n / 1_000f:F1}K";
            return $"{n:F0}";
        }
        private void UpdateSlotsText()
        {
            if (_traitSlotsText != null)
                _traitSlotsText.text = $"{_selectedTraitIds.Count} / {_maxTraitSlots}";
        }

        private PlanningTabItem FindTabItem(string id)
            => _tabItems.Find(t => t.ItemId == id);

        private void ClearList<T>(List<T> items, Transform container)
            where T : MonoBehaviour
        {
            for (int i = items.Count - 1; i >= 0; i--)
                if (items[i] != null) Destroy(items[i].gameObject);
            items.Clear();
            if (container != null)
                for (int i = container.childCount - 1; i >= 0; i--)
                    Destroy(container.GetChild(i).gameObject);
        }

        public IReadOnlyList<string> SelectedTraitIds => _selectedTraitIds;
    }
}
