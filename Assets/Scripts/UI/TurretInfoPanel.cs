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

        [Tooltip("Assign your own close button here to match the game's UI style. If left empty, a simple runtime 'X' button is created instead.")]
        [SerializeField] private Button _closeButton;

        [Header("Evolution")]
        [SerializeField] private GameObject _evolvePanel;
        [SerializeField] private Button _evolvePathAButton;
        [SerializeField] private Image _evolvePathAIcon;
        [SerializeField] private TMP_Text _evolvePathAText;
        [SerializeField] private Button _evolvePathBButton;
        [SerializeField] private Image _evolvePathBIcon;
        [SerializeField] private TMP_Text _evolvePathBText;

        [Header("Evolution Tier 2")]
        [Tooltip("Single-confirm panel for the level-25 shared Tier2 evolution (no path choice).")]
        [SerializeField] private GameObject _evolveTier2Panel;
        [SerializeField] private Button _evolveTier2ConfirmButton;
        [SerializeField] private Image _evolveTier2Icon;
        [SerializeField] private TMP_Text _evolveTier2Text;

        [Header("Targeting Priority")]
        [Tooltip("Optional: a TMP_Dropdown listing the priorities. Assign this OR the cycle button (or both).")]
        [SerializeField] private TMP_Dropdown _targetingDropdown;
        [Tooltip("Optional: a Button that advances to the next priority on click. Pair it with the label below.")]
        [SerializeField] private Button _targetingCycleButton;
        [Tooltip("Optional: label showing the current priority name (used with the cycle button, or as a caption).")]
        [SerializeField] private TMP_Text _targetingModeLabel;
        [Tooltip("Optional: the whole targeting UI group. Hidden automatically for Support/Radar turrets that don't target. " +
                 "If left empty, the dropdown/button/label are hidden individually.")]
        [SerializeField] private GameObject _targetingGroup;

        // Player-selectable priorities (None is Support/Radar only and never listed).
        private static readonly TargetingMode[] SelectableModes =
        {
            TargetingMode.First, TargetingMode.Last, TargetingMode.Strongest, TargetingMode.Closest
        };

        private TurretController _selectedTurret;
        private int _selectedTurretId = -1;
        private TurretManager _turretManager;
        private RunManager _runManager;

        private void Start()
        {
            ResolveServices();

            EnsureDraggable();

            if (_closeButton != null)
                _closeButton.onClick.AddListener(ClosePanel);
            else
                PanelCloseButton.Ensure(_panel, ClosePanel);

            if (_panel != null) _panel.SetActive(false);
            if (_evolvePanel != null) _evolvePanel.SetActive(false);
            if (_evolveTier2Panel != null) _evolveTier2Panel.SetActive(false);

            _upgradeButton?.onClick.AddListener(OnUpgradeClicked);
            _sellButton?.onClick.AddListener(OnSellClicked);
            _evolvePathAButton?.onClick.AddListener(() => OnEvolveClicked(0));
            _evolvePathBButton?.onClick.AddListener(() => OnEvolveClicked(1));
            _evolveTier2ConfirmButton?.onClick.AddListener(OnEvolveTier2Clicked);

            if (_targetingDropdown != null)
            {
                PopulateTargetingDropdown();
                _targetingDropdown.onValueChanged.AddListener(OnTargetingDropdownChanged);
            }
            _targetingCycleButton?.onClick.AddListener(OnTargetingCycleClicked);

            EventBus.Subscribe<TurretSelectedEvent>(OnTurretSelected);
            EventBus.Subscribe<TurretDeselectedEvent>(OnTurretDeselected);
            EventBus.Subscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Subscribe<TurretEvolvedEvent>(OnTurretEvolvedRefresh);
            EventBus.Subscribe<ShowEvolveChoiceEvent>(OnShowEvolve);
            EventBus.Subscribe<ShowEvolveTier2ChoiceEvent>(OnShowEvolveTier2);
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
        /// <summary>
        /// Close button handler. Publishes the same deselect event the input
        /// handler uses, so the range indicator and every other selection
        /// listener stays consistent (OnTurretDeselected then hides the panel).
        /// </summary>
        private void ClosePanel()
        {
            EventBus.Publish(new TurretDeselectedEvent());
        }

        private void OnTurretDeselected(TurretDeselectedEvent evt)
        {
            // If an evolve panel was open, restore game state before hiding
            bool hadOpenEvolvePanel = false;
            if (_evolvePanel != null && _evolvePanel.activeSelf)
            {
                _evolvePanel.SetActive(false);
                hadOpenEvolvePanel = true;
            }
            if (_evolveTier2Panel != null && _evolveTier2Panel.activeSelf)
            {
                _evolveTier2Panel.SetActive(false);
                hadOpenEvolvePanel = true;
            }
            if (hadOpenEvolvePanel)
                GameManager.Instance.PopModalState();

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

        /// <summary>Called by KeyboardShortcuts (key 1 during the Tier2 single-confirm evolve choice)</summary>
        public void ConfirmTier2ByKey()
        {
            if (_evolveTier2Panel == null || !_evolveTier2Panel.activeSelf) return;
            OnEvolveTier2Clicked();
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

        /// <summary>
        /// Tier2 now evolves automatically with no confirm button (see
        /// TurretController.Upgrade), so this is what shows the newly evolved
        /// turret's stats — Path A/B evolves also flow through here.
        /// </summary>
        private void OnTurretEvolvedRefresh(TurretEvolvedEvent evt)
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

        /// <summary>
        /// v1.0 level-25 shared Tier2 evolution. Unlike OnShowEvolve, there's no
        /// path A/B choice — Tier2 converges from whichever path was already chosen,
        /// so this just shows a single confirm with the Tier2 name/icon.
        /// </summary>
        private void OnShowEvolveTier2(ShowEvolveTier2ChoiceEvent evt)
        {
            if (_selectedTurret == null || _selectedTurret.InstanceId != evt.TurretId)
                return;

            var data = _selectedTurret.Data;
            if (data == null || data.Tier2 == null)
                return;

            if (_evolveTier2Panel != null)
                _evolveTier2Panel.SetActive(true);

            string baseKey = "turret_" + data.LocalizationKey;

            if (_evolveTier2Text != null)
            {
                string name = SOLocalization.GetName(baseKey + "_tier2", data.Tier2.Name);
                string costLabel = LocalizationManager.GetFormat(
                    "turret_info_evolve_tier2_cost_format", "Evolve Cost: {0}", _selectedTurret.GetTier2EvolveCost());
                _evolveTier2Text.text = name + "\n" + costLabel;
            }

            if (_evolveTier2Icon != null)
                _evolveTier2Icon.sprite = data.Tier2.Icon;

            RefreshTier2Affordability();
        }

        private void RefreshTier2Affordability()
        {
            if (_evolveTier2ConfirmButton == null || _selectedTurret == null)
                return;

            int cost = _selectedTurret.GetTier2EvolveCost();
            _evolveTier2ConfirmButton.interactable = _runManager != null
                && _runManager.RunData != null
                && _runManager.RunData.Gold >= cost;
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

            RefreshTargetingUI();
        }

        // =================================================================
        // TARGETING PRIORITY UI
        // =================================================================

        private static string GetModeLabel(TargetingMode mode) => mode switch
        {
            TargetingMode.First     => LocalizationManager.Get("targeting_mode_first", "First"),
            TargetingMode.Last      => LocalizationManager.Get("targeting_mode_last", "Last"),
            TargetingMode.Strongest => LocalizationManager.Get("targeting_mode_strongest", "Strongest"),
            TargetingMode.Closest   => LocalizationManager.Get("targeting_mode_closest", "Closest"),
            _                       => LocalizationManager.Get("targeting_mode_first", "First"),
        };

        private static int IndexOfMode(TargetingMode mode)
        {
            for (int i = 0; i < SelectableModes.Length; i++)
                if (SelectableModes[i] == mode) return i;
            return 0;
        }

        private void PopulateTargetingDropdown()
        {
            if (_targetingDropdown == null) return;
            _targetingDropdown.ClearOptions();
            var opts = new System.Collections.Generic.List<string>(SelectableModes.Length);
            for (int i = 0; i < SelectableModes.Length; i++)
                opts.Add(GetModeLabel(SelectableModes[i]));
            _targetingDropdown.AddOptions(opts);
        }

        /// <summary>
        /// True when the selected turret actually aims at enemies. Checked by TYPE, not
        /// by TargetingMode — older Support/Radar assets shipped with DefaultTargeting
        /// = First, so the mode alone is not a reliable signal.
        /// </summary>
        private bool SelectedTurretUsesTargeting()
        {
            var data = _selectedTurret != null ? _selectedTurret.Data : null;
            if (data == null) return false;
            if (data.Type == TurretType.Support || data.Type == TurretType.Radar) return false;
            return true;
        }

        private void RefreshTargetingUI()
        {
            if (_selectedTurret == null) return;

            // Requested UX: the control stays visible for every turret. For Support/
            // Radar it shows "-" and is locked (cannot be opened/clicked).
            if (_targetingGroup != null) _targetingGroup.SetActive(true);

            bool usesTargeting = SelectedTurretUsesTargeting();

            if (_targetingDropdown != null)
            {
                _targetingDropdown.interactable = usesTargeting;

                if (usesTargeting)
                {
                    // SetValueWithoutNotify so syncing the UI to the turret doesn't
                    // re-fire onValueChanged (which would re-apply the mode / recurse).
                    _targetingDropdown.SetValueWithoutNotify(IndexOfMode(_selectedTurret.CurrentTargetingMode));
                    _targetingDropdown.RefreshShownValue();
                }
                else if (_targetingDropdown.captionText != null)
                {
                    // Non-targeting turret: blank caption instead of a fake mode.
                    _targetingDropdown.captionText.text = "-";
                }
            }

            if (_targetingCycleButton != null)
                _targetingCycleButton.interactable = usesTargeting;

            if (_targetingModeLabel != null)
                _targetingModeLabel.text = usesTargeting
                    ? GetModeLabel(_selectedTurret.CurrentTargetingMode)
                    : "-";
        }

        private void OnTargetingDropdownChanged(int index)
        {
            if (!ResolveSelectedTurret() || !SelectedTurretUsesTargeting()) return;
            if (index < 0 || index >= SelectableModes.Length) return;

            _selectedTurret.SetTargetingMode(SelectableModes[index]);
            if (_targetingModeLabel != null)
                _targetingModeLabel.text = GetModeLabel(SelectableModes[index]);
        }

        private void OnTargetingCycleClicked()
        {
            if (!ResolveSelectedTurret() || !SelectedTurretUsesTargeting()) return;

            int idx = IndexOfMode(_selectedTurret.CurrentTargetingMode);
            idx = (idx + 1) % SelectableModes.Length;
            _selectedTurret.SetTargetingMode(SelectableModes[idx]);
            RefreshTargetingUI();
        }

        // Live laser ramp readout: the panel is event-driven, but the ramp multiplier
        // changes every frame while the beam holds a target, so poll it at a low rate
        // while a ramping laser is selected. No scene wiring needed — the ramp is
        // appended to the existing damage row.
        private float _rampRefreshTimer;

        private void Update()
        {
            if (_panel == null || !_panel.activeSelf)
                return;
            if (_selectedTurret == null || !_selectedTurret.HasLaserRamp)
                return;

            _rampRefreshTimer += Time.unscaledDeltaTime;
            if (_rampRefreshTimer < 0.25f)
                return;
            _rampRefreshTimer = 0f;
            RefreshStats();
        }

        private static string LaserRampSuffix(TurretController turret)
        {
            if (turret == null || !turret.HasLaserRamp)
                return "";

            return $" <color=#FFA940>x{turret.LaserRampMultiplier:F2}/{turret.LaserRampCapMultiplier:F1}</color>";
        }

        private void RefreshStats()
        {
            var tile = _selectedTurret.DynamicTile;

            SetText(_damageText,
                $"{_selectedTurret.Damage:F1}" + TileMod(tile, TurretStatModifier.StatType.Damage)
                + LaserRampSuffix(_selectedTurret));

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

        private void OnEvolveTier2Clicked()
        {
            if (!ResolveSelectedTurret() || _runManager == null) return;

            int cost = _selectedTurret.GetTier2EvolveCost();
            if (!_runManager.SpendGold(cost)) return;

            if (ServiceLocator.TryGet<TurretManager>(out var mgr))
                mgr.EvolveTurretTier2(_selectedTurret.InstanceId);

            if (_evolveTier2Panel != null) _evolveTier2Panel.SetActive(false);

            // Restore whatever state we were in before evolve (Prep OR WaveActive)
            GameManager.Instance.PopModalState();
            RefreshInfo();
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            RefreshOpenPanel();
            if (_evolveTier2Panel != null && _evolveTier2Panel.activeSelf)
                RefreshTier2Affordability();
        }
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
            EventBus.Unsubscribe<TurretEvolvedEvent>(OnTurretEvolvedRefresh);
            EventBus.Unsubscribe<ShowEvolveChoiceEvent>(OnShowEvolve);
            EventBus.Unsubscribe<ShowEvolveTier2ChoiceEvent>(OnShowEvolveTier2);
            EventBus.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Unsubscribe<PrepPhaseStartedEvent>(OnPrepPhaseStarted);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }
    }
}
