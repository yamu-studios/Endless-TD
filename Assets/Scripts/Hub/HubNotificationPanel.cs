// ============================================================================
// ETD.Hub - HubNotificationPanel.cs  [NEW]
// Shows notifications in the hub after a run:
//   "New turret unlocked: Laser Turret"
//   "New trait available: Lucky Charm"
//   "Challenge completed: First Defense"
// Panel hidden if nothing new. Each entry has icon + label.
// Add to Hub scene. Call Refresh() on hub load.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Data;
using ETD.UI;

namespace ETD.Hub
{
    public class HubNotificationPanel : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameDatabase _database;

        [Header("Panel")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TMP_Text   _titleText;
        [SerializeField] private TMP_Text   _bodyText;
        [SerializeField] private Transform  _container;
        [SerializeField] private GameObject _notificationRowPrefab;
        [SerializeField] private Button     _dismissButton;

        [Header("Section Labels - fallback only")]
        [SerializeField] private string _newTurretLabel     = "New Turret Unlocked";
        [SerializeField] private string _newTraitLabel      = "New Trait Available";
        [SerializeField] private string _completedChalLabel = "Challenge Completed";

        private void Awake()
        {
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void Start()
        {
            _dismissButton?.onClick.AddListener(Dismiss);
            HubBadgeRegistry.LoadFromSave();
            Refresh();
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            Refresh();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        public void Refresh()
        {
            if (_container == null || _notificationRowPrefab == null) return;

            for (int i = _container.childCount - 1; i >= 0; i--)
                Destroy(_container.GetChild(i).gameObject);

            var rows = new List<(Sprite icon, string label, string subtitle)>();
            var save = SaveSystem.Load();

            // New turrets
            var newTurrets = save.LastRunNewTurretIds ?? System.Array.Empty<string>();
            foreach (var id in newTurrets)
            {
                var data = _database?.GetTurret(id);
                if (data != null)
                    rows.Add((
                        data.Icon,
                        LocalizationManager.Get("hub_notification_new_turret_title", _newTurretLabel),
                        SOLocalization.GetName("turret_" + data.LocalizationKey, data.DisplayName)
                    ));
            }

            // New traits
            var newTraits = save.LastRunNewTraitIds ?? System.Array.Empty<string>();
            foreach (var id in newTraits)
            {
                var data = _database?.GetTrait(id);
                if (data != null)
                    rows.Add((
                        data.Icon,
                        LocalizationManager.Get("hub_notification_new_trait_title", _newTraitLabel),
                        SOLocalization.GetName("trait_" + data.LocalizationKey, data.DisplayName)
                    ));
            }

            // Completed challenges (unclaimed)
            var completedChallenges = save.LastRunCompletedChallengeIds ?? System.Array.Empty<string>();
            var claimed = new System.Collections.Generic.HashSet<string>(
                save.ClaimedChallengeIds ?? System.Array.Empty<string>());
            foreach (var id in completedChallenges)
            {
                var data = _database?.GetChallenge(id);
                if (data != null)
                    rows.Add((
                        data.CompleteIcon,
                        LocalizationManager.Get("hub_notification_challenge_completed_title", _completedChalLabel),
                        SOLocalization.GetName("challenge_" + data.LocalizationKey, data.DisplayName)
                    ));
            }

            if (_titleText != null)
                _titleText.text = LocalizationManager.Get("hub_notification_panel_title", "Run Results");

            if (_bodyText != null)
                _bodyText.text = LocalizationManager.Get("hub_notification_panel_body", "New unlocks and completed challenges from your last run.");

            bool hasAny = rows.Count > 0;
            if (_panelRoot != null) _panelRoot.SetActive(hasAny);
            if (!hasAny) return;

            foreach (var (icon, label, subtitle) in rows)
            {
                var go  = Instantiate(_notificationRowPrefab, _container);
                var row = go.GetComponent<HubNotificationRow>();
                if (row == null) row = go.AddComponent<HubNotificationRow>();
                row.Setup(icon, label, subtitle);
            }
        }

        public void Dismiss()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }
    }

   
}
