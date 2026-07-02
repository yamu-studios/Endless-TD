using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Data;
using ETD.Meta;

namespace ETD.Hub
{
    public class ChallengesWindowUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameDatabase _database;

        [Header("List")]
        [SerializeField] private Transform _listContainer;
        [SerializeField] private GameObject _itemPrefab;

        [Header("Colors")]
        [SerializeField] private Color _selectedBorderColor = new Color(0.3f, 0.85f, 1f);
        [SerializeField] private Color _defaultBorderColor = new Color(0.3f, 0.3f, 0.3f);
        [SerializeField] private Color _progressNumberColor = new Color(0.4f, 1f, 0.5f);

        [Header("Close")]
        [SerializeField] private Button _closeButton;

        private readonly List<ChallengeItemUI> _items = new();
        private ChallengeItemUI _selectedItem;
        private ChallengeTracker _tracker;

        private void OnEnable()
        {
            ServiceLocator.TryGet(out _tracker);
            _closeButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.AddListener(() => gameObject.SetActive(false));
            GenerateList();
            HubController.Instance?.RefreshButtonBadges();
        }

        private void GenerateList()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
                if (_items[i] != null) Destroy(_items[i].gameObject);
            _items.Clear();

            if (_listContainer != null)
                for (int i = _listContainer.childCount - 1; i >= 0; i--)
                    Destroy(_listContainer.GetChild(i).gameObject);

            if (_database?.Challenges == null) return;

            var save = SaveSystem.Load();
            var completed = new HashSet<string>(save.CompletedChallengeIds ?? System.Array.Empty<string>());
            var claimed = new HashSet<string>(save.ClaimedChallengeIds ?? System.Array.Empty<string>());

            foreach (var ch in _database.Challenges)
            {
                if (ch == null) continue;
                var go = Instantiate(_itemPrefab, _listContainer);
                var item = go.GetComponent<ChallengeItemUI>();
                if (item == null) item = go.AddComponent<ChallengeItemUI>();

                bool done = completed.Contains(ch.Id);
                bool isClaimed = claimed.Contains(ch.Id);
                bool pendingClaim = done && !isClaimed;
                float norm = done ? 1f : GetProgressNormalized(ch);
                float raw = done ? ch.TargetValue : GetProgress(ch);

                // FIX: pass onClaim as positional arg, not named param
                item.Setup(ch, done, isClaimed, pendingClaim, norm, raw,
                    _progressNumberColor,
                    _selectedBorderColor,
                    _defaultBorderColor,
                    OnItemClicked,
                    OnClaimById);

                _items.Add(item);
            }
        }

        private void OnItemClicked(ChallengeItemUI clicked)
        {
            _selectedItem?.SetBorderSelected(false);
            _selectedItem = clicked;
            clicked.SetBorderSelected(true);

            HubBadgeRegistry.MarkViewed(HubBadgeType.Challenge, clicked.Data.Id);
            clicked.HideBadge();
            HubController.Instance?.RefreshButtonBadges();
        }

        // =================================================================
        // CLAIM — applied on demand, not automatically
        // =================================================================

        private void OnClaimById(string challengeId)
        {
            var ch = _database?.GetChallenge(challengeId);
            if (ch == null) return;

            var save = SaveSystem.Load();
            bool done = System.Array.Exists(
                save.CompletedChallengeIds ?? System.Array.Empty<string>(), id => id == ch.Id);
            bool claimed = System.Array.Exists(
                save.ClaimedChallengeIds ?? System.Array.Empty<string>(), id => id == ch.Id);

            if (!done || claimed) return;

            ApplyReward(ch, save);
            SaveSystem.MarkClaimed(save, ch.Id);
            SaveSystem.Save(save);
            HubBadgeRegistry.MarkViewed(HubBadgeType.Challenge, ch.Id);

            AudioManager.Instance?.PlaySFX(
                GameSoundConfig.Instance?.ChallengeComplete, SoundCategory.UI);

            GenerateList();
            HubController.Instance?.RefreshButtonBadges();
        }

        private void ApplyReward(ChallengeData ch, SaveData save)
        {
            switch (ch.RewardType)
            {
                case ChallengeRewardType.FlatCurrency:
                    save.MetaCurrency += Mathf.RoundToInt(ch.RewardValue);
                    EventBus.Publish(new MetaCurrencyChangedEvent
                    { Current = save.MetaCurrency, Delta = Mathf.RoundToInt(ch.RewardValue) });
                    break;
                case ChallengeRewardType.MaxHPBonus:
                    save.PermanentBonuses[0] += ch.RewardValue;
                    break;
                case ChallengeRewardType.XPMultiplier:
                    save.PermanentBonuses[1] += ch.RewardValue - 1f;
                    break;
                case ChallengeRewardType.GoldMultiplier:
                    save.PermanentBonuses[2] += ch.RewardValue - 1f;
                    break;
                case ChallengeRewardType.DamageBonus:
                    save.PermanentBonuses[3] += ch.RewardValue - 1f;
                    break;
                case ChallengeRewardType.MetaCurrencyMultiplier:
                    save.PermanentBonuses[5] += ch.RewardValue - 1f;
                    break;
            }
        }

        // =================================================================
        // PROGRESS HELPERS
        // =================================================================

        private float GetProgress(ChallengeData ch)
        {
            if (_tracker != null) return _tracker.GetProgress(ch);
            return SaveSystem.GetChallengeProgress(
                SaveSystem.Load(), (int)ch.ConditionType);
        }

        private float GetProgressNormalized(ChallengeData ch)
        {
            if (ch.TargetValue <= 0) return 0f;
            return Mathf.Clamp01(GetProgress(ch) / ch.TargetValue);
        }

        public static string BuildProgressText(float current, float target, Color numColor)
        {
            string hex = ColorUtility.ToHtmlStringRGB(numColor);
            return $"<color=#{hex}>{FormatNum(Mathf.Min(current, target))}</color> / {FormatNum(target)}";
        }

        public static Color ProgressColor(float t)
        {
            if (t <= 0.5f)
                return Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.85f, 0.1f), t * 2f);
            return Color.Lerp(new Color(1f, 0.85f, 0.1f), new Color(0.2f, 0.85f, 0.2f), (t - 0.5f) * 2f);
        }

        private static string FormatNum(float n)
        {
            if (n >= 1_000_000) return $"{n / 1_000_000f:F1}M";
            if (n >= 1_000) return $"{n / 1_000f:F1}K";
            return $"{n:F0}";
        }
    }
}