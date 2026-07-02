using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Gameplay;

namespace ETD.UI
{
    public class DebugResetUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F12;
        [SerializeField] private bool _enableInBuilds = false;

        [Header("Data")]
        [SerializeField] private Data.GameDatabase _database;  // FIX: SerializeField, not ServiceLocator

        [Header("Buttons")]
        [SerializeField] private Button _resetTurretUnlocks;
        [SerializeField] private Button _resetTraitUnlocks;
        [SerializeField] private Button _resetChallenges;
        [SerializeField] private Button _resetShopPurchases;
        [SerializeField] private Button _resetAllProgress;
        [SerializeField] private Button _resetSavedRun;
        [SerializeField] private Button _resetTutorial;
        [SerializeField] private Button _resetHubTutorial;

        [Header("Status")]
        [SerializeField] private TMP_Text _statusText;

        private void Awake()
        {
#if !UNITY_EDITOR
            if (!_enableInBuilds) { gameObject.SetActive(false); return; }
#endif
            if (_panel != null) _panel.SetActive(false);

            _resetTurretUnlocks?.onClick.AddListener(ResetTurrets);
            _resetTraitUnlocks?.onClick.AddListener(ResetTraits);
            _resetChallenges?.onClick.AddListener(ResetChallenges);
            _resetShopPurchases?.onClick.AddListener(ResetShop);
            _resetAllProgress?.onClick.AddListener(ResetAll);
            _resetSavedRun?.onClick.AddListener(ResetSavedRun);
            _resetTutorial?.onClick.AddListener(() =>
            {
                var save = SaveSystem.Load();
                save.TutorialCompleted = false;
                SaveSystem.Save(save);
                ShowStatus("In-game tutorial reset.");
            });

            _resetHubTutorial?.onClick.AddListener(() =>
            {
                var save = SaveSystem.Load();
                save.HubTutorialSeen = false;
                save.TutorialCompleted = false;
                SaveSystem.Save(save);
                ShowStatus("Full tutorial reset — reopen hub.");
            });
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(_toggleKey))
                _panel?.SetActive(!(_panel.activeSelf));
        }

        private void ResetTurrets()
        {
            var save = SaveSystem.Load();
            var defaultIds = new System.Collections.Generic.List<string>();

            // FIX: use _database SerializeField, check for null
            if (_database?.Turrets != null)
                foreach (var t in _database.Turrets)
                    if (t != null && t.IsUnlockedByDefault) defaultIds.Add(t.Id);

            save.UnlockedTurretIds = defaultIds.ToArray();
            SaveSystem.Save(save);
            HubBadgeRegistry.ClearLastRunData();
            ShowStatus("Turret unlocks reset.");
        }

        private void ResetTraits()
        {
            var save = SaveSystem.Load();
            var defaultIds = new System.Collections.Generic.List<string>();

            if (_database?.Traits != null)
                foreach (var t in _database.Traits)
                    if (t != null && t.IsUnlockedByDefault) defaultIds.Add(t.Id);

            save.UnlockedTraitIds = defaultIds.ToArray();
            save.SelectedTraitIds = System.Array.Empty<string>();
            save.TraitUpgradeIds = System.Array.Empty<string>();
            save.TraitUpgradeLevels = System.Array.Empty<int>();
            SaveSystem.Save(save);
            ShowStatus("Trait unlocks reset.");
        }

        private void ResetChallenges()
        {
            var save = SaveSystem.Load();
            save.CompletedChallengeIds = System.Array.Empty<string>();
            save.ClaimedChallengeIds = System.Array.Empty<string>();
            save.ChallengeProgressTypes = System.Array.Empty<int>();
            save.ChallengeProgressValues = System.Array.Empty<float>();
            SaveSystem.Save(save);
            ShowStatus("Challenge progress reset.");
        }

        private void ResetShop()
        {
            var save = SaveSystem.Load();
            save.ShopMetaBonusLevel = 0;
            save.ShopXPMultiplierLevel = 0;
            save.ShopGoldMultiplierLevel = 0;
            save.ShopMaxHPLevel = 0;
            save.TraitSlotCount = GameConstants.MAX_TRAIT_SLOTS_DEFAULT;
            save.RerollTokens = 0;
            for (int i = 0; i < save.PermanentBonuses.Length; i++)
                save.PermanentBonuses[i] = 0f;
            SaveSystem.Save(save);
            ShowStatus("Shop purchases reset.");
        }

        private void ResetSavedRun()
        {
            RunSnapshotManager.ClearSnapshot();
            ShowStatus("Saved run cleared.");
        }

        private void ResetAll()
        {
            ResetTurrets();
            ResetTraits();
            ResetChallenges();
            ResetShop();
            ResetSavedRun();
            ShowStatus("ALL progress reset.");
        }

        private void ShowStatus(string msg)
        {
            if (_statusText != null)
            {
                _statusText.text = msg;
                CancelInvoke(nameof(ClearStatus));
                Invoke(nameof(ClearStatus), 2f);
            }
            Debug.Log($"[DebugReset] {msg}");
        }

        private void ClearStatus()
        {
            if (_statusText != null) _statusText.text = "";
        }
    }
}