// ETD.Hub - ChallengeItemUI.cs  [FIXED]
// onClaim parameter properly added to Setup() signature.
// Claim button wired inside the method body.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Data;

namespace ETD.Hub
{
    public class ChallengeItemUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _border;
        [SerializeField] private TMP_Text _conditionText;
        [SerializeField] private Image _progressFill;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private Image _rewardIcon;
        [SerializeField] private TMP_Text _rewardText;
        [SerializeField] private GameObject _completedIconGO;
        [SerializeField] private GameObject _pendingClaimGO;
        [SerializeField] private Button _button;
        [SerializeField] private HubBadge _badge;
        [SerializeField] private Button _claimButton;

        public ChallengeData Data { get; private set; }
        public bool IsCompleted { get; private set; }

        private Color _selBorder, _defBorder;

        // FIX: onClaim is now a proper parameter in the signature
        public void Setup(ChallengeData data, bool isCompleted, bool isClaimed,
            bool pendingClaim, float normProg, float rawProg,
            Color numColor, Color selBorder, Color defBorder,
            System.Action<ChallengeItemUI> onClick,
            System.Action<string> onClaim = null)
        {
            Data = data;
            IsCompleted = isCompleted;
            _selBorder = selBorder;
            _defBorder = defBorder;

            if (_icon != null)
                _icon.sprite = isCompleted ? data.CompleteIcon : data.IncompleteIcon;

            if (_border != null) _border.color = data.BorderColor;

            if (_conditionText != null)
            {
                string baseKey = "challenge_" + data.LocalizationKey;
                string fallback = string.IsNullOrEmpty(data.ConditionDescription)
                    ? data.DisplayName
                    : data.ConditionDescription;

                _conditionText.text = SOLocalization.GetCondition(baseKey, fallback);
            }

            if (_progressFill != null)
            {
                _progressFill.fillAmount = normProg;
                _progressFill.color = ChallengesWindowUI.ProgressColor(normProg);
            }
            if (_progressText != null)
                _progressText.text = ChallengesWindowUI.BuildProgressText(rawProg, data.TargetValue, numColor);

            if (_rewardIcon != null)
            {
                _rewardIcon.sprite = data.RewardIcon;
                _rewardIcon.enabled = data.RewardIcon != null;
            }
            if (_rewardText != null)
                _rewardText.text = data.GetRewardAmountText();

            if (_completedIconGO != null) _completedIconGO.SetActive(isClaimed);
            if (_pendingClaimGO != null) _pendingClaimGO.SetActive(pendingClaim);

            _badge?.SetVisible(pendingClaim || HubBadgeRegistry.IsNew(HubBadgeType.Challenge, data.Id));

            // FIX: claim button wired inside the method body
            if (_claimButton != null)
            {
                _claimButton.gameObject.SetActive(pendingClaim);
                _claimButton.onClick.RemoveAllListeners();
                if (onClaim != null)
                    _claimButton.onClick.AddListener(() => onClaim.Invoke(data.Id));
            }

            SetBorderSelected(false);

            if (_button == null) _button = GetComponent<Button>();
            _button?.onClick.RemoveAllListeners();
            _button?.onClick.AddListener(() => onClick?.Invoke(this));
        }

        public void SetBorderSelected(bool sel)
        {
            if (_border != null) _border.color = sel ? _selBorder : _defBorder;
        }

        public void HideBadge() => _badge?.SetVisible(false);
    }
}