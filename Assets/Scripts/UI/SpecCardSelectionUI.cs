// ============================================================================
// ETD.UI - SpecCardSelectionUI.cs  [UPDATED - re-roll mechanic]
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Data;
using ETD.Gameplay;

namespace ETD.UI
{
    public class SpecCardSelectionUI : MonoBehaviour
    {
        [Header("Card Slots")]
        [SerializeField] private SpecCardOptionUI[] _cardSlots;

        [Header("Panel")]
        [SerializeField] private GameObject _panel;

        [Header("Re-Roll")]
        [SerializeField] private Button _rerollButton;
        [SerializeField] private TMP_Text _rerollButtonText;
        [SerializeField] private TMP_Text _rerollTokenText;

        [Tooltip("Crystal icon shown next to the cost while the button is in PAID mode " +
                 "(free tokens exhausted). Hidden while free tokens remain. Assign your " +
                 "crystal sprite object here.")]
        [SerializeField] private GameObject _crystalIcon;

        [Tooltip("Shows the player's CURRENT crystal total while the button is in PAID " +
                 "mode, so they can see what they can afford. Hidden while free tokens " +
                 "remain. Assign a TextMeshPro text here.")]
        [SerializeField] private TMP_Text _crystalAmountText;


        [Header("Info")]
        [SerializeField] private TMP_Text _levelText;

        private RunManager _runManager;
        private bool _isShowing;
        private int currentRerolls = 0;
        private void Awake()
        {
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            _rerollButton?.onClick.RemoveAllListeners();
            _rerollButton?.onClick.AddListener(OnRerollClicked);
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            ServiceLocator.TryGet(out _runManager);
            if (_runManager == null) return;
            currentRerolls = _runManager.RunData != null ? _runManager.RunData.ActiveRerollTokens : 0;
            ShowCards(evt.NewLevel);
        }

        private void ShowCards(int level)
        {
            var options = _runManager?.CurrentSpecOptions;
            if (options == null || options.Length == 0) return;

            if (_panel != null) _panel.SetActive(true);
            _isShowing = true;

            //if (_levelText != null)
            //    _levelText.text = $"{LocalizationManager.Get("ui_level_up", "LEVEL UP!")} � {LocalizationManager.Get("ui_level", "Level")} {level}";

            for (int i = 0; i < _cardSlots.Length; i++)
            {
                if (_cardSlots[i] == null) continue;
                if (i < options.Length && options[i] != null)
                {
                    int captured = i;
                    _cardSlots[i].Setup(options[i], () => OnCardChosen(captured));

                    _cardSlots[i].gameObject.SetActive(true);
                    _cardSlots[i].GetComponent<UIInteractiveEffect>().ResetAll();
                }
                else
                {
                    _cardSlots[i].gameObject.SetActive(false);
                }
            }

            RefreshRerollButton();
        }

        /// <summary>Called by KeyboardShortcuts (keys 1,2,3)</summary>
        public void SelectCard(int index)
        {
            if (!_isShowing) return;
            if (index >= _cardSlots.Length || _cardSlots[index] == null) return;
            if (!_cardSlots[index].gameObject.activeSelf) return;
            OnCardChosen(index);
        }

        /// <summary>Called by KeyboardShortcuts (key R)</summary>
        public void TryReroll()
        {
            if (!_isShowing) return;
            if (_rerollButton != null && _rerollButton.interactable)
                OnRerollClicked();
        }

        private void OnCardChosen(int index)
        {
            if (!_isShowing) return;
            _isShowing = false;
            AudioManager.Instance?.PlaySFX(GameSoundConfig.Instance?.SpecCardSelect, SoundCategory.SpecCard);
            EventBus.Publish(new SpecCardChosenEvent { CardIndex = index });
           
            if (_panel != null) _panel.SetActive(false);

        }

        private void OnRerollClicked()
        {
            // Free tokens first; once exhausted the SAME button switches to paid
            // crystal rerolls (escalating cost, capped per offer by RunManager).
            if (currentRerolls > 0)
            {
                currentRerolls--;
                if (_runManager != null && _runManager.RunData != null)
                    _runManager.RunData.ActiveRerollTokens = currentRerolls;

                AudioManager.Instance?.PlaySFX(GameSoundConfig.Instance?.ButtonClick, SoundCategory.UI, 0.9f, 1.1f);
                _runManager?.RerollSpecCards();
                ShowCards(_runManager?.RunData?.Level ?? 1);
                return;
            }

            if (_runManager == null) return;
            if (_runManager.TryPaidCrystalReroll())
            {
                AudioManager.Instance?.PlaySFX(GameSoundConfig.Instance?.ButtonClick, SoundCategory.UI, 0.9f, 1.1f);
                ShowCards(_runManager.RunData?.Level ?? 1);
            }
            else
            {
                // Capped or can't afford — resync the button state.
                RefreshRerollButton();
            }
        }

        private void RefreshRerollButton()
        {
            if (currentRerolls > 0)
            {
                // Free-token mode: show remaining tokens (original behavior).
                if (_rerollButton != null) _rerollButton.interactable = true;
                if (_rerollTokenText != null) _rerollTokenText.text = currentRerolls.ToString();
                if (_crystalIcon != null) _crystalIcon.SetActive(false);
                if (_crystalAmountText != null) _crystalAmountText.gameObject.SetActive(false);
                return;
            }

            // Paid mode: show the crystal cost; disable when capped or unaffordable.
            int cost = _runManager != null ? _runManager.GetNextPaidRerollCost() : -1;
            int crystals = SaveSystem.Load().MetaCurrency;

            if (_crystalIcon != null) _crystalIcon.SetActive(true);
            if (_crystalAmountText != null)
            {
                _crystalAmountText.gameObject.SetActive(true);
                _crystalAmountText.text = ETD.Core.NumberFormat.Compact(crystals);
            }
            bool canPay = cost >= 0 && crystals >= cost;

            if (_rerollButton != null) _rerollButton.interactable = canPay;

            if (_rerollTokenText != null)
            {
                // Cost is crystal-colored so players see it is no longer a free token.
                _rerollTokenText.text = cost >= 0
                    ? $"<color=#7FDBFF>{cost}</color>"
                    : "-";
            }
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
        }
    }
}