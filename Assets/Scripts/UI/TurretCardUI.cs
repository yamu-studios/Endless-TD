using ETD.Core;
using ETD.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ETD.UI
{
    public class TurretCardUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private Button _button;
        [SerializeField] private Image _canvasGroup;
        [SerializeField] private Color cannotAffordColor;

        [Header("Tooltip")]
        [Tooltip("Optional. Left empty, a TooltipTrigger is added at runtime, so the card " +
                 "prefab needs no extra wiring.")]
        [SerializeField] private TooltipTrigger _tooltipTrigger;

        public TurretData Data { get; private set; }
        private System.Action<TurretData> _onClick;

        // Whether the last built tooltip said "affordable", so SetAffordability (called on
        // every gold change) only rebuilds the content when the answer actually flipped.
        private bool _tooltipCanAfford = true;
        private bool _tooltipBuilt;

        public void Initialize(TurretData data, System.Action<TurretData> onClick)
        {
            Data = data;
            _onClick = onClick;

            ApplyLocalizedText();

            if (_icon != null) _icon.sprite = data.Icon;
            if (_costText != null) _costText.text = data.Cost.ToString();

            if (_button == null) _button = GetComponent<Button>();
            _button?.onClick.AddListener(() => _onClick?.Invoke(Data));

            EnsureTooltipTrigger();
            RefreshTooltip(_tooltipCanAfford, force: true);

            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void ApplyLocalizedText()
        {
            if (_nameText != null && Data != null)
                _nameText.text = SOLocalization.GetName("turret_" + Data.LocalizationKey, Data.DisplayName);
        }

        // The language can be switched from the pause menu mid-run, which would otherwise
        // leave both the card label and its cached tooltip in the old language.
        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            ApplyLocalizedText();
            RefreshTooltip(_tooltipCanAfford, force: true);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        /// <summary>
        /// The trigger sits on the card root, not the button: a card the player cannot
        /// afford has a non-interactable button, and the tooltip explaining why is exactly
        /// what is needed then.
        /// </summary>
        private void EnsureTooltipTrigger()
        {
            if (_tooltipTrigger != null) return;

            _tooltipTrigger = GetComponent<TooltipTrigger>();
            if (_tooltipTrigger == null)
                _tooltipTrigger = gameObject.AddComponent<TooltipTrigger>();
        }

        private void RefreshTooltip(bool canAfford, bool force = false)
        {
            if (_tooltipTrigger == null || Data == null) return;
            if (!force && _tooltipBuilt && _tooltipCanAfford == canAfford) return;

            _tooltipCanAfford = canAfford;
            _tooltipBuilt = true;
            _tooltipTrigger.SetContent(TooltipContentBuilder.FromTurretData(Data, canAfford));
            // Gold can change while the card is hovered, so push the new text immediately.
            _tooltipTrigger.RefreshIfShowing();
        }

        public void SetAffordable(bool canAfford)
        {
            RefreshTooltip(canAfford);

            if (_canvasGroup != null && !canAfford)
            {
                _canvasGroup.enabled = true;
                _canvasGroup.color = cannotAffordColor;
            }else if(_canvasGroup != null && canAfford)
            {
                _canvasGroup.enabled = false;
            }


            if (_button != null)
                _button.interactable = canAfford;
        }
    }
}
