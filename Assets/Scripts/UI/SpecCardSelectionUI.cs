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
            //    _levelText.text = $"{LocalizationManager.Get("ui_level_up", "LEVEL UP!")} — {LocalizationManager.Get("ui_level", "Level")} {level}";

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

            if (currentRerolls <= 0) return;
            currentRerolls--;
            if (_runManager != null && _runManager.RunData != null)
                _runManager.RunData.ActiveRerollTokens = currentRerolls;

            AudioManager.Instance?.PlaySFX(GameSoundConfig.Instance?.ButtonClick, SoundCategory.UI, 0.9f, 1.1f);
            _runManager?.RerollSpecCards();
            ShowCards(_runManager?.RunData?.Level ?? 1);
        }

        private void RefreshRerollButton()
        {

            bool canReroll = currentRerolls > 0;

            if (_rerollButton != null) _rerollButton.interactable = canReroll;

            //string rerollLabel = LocalizationManager.Get("ui_reroll", "Re-Roll");
            //if (_rerollButtonText != null)
            //    _rerollButtonText.text = //isFree ? $"{rerollLabel} (FREE)" : $"{rerollLabel} ({tokens})";

            if (_rerollTokenText != null)
                _rerollTokenText.text = currentRerolls.ToString(); //isFree ? "FREE" : tokens.ToString();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
        }
    }
}