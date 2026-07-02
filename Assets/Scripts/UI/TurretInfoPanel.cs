// ============================================================================
// ETD.UI - TurretInfoPanel.cs
// Shown when player selects a placed turret. Displays current stats,
// relic slots, upgrade button, sell button, and evolution choices.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Data;
using ETD.Gameplay;
using ETD.Turrets;
using System.IO;

namespace ETD.UI
{
    public class TurretInfoPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panel;

        [Header("Drag")]
        [SerializeField] private bool _enableDragging = true;
        [SerializeField] private RectTransform _dragHandle;
        [SerializeField] private string _dragSaveId = "turret_info_panel";

        [Header("Info")]
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _damageText;
        [SerializeField] private TMP_Text _attackSpeedText;
        [SerializeField] private TMP_Text _rangeText;


        [Header("Buttons")]
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private TMP_Text _upgradeCostText;
        [SerializeField] private Button _sellButton;
        [SerializeField] private TMP_Text _sellValueText;

        [Header("Evolution")]
        [SerializeField] private GameObject _evolvePanel;
        [SerializeField] private Button _evolvePathAButton;
        [SerializeField] private Image _evolvePathAIcon;
        [SerializeField] private TMP_Text _evolvePathAText;
        [SerializeField] private Button _evolvePathBButton;
        [SerializeField] private Image _evolvePathBIcon;
        [SerializeField] private TMP_Text _evolvePathBText;

        private TurretController _selectedTurret;
        private int _selectedTurretId = -1;
        private TurretManager _turretManager;
        private RunManager _runManager;

