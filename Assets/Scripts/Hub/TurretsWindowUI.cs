// ============================================================================
// ETD.Hub - TurretsWindowUI.cs  [UPDATED]
// TurretEvolutionPathUI now has 3 separate stat texts (damage, range, speed)
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Data;
using ETD.Meta;

namespace ETD.Hub
{
    public class TurretsWindowUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameDatabase _database;

        [Header("Icon List")]
        [SerializeField] private Transform    _iconListContainer;
        [SerializeField] private GameObject   _iconItemPrefab;

        [Header("Info Panel (always open)")]
        [SerializeField] private Image    _infoIcon;
        [SerializeField] private TMP_Text _infoName;
        [SerializeField] private TMP_Text _infoDescription;
        [SerializeField] private TMP_Text _infoDamage;
        [SerializeField] private TMP_Text _infoRange;
        [SerializeField] private TMP_Text _infoFireSpeed;

        [Header("Evolution Paths Section")]
        [SerializeField] private GameObject          _evolutionSection;
        [SerializeField] private TurretEvolutionPathUI _pathA;
        [SerializeField] private TurretEvolutionPathUI _pathB;

        [Header("Colors")]
        [SerializeField] private Color _selectedBorderColor = new Color(0.3f, 0.85f, 1f);
        [SerializeField] private Color _defaultBorderColor  = new Color(0.3f, 0.3f, 0.3f);

        [Header("Labels")]
        [SerializeField] private string _damageLabel  = "DMG";
        [SerializeField] private string _rangeLabel   = "RNG";
        [SerializeField] private string _speedLabel   = "SPD";
        [SerializeField] private string _noValueLabel = "—";

        [Header("Close")]
        [SerializeField] private Button _closeButton;

        private readonly List<TurretIconItem> _items = new();
        private TurretIconItem _selectedItem;
        private MetaProgressionManager _metaManager;

        private void OnEnable()
        {
            _metaManager = ServiceLocator.Get<MetaProgressionManager>();
            _closeButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.AddListener(() => gameObject.SetActive(false));
            GenerateList();
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
        private void GenerateList()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
                if (_items[i] != null) Destroy(_items[i].gameObject);
            _items.Clear();
            for (int i = _iconListContainer.childCount - 1; i >= 0; i--)
                Destroy(_iconListContainer.GetChild(i).gameObject);

            if (_database?.Turrets == null) return;

            foreach (var td in _database.Turrets)
            {
                var go   = Instantiate(_iconItemPrefab, _iconListContainer);
                var item = go.GetComponent<TurretIconItem>();
                if (item == null) item = go.AddComponent<TurretIconItem>();

                bool unlocked = _metaManager == null || _metaManager.IsTurretUnlocked(td.Id);
                item.Setup(td, unlocked, _selectedBorderColor, _defaultBorderColor, OnItemClicked);
                _items.Add(item);
            }

            if (_items.Count > 0) OnItemClicked(_items[0]);
        }

        private void OnItemClicked(TurretIconItem clicked)
        {
            _selectedItem?.SetBorderSelected(false);
            _selectedItem = clicked;
            clicked.SetBorderSelected(true);

            var  td       = clicked.Data;
            bool unlocked = clicked.IsUnlocked;

            if (_infoIcon != null) { _infoIcon.sprite = td.Icon; _infoIcon.enabled = td.Icon != null; }
            if (_infoName != null) _infoName.text = SOLocalization.GetName("turret_"+td.LocalizationKey, td.DisplayName);
            //if (_infoDescription != null) _infoDescription.text = unlocked ? td.Description : "Locked";

            //if (_infoDescription != null)
            //{
            //    if (unlocked)
            //    {
            //        _infoDescription.text = SOLocalization.GetDesc("turret_"+td.LocalizationKey, td.Description);
            //    }
            //    else
            //    {
            //        // Show unlock condition with inline progress (same as traits)
            //        _infoDescription.text = BuildUnlockText(td, SOLocalization.GetUnlock("turret_" + td.LocalizationKey, td.UnlockCondition));
            //    }
            //}
            if (_infoDescription != null)
            {
                string localizedDescription = SOLocalization.GetDesc("turret_" + td.LocalizationKey, td.Description);

                if (!unlocked)
                {
                    string unlockText = BuildUnlockText(td, SOLocalization.GetUnlock("turret_" + td.LocalizationKey, td.UnlockCondition));
                    _infoDescription.text = $"{unlockText}\n\n<color=#FF5A5F>{localizedDescription}</color>";
                }
                else
                {
                    // Show localized description first, then unlock condition note if applicable
                    _infoDescription.text = localizedDescription;
                }
            }

            // Stats
            bool hasSpeed = unlocked
                && td.Type != TurretType.Support
                && td.Type != TurretType.Radar
                && !td.IsContinuousBeam;
            
           
            if (_infoDamage    != null) _infoDamage.text    = $"{_damageLabel}: {(unlocked ? $"{td.Damage:F0}" : _noValueLabel)}";
            if (_infoRange     != null) _infoRange.text     = $"{_rangeLabel}: {(unlocked ? $"{td.Range:F1}" : _noValueLabel)}";
            if (_infoFireSpeed != null)
            {
                string val = !unlocked ? _noValueLabel
                    : hasSpeed ? $"{(td.AttackInterval > 0 ? 1f / td.AttackInterval : 0f):F2}/s"
                    : _noValueLabel;
                _infoFireSpeed.text = $"{_speedLabel}: {val}";
            }

            // Evolution paths
            bool hasPaths = td.PathA != null || td.PathB != null;
            if (_evolutionSection != null) _evolutionSection.SetActive(hasPaths && unlocked);

            if (hasPaths && unlocked)
            {
                _pathA.Setup(td.PathA, td, _damageLabel, _rangeLabel, _speedLabel, _noValueLabel,"path_a");
                _pathB.Setup(td.PathB, td, _damageLabel, _rangeLabel, _speedLabel, _noValueLabel,"path_b");
            }

            // Mark turret as viewed — dismiss badge
        HubBadgeRegistry.MarkViewed(HubBadgeType.Turret, td.Id);
            // Also hide the badge on the item
            clicked.HideBadge();
            // Refresh button badge
            HubController.Instance?.RefreshButtonBadges();
        }

        private string BuildUnlockText(TurretData td,string text)
        {
            string baseText = text;
            if (string.IsNullOrEmpty(baseText)) return "Locked";
            if (td.UnlockConditionTarget <= 0) return $" {baseText}";

            var save = SaveSystem.Load();
            float progress = SaveSystem.GetChallengeProgress(save, (int)td.UnlockConditionType);
            progress = Mathf.Min(progress, td.UnlockConditionTarget);

            string targetStr = FormatNum(td.UnlockConditionTarget);
            string progressStr = $"({FormatNum(progress)})";

            int idx = baseText.IndexOf(targetStr, System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return baseText.Substring(0, idx + targetStr.Length)
                    + progressStr
                    + baseText.Substring(idx + targetStr.Length);
            }

            return $"{baseText} ({FormatNum(progress)}/{targetStr})";
        }

        private static string FormatNum(float n)
        {
            if (n >= 1_000_000) return $"{n / 1_000_000f:F1}M";
            if (n >= 1_000) return $"{n / 1_000f:F1}K";
            return $"{n:F0}";
        }

    }



}
