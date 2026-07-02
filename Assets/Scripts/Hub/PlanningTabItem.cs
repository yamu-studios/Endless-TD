// ============================================================================
// ETD.Hub - PlanningTabItem.cs  [REWRITTEN]
// Layout: Icon | Name | Grade Text (grade-colored)
// Checkmark button (top-left or integrated):
//   - Click to select: button color → assigned color, enable child checkmark image
//   - Click again to deselect: button color → default, disable child checkmark image
//   - Border: selected = assigned color, default = defaultBorderColor
// Info click: clicking body (not checkmark) shows info
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using ETD.Data;
using ETD.Core;

namespace ETD.Hub
{
    public class PlanningTabItem : MonoBehaviour, IPointerClickHandler
    {
        [Header("Visuals")]
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _gradeText;
        [SerializeField] private Image _border;

        [Header("Checkmark Button")]
        [SerializeField] private Button _checkmarkButton;
        [SerializeField] private Image _checkmarkButtonImage;   // the button background
        [SerializeField] private Image _checkmarkChildImage;    // the tick inside it (disabled when not selected)

        [Header("Lock")]
        [SerializeField] private GameObject _lockOverlay; // enabled when trait is locked

        [Header("Colors")]
        [SerializeField] private Color _defaultBorderColor = new Color(0.4f, 0.4f, 0.4f);
        [SerializeField] private Color _defaultButtonColor = new Color(0.25f, 0.25f, 0.25f);
        [SerializeField] private Color _selectedBorderColor = new Color(0.3f, 0.85f, 0.3f); // set per-item in Setup
        [SerializeField] private Color _selectedButtonColor = new Color(0.3f, 0.85f, 0.3f);

        [SerializeField] private HubBadge _badge;

        public string ItemId { get; private set; }
        public bool IsSelected { get; private set; }

        private bool _isUnlocked;
        private System.Action<string, bool> _onToggled;
        private System.Action<string,PlanningTabItem> _onInfoClicked;
        private Color _assignedColor;

        private bool _suppressToggle;

        public Transform CheckmarkButtonTransform
           => _checkmarkButton != null ? _checkmarkButton.transform : transform;
        public bool IsUnlocked => _isUnlocked;

        public System.Action<string> OnSelectedCallback;

        public void Setup(string id, Sprite icon, string displayName,
            SpecCardRarity grade, bool isUnlocked, bool isSelected,
            Color assignedColor,
            System.Action<string, bool> onToggled,
            System.Action<string,PlanningTabItem> onInfoClicked,TraitData trait = null)
        {
            ItemId = id;
            _isUnlocked = isUnlocked;
            _onToggled = onToggled;
            _onInfoClicked = onInfoClicked;
            _assignedColor = assignedColor;
            IsSelected = isSelected;

            // Lock overlay
            if (_lockOverlay != null) _lockOverlay.SetActive(!isUnlocked);

            // Icon
            if (_icon != null) _icon.sprite = icon;

           
            // Name
            if (_nameText != null) _nameText.text = SOLocalization.GetName("trait_" + trait.LocalizationKey, trait.DisplayName);

            // Grade text — colored by grade
            if (_gradeText != null)
            {
                _gradeText.text = RarityColorHelper.GetLocalizedName(grade);
                _gradeText.color = HubController.Instance.GetColor(grade);//grade.GetRarityColor();
            }

            // Checkmark button listener
            if (_checkmarkButton != null)
            {
                _checkmarkButton.onClick.RemoveAllListeners();
                _checkmarkButton.onClick.AddListener(OnCheckmarkClicked);
                _checkmarkButton.interactable = isUnlocked;
            }

            // Apply visual state
            ApplySelectionVisuals(isSelected);
            _badge?.SetVisible(HubBadgeRegistry.IsNew(HubBadgeType.Trait, id));
        }

        // =================================================================
        // CHECKMARK BUTTON
        // =================================================================

        private void OnCheckmarkClicked()
        {
            if (!_isUnlocked) return;

            bool newState = !IsSelected;
            IsSelected = newState;
            ApplySelectionVisuals(newState);
            _onToggled?.Invoke(ItemId, newState);

            if (newState)
                OnSelectedCallback?.Invoke(ItemId);

           
        }

        // =================================================================
        // ITEM BODY — show info only (IPointerClickHandler)
        // =================================================================

        public void OnPointerClick(PointerEventData eventData)
        {
            // Ignore if click landed on the checkmark button
            if (_checkmarkButton != null)
            {
                var rt = _checkmarkButton.GetComponent<RectTransform>();
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(
                        rt, eventData.position, eventData.pressEventCamera))
                    return;
            }

            if (_border != null)
                _border.color = _selectedBorderColor;

            HubBadgeRegistry.MarkViewed(HubBadgeType.Trait, ItemId);
            _badge?.SetVisible(false);
            HubController.Instance?.RefreshButtonBadges();

            _onInfoClicked?.Invoke(ItemId,this);
        }

        // =================================================================
        // VISUAL STATE
        // =================================================================

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            ApplySelectionVisuals(selected);
        }

        public void Deselect()
        {
            if (_border != null)
                _border.color = IsSelected ? _assignedColor : _defaultBorderColor;
        }
        private void ApplySelectionVisuals(bool selected)
        {
            // Border color
            if (_border != null)
                _border.color = selected ? _assignedColor : _defaultBorderColor;

            // Checkmark button background
            if (_checkmarkButtonImage != null)
                _checkmarkButtonImage.color = selected ? _selectedButtonColor : _defaultButtonColor;

            // Child checkmark image inside button
            if (_checkmarkChildImage != null)
                _checkmarkChildImage.enabled = selected;
        }
    }
}