        private void Start()
        {
            ResolveServices();

            EnsureDraggable();

            if (_panel != null) _panel.SetActive(false);
            if (_evolvePanel != null) _evolvePanel.SetActive(false);

            _upgradeButton?.onClick.AddListener(OnUpgradeClicked);
            _sellButton?.onClick.AddListener(OnSellClicked);
            _evolvePathAButton?.onClick.AddListener(() => OnEvolveClicked(0));
            _evolvePathBButton?.onClick.AddListener(() => OnEvolveClicked(1));

            EventBus.Subscribe<TurretSelectedEvent>(OnTurretSelected);
            EventBus.Subscribe<TurretDeselectedEvent>(OnTurretDeselected);
            EventBus.Subscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Subscribe<ShowEvolveChoiceEvent>(OnShowEvolve);
            EventBus.Subscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Subscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Subscribe<PrepPhaseStartedEvent>(OnPrepPhaseStarted);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void EnsureDraggable()
        {
            if (!_enableDragging || _panel == null)
                return;

            var draggable = _panel.GetComponent<DraggableUIPanel>();
            if (draggable == null)
                draggable = _panel.AddComponent<DraggableUIPanel>();

            draggable.Configure(_dragSaveId, _dragHandle);
        }

        private void ResolveServices()
        {
            if (_turretManager == null)
                ServiceLocator.TryGet(out _turretManager);
            if (_runManager == null)
                ServiceLocator.TryGet(out _runManager);
        }

        private bool ResolveSelectedTurret()
        {
            ResolveServices();

            if (_selectedTurretId < 0)
                return false;

            if (_selectedTurret == null || _selectedTurret.InstanceId != _selectedTurretId)
                _selectedTurret = _turretManager != null ? _turretManager.GetTurret(_selectedTurretId) : null;

            return _selectedTurret != null;
        }

        private void RefreshOpenPanel()
        {
            if (_panel != null && _panel.activeSelf)
                RefreshInfo();
        }

        private void OnTurretSelected(TurretSelectedEvent evt)
        {
            ResolveServices();
            _selectedTurretId = evt.TurretId;
            _selectedTurret = _turretManager?.GetTurret(evt.TurretId);

            if (_selectedTurret == null) return;
            _panel.SetActive(true);
            RefreshInfo();
        }
        private void OnTurretDeselected(TurretDeselectedEvent evt)
        {
            // If evolve panel was open, restore game state before hiding
            if (_evolvePanel != null && _evolvePanel.activeSelf)
            {
                _evolvePanel.SetActive(false);
                GameManager.Instance.PopModalState();
            }
            if (_panel != null) _panel.SetActive(false);
            _selectedTurret = null;
            _selectedTurretId = -1;
        }


        /// <summary>Called by KeyboardShortcuts (keys 1,2 during evolve choice)</summary>
        public void EvolvePathByKey(int path)
        {
            if (_evolvePanel == null || !_evolvePanel.activeSelf) return;
            OnEvolveClicked(path);
        }

        /// <summary>Called by KeyboardShortcuts (key Q)</summary>
        public void UpgradeShortcut()
        {
            if (!ResolveSelectedTurret() || _upgradeButton == null) return;
            if (_upgradeButton.interactable)
                _upgradeButton.onClick.Invoke();
        }

        /// <summary>Called by KeyboardShortcuts (key E)</summary>
        public void SellShortcut()
        {
            if (!ResolveSelectedTurret() || _sellButton == null) return;
            if (_sellButton.interactable)
                _sellButton.onClick.Invoke();
        }

        private void OnTurretUpgraded(TurretUpgradedEvent evt)
        {
            if (_selectedTurret != null && _selectedTurret.InstanceId == evt.TurretId)
                RefreshInfo();
        }

        //private void OnShowEvolve(ShowEvolveChoiceEvent evt)
        //{
        //    if (_selectedTurret == null || _selectedTurret.InstanceId != evt.TurretId) return;

        //    //GameManager.Instance.SetState(GameState.EvolveChoice);

        //    if (_evolvePanel != null) _evolvePanel.SetActive(true);
        //    if (_evolvePathAText != null && _selectedTurret.Data.PathA != null)
        //    {
        //        _evolvePathAText.text = SOLocalization.GetName("turret_" + _selectedTurret.Data.LocalizationKey + "_path_a", _selectedTurret.Data.PathA.Name);
        //        _evolvePathAIcon.sprite = _selectedTurret.Data.PathA.Icon;
        //    }

        //    if (_evolvePathBText != null && _selectedTurret.Data.PathB != null)
        //    {
        //        _evolvePathBText.text = SOLocalization.GetName("turret_" + _selectedTurret.Data.LocalizationKey + "_path_b", _selectedTurret.Data.PathB.Name);
        //        _evolvePathBIcon.sprite = _selectedTurret.Data.PathB.Icon;
        //    }

        //}

        private void OnShowEvolve(ShowEvolveChoiceEvent evt)
        {
            if (_selectedTurret == null || _selectedTurret.InstanceId != evt.TurretId)
                return;

            var data = _selectedTurret.Data;
            if (data == null)
                return;

            if (_evolvePanel != null)
                _evolvePanel.SetActive(true);

            bool hasPathA = data.PathA != null;
            bool hasPathB = data.PathB != null;

            if (_evolvePathAButton != null)
                _evolvePathAButton.gameObject.SetActive(hasPathA);

            if (_evolvePathBButton != null)
                _evolvePathBButton.gameObject.SetActive(hasPathB);

            if (hasPathA)
            {
                if (_evolvePathAText != null)
                {
                    _evolvePathAText.text = SOLocalization.GetName(
                        "turret_" + data.LocalizationKey + "_path_a",
                        data.PathA.Name
                    );
                }

                if (_evolvePathAIcon != null)
                    _evolvePathAIcon.sprite = data.PathA.Icon;
            }

            if (hasPathB)
            {
                if (_evolvePathBText != null)
                {
                    _evolvePathBText.text = SOLocalization.GetName(
                        "turret_" + data.LocalizationKey + "_path_b",
                        data.PathB.Name
                    );
                }

                if (_evolvePathBIcon != null)
                    _evolvePathBIcon.sprite = data.PathB.Icon;
            }
        }

        private void RefreshInfo()
        {
            if (!ResolveSelectedTurret())
            {
                if (_panel != null) _panel.SetActive(false);
                return;
            }

            var data = _selectedTurret.Data;
            string baseKey = "turret_" + data.LocalizationKey;
            string displayName = _selectedTurret.IsEvolved
                ? (_selectedTurret.EvolutionPath == 0
                    ? SOLocalization.GetName(baseKey + "_path_a", data.PathA != null ? data.PathA.Name : data.DisplayName)
                    : SOLocalization.GetName(baseKey + "_path_b", data.PathB != null ? data.PathB.Name : data.DisplayName))
                : SOLocalization.GetName(baseKey, data.DisplayName);

            SetText(_nameText, displayName);
            SetText(_levelText, LocalizationManager.GetFormat("ui_level_short_format", "Lv {0}", _selectedTurret.Level));
            //SetText(_damageText, $"{_selectedTurret.Damage:F1}");
            //SetText(_attackSpeedText, $"{_selectedTurret.AttackSpeed:F2}/s");
            //SetText(_rangeText, $"{_selectedTurret.Range:F1}");
            RefreshStats();

            int upgradeCost = _selectedTurret.GetUpgradeCost();
            SetText(_upgradeCostText, upgradeCost.ToString());
            if (_upgradeButton != null)
                _upgradeButton.interactable = _runManager != null
                    && _runManager.RunData != null
                    && _runManager.RunData.Gold >= upgradeCost;

            int sellValue = _selectedTurret.GetSellValue();
            SetText(_sellValueText, sellValue.ToString());
            if (_sellButton != null)
                _sellButton.interactable = true;

          
        }
        private void RefreshStats()
        {
            var tile = _selectedTurret.DynamicTile;

            SetText(_damageText,
                $"{_selectedTurret.Damage:F1}" + TileMod(tile, TurretStatModifier.StatType.Damage));

            SetText(_attackSpeedText,
                $"{_selectedTurret.AttackSpeed:F2}/s" + TileMod(tile, TurretStatModifier.StatType.AttackSpeed));

            SetText(_rangeText,
                $"{_selectedTurret.Range:F1}" + TileMod(tile, TurretStatModifier.StatType.Range));
        }

        /// <summary>
        /// Returns a colored parenthetical string for a tile modifier on the given stat.
        /// e.g. " <color=#44FF66>(+10%)</color>" or " <color=#FF5555>(-20%)</color>"
        /// Returns empty string if this tile has no modifier for this stat.
        /// </summary>
        private static string TileMod(DynamicTileData tile, TurretStatModifier.StatType stat)
        {
            if (tile == null) return "";

            float total = 0f;
            if (tile.PrimaryEffect.Stat == stat) total += tile.PrimaryEffect.Value;
            if (tile.Tradeoff.Stat == stat) total += tile.Tradeoff.Value;

            if (Mathf.Abs(total) < 0.0001f) return "";

            string sign = total > 0 ? "+" : "";
            string color = total > 0 ? "#44FF66" : "#FF5555";
            return $" <color={color}>({sign}{total * 100f:F0}%)</color>";
        }
        private void OnUpgradeClicked()
        {
            if (!ResolveSelectedTurret() || _runManager == null || _runManager.RunData == null) return;
            int cost = _selectedTurret.GetUpgradeCost();
            if (_runManager.SpendGold(cost))
            {
                _selectedTurret.Upgrade();
                RefreshInfo();
            }
        }

        private void OnSellClicked()
        {
            if (!ResolveSelectedTurret()) return;
            int id = _selectedTurretId;
            _turretManager?.SellTurret(id);
            _selectedTurret = null;
            _selectedTurretId = -1;
            _panel.SetActive(false);
        }

        private void OnEvolveClicked(int path)
        {

            if (!ResolveSelectedTurret()) return;

            if (ServiceLocator.TryGet<TurretManager>(out var mgr))
            {
                var evolved = mgr.EvolveTurret(_selectedTurret.InstanceId, path);
                if (evolved != null)
                {
                    _selectedTurret = evolved;
                    _selectedTurretId = evolved.InstanceId;
                }
            }

            if (_evolvePanel != null) _evolvePanel.SetActive(false);

            // Restore whatever state we were in before evolve (Prep OR WaveActive)
            GameManager.Instance.PopModalState();
            RefreshInfo();
        }

        private void OnGoldChanged(GoldChangedEvent evt) => RefreshOpenPanel();
        private void OnWaveStarted(WaveStartedEvent evt) => RefreshOpenPanel();
        private void OnPrepPhaseStarted(PrepPhaseStartedEvent evt) => RefreshOpenPanel();
        private void OnGameStateChanged(GameStateChangedEvent evt) => RefreshOpenPanel();

        private void SetText(TMP_Text text, string value)
        {
            if (text != null) text.text = value;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TurretSelectedEvent>(OnTurretSelected);
            EventBus.Unsubscribe<TurretDeselectedEvent>(OnTurretDeselected);
            EventBus.Unsubscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Unsubscribe<ShowEvolveChoiceEvent>(OnShowEvolve);
            EventBus.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Unsubscribe<PrepPhaseStartedEvent>(OnPrepPhaseStarted);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }
    }
}
