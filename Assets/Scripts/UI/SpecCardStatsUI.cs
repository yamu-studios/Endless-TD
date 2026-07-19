// ============================================================================
// ETD.UI - SpecCardStatsUI.cs  [REWRITTEN]
// Tab key opens/closes. Groups related effects into one stat row each.
// Per-wave bonuses show current effective value (rate � currentWave).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Data;
using ETD.Gameplay;
using ETD.Waves;
using ETD.Traits;

namespace ETD.UI
{
    public class SpecCardStatsUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _closeButton;

        [Tooltip("Opens the panel on click. The panel was previously Tab-key-only with no " +
                 "clickable way in, which left gamepad-only players unable to ever open it.")]
        [SerializeField] private Button _openButton;

        [Header("Drag")]
        [SerializeField] private bool _enableDragging = true;
        [SerializeField] private RectTransform _dragHandle;
        [SerializeField] private string _dragSaveId = "speccard_stat_panel";

        [Header("Header")]
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _cardCountText;

        [Header("List")]
        [SerializeField] private Transform _container;
        [SerializeField] private GameObject _rowPrefab;

        [Header("Section Header Prefab")]
        [SerializeField] private GameObject _sectionHeaderPrefab;

        [Header("Colors")]
        [SerializeField] private Color _positiveColor = new Color(0.3f, 1f, 0.4f);
        [SerializeField] private Color _negativeColor = new Color(1f, 0.35f, 0.35f);
        [SerializeField] private Color _specialColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color _labelColor = new Color(0.8f, 0.8f, 0.8f);

        [Tooltip("Distinct color for trait rows so they stand out from spec-card stats.")]
        [SerializeField] private Color _traitColor = new Color(0.62f, 0.71f, 1f);

        [Header("Trait Section")]
        [Tooltip("If true, active traits (including run-dynamic ones like damage-per-gold-spent) are listed with their CURRENT effective value.")]
        [SerializeField] private bool _showTraits = true;

        [Tooltip("Seconds between live refreshes while the panel is open, so dynamic trait values update. 0 = only refresh on open.")]
        [SerializeField] private float _liveRefreshInterval = 0.75f;

        private bool _isOpen;
        private float _nextLiveRefreshTime;

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
            else
                PanelCloseButton.Ensure(_panel, Close);

            _openButton?.onClick.AddListener(Toggle);

            EnsureDraggable();
            if (_panel != null) _panel.SetActive(false);
        }

        private void Update()
        {
            if (KeybindingManager.GetKeyDown(KeybindAction.ToggleSpecCardStats))
                Toggle();

            // Live-refresh while open so run-dynamic traits (damage per gold spent,
            // per owned turret, per wave) show their current effective value.
            if (_isOpen && _liveRefreshInterval > 0f && Time.unscaledTime >= _nextLiveRefreshTime)
            {
                _nextLiveRefreshTime = Time.unscaledTime + _liveRefreshInterval;
                Refresh();
            }
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

        // =================================================================
        // OPEN / CLOSE / TOGGLE
        // =================================================================

        public void Toggle() { if (_isOpen) Close(); else Open(); }

        public void Open()
        {
            _isOpen = true;
            _nextLiveRefreshTime = Time.unscaledTime + _liveRefreshInterval;
            if (_panel != null) _panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            _isOpen = false;
            if (_panel != null) _panel.SetActive(false);
        }

        // =================================================================
        // REFRESH
        // =================================================================

        private void Refresh()
        {
            if (_container == null || _rowPrefab == null) return;

            for (int i = _container.childCount - 1; i >= 0; i--)
                Destroy(_container.GetChild(i).gameObject);

            if (!ServiceLocator.TryGet<RunManager>(out var runManager)) return;
            var runData = runManager.RunData;
            if (runData == null) return;

            int currentWave = 0;
            if (ServiceLocator.TryGet<WaveManager>(out var wm))
                currentWave = wm.CurrentWave;

            if (_levelText != null) _levelText.text = LocalizationManager.GetFormat("speccardstat_level_format", "Level {0}", runData.Level);
            if (_cardCountText != null) _cardCountText.text = $"{LocalizationManager.Get("speccardstat_cards_collected", "Cards Collected")}: {runData.SpecCardsChosen}";

            float Get(SpecCardEffectType t) => runData.GetSpecBonus(t);

            // =============================================================
            // SECTION: TRAITS (shown first; run-dynamic values are live)
            // =============================================================
            if (_showTraits)
                AddTraitRows(runData, currentWave);

            // =============================================================
            // SECTION: OFFENSE
            // =============================================================
           // AddSection(LocalizationManager.Get("speccardstat_section_offense", "OFFENSE"));

            // Damage (base + projectile combined). Wave scaling is exponential in gameplay,
            // so show the effective current-wave bonus instead of old linear value.
            float totalDmg = Get(SpecCardEffectType.DamagePercent)
                           + Get(SpecCardEffectType.ProjectileDamage)
                           + GetEffectiveWaveBonus(Get(SpecCardEffectType.DamagePerWave), currentWave);
            AddRow("speccardstat_damage", "Damage", totalDmg, StatFormat.Percent, _positiveColor);

            // Attack Speed (flat + per-wave + per-nearby).
            // Per-nearby-enemy is context-dependent; show base rate accumulated.
            float totalAS = Get(SpecCardEffectType.AttackSpeedPercent)
                          + GetEffectiveWaveBonus(Get(SpecCardEffectType.AttackSpeedPerWave), currentWave)
                          + Get(SpecCardEffectType.AttackSpeedPerNearbyEnemy); // show as base %
            AddRow("speccardstat_attack_speed", "Attack Speed", totalAS, StatFormat.Percent, _positiveColor);

            // Range
            AddRow("speccardstat_range", "Range", Get(SpecCardEffectType.RangePercent), StatFormat.Percent, _positiveColor);

            // Crit Chance (regular + strong combined)
            float totalCrit = Get(SpecCardEffectType.CritChance) + Get(SpecCardEffectType.CritChanceStrong);
            AddRow("speccardstat_crit_chance", "Crit Chance", totalCrit, StatFormat.Percent, _positiveColor);

            // =============================================================
            // SECTION: STATUS
            // =============================================================
          //  AddSection(LocalizationManager.Get("speccardstat_section_status", "STATUS"));

            // Burn Damage
            float totalBurn = Get(SpecCardEffectType.BurnDamage) + Get(SpecCardEffectType.BurnDamageStrong);
            AddRow("speccardstat_burn_damage", "Burn Damage", totalBurn, StatFormat.Percent, _positiveColor);

            // Slow (duration + strength combined)
            float totalSlow = Get(SpecCardEffectType.SlowDuration) + Get(SpecCardEffectType.SlowStrength);
            AddRow("speccardstat_slow_effect", "Slow Effect", totalSlow, StatFormat.Percent, _positiveColor);

            // Chain Damage
            AddRow("speccardstat_chain_damage", "Chain Damage", Get(SpecCardEffectType.ChainDamage), StatFormat.Percent, _positiveColor);

            // Chain Range
            AddRow("speccardstat_chain_range", "Chain Range", Get(SpecCardEffectType.ChainRange), StatFormat.Percent, _positiveColor);

            // Chain Targets (flat bonus)
            AddRow("speccardstat_chain_targets", "Chain Targets", Get(SpecCardEffectType.ChainTargetBonus), StatFormat.FlatInt, _positiveColor);

            // Support / Aura Range (combined)
            float totalSupport = Get(SpecCardEffectType.AuraRange) + Get(SpecCardEffectType.SupportRadius);
            AddRow("speccardstat_support_range", "Support Range", totalSupport, StatFormat.Percent, _positiveColor);

            // Freeze Amplifier
            AddRow("speccardstat_freeze_amplifier", "Freeze Amplifier", Get(SpecCardEffectType.FreezeAmplifier), StatFormat.Percent, _positiveColor);

            // =============================================================
            // SECTION: CONDITIONAL
            // =============================================================
           // AddSection(LocalizationManager.Get("speccardstat_section_conditional", "CONDITIONAL"));

            AddRow("speccardstat_dmg_vs_elite", "vs Elite Damage", Get(SpecCardEffectType.DamageVsElite), StatFormat.Percent, _specialColor);
            AddRow("speccardstat_dmg_vs_burning", "vs Burning Enemy Damage", Get(SpecCardEffectType.DamageVsBurning), StatFormat.Percent, _specialColor);
            AddRow("speccardstat_dmg_vs_slowed", "vs Slowed/Frozen Damage", Get(SpecCardEffectType.DamageVsSlowedFrozen), StatFormat.Percent, _specialColor);

            // Damage per Gold (both variants summed as effective stacks)
            float totalGoldDmg = Get(SpecCardEffectType.DamagePerGoldHeld)
                               + Get(SpecCardEffectType.DamagePerGoldHeldStrong);
            AddRow("speccardstat_dmg_per_gold", "Damage per 1000 Gold", totalGoldDmg, StatFormat.Percent, _specialColor);

            AddRow("speccardstat_dmg_per_distance", "Damage per Tile Distance", Get(SpecCardEffectType.DamagePerDistance), StatFormat.Percent, _specialColor);

            // Shock Chance
            AddRow("speccardstat_shock_chance", "Shock Chance", Get(SpecCardEffectType.ShockChance), StatFormat.Percent, _specialColor);

            // =============================================================
            // SECTION: SPECIAL MECHANICS
            // =============================================================
           // AddSection(LocalizationManager.Get("speccardstat_section_special", "SPECIAL"));

            AddRowSpecial("speccardstat_death_explosion", "Death Explosion", Get(SpecCardEffectType.DeathExplosion));
            AddRowSpecial("speccardstat_burn_spread", "Burn Spread on Death", Get(SpecCardEffectType.BurnSpreadOnDeath));
            AddRowSpecial("speccardstat_extra_projectile", "Extra Projectile", Get(SpecCardEffectType.ExtraProjectilePeriodic));

            // =============================================================
            // SECTION: ECONOMY
            // =============================================================
         //   AddSection(LocalizationManager.Get("speccardstat_section_economy", "ECONOMY"));

            AddRow("speccardstat_gold_gain", "Gold Gain", Get(SpecCardEffectType.GoldGain), StatFormat.Percent, _positiveColor);
            AddRow("speccardstat_upgrade_cost", "Upgrade Cost", Get(SpecCardEffectType.UpgradeCostReduce), StatFormat.PercentNegative, _positiveColor);

            // =============================================================
            // SECTION: LIVES
            // =============================================================
            float totalHeals = Get(SpecCardEffectType.HealHealth);
            if (totalHeals > 0)
            {
               // AddSection(LocalizationManager.Get("speccardstat_section_lives", "LIVES"));
                AddRow("speccardstat_lives_restored", "Lives Restored (total)", totalHeals, StatFormat.FlatInt, _positiveColor);
            }
        }

        // =================================================================
        // ROW BUILDERS
        // =================================================================

        private float GetEffectiveWaveBonus(float perWaveBonus, int wave)
        {
            if (perWaveBonus <= 0f || wave <= 0)
                return 0f;

            float normalized = perWaveBonus > 1f ? perWaveBonus * 0.01f : perWaveBonus;
            return Mathf.Pow(1f + normalized, wave) - 1f;
        }

        private void AddSection(string title)
        {
            if (_sectionHeaderPrefab == null) return;
            var go = Instantiate(_sectionHeaderPrefab, _container);
            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = title;
        }

        // =================================================================
        // TRAITS — active traits with their CURRENT effective value.
        // Dynamic traits (per gold spent / per owned turret / per wave) are
        // computed against live run state, matching RunStatModifiers, so the
        // panel reflects how strong the trait is *right now*.
        // =================================================================

        private void AddTraitRows(RunData runData, int wave)
        {
            var tm = TraitManager.Instance;
            if (tm == null) return;

            var traits = tm.ActiveTraits;
            if (traits == null || traits.Count == 0) return;

            AddSection(LocalizationManager.Get("speccardstat_section_traits", "TRAITS"));

            for (int i = 0; i < traits.Count; i++)
            {
                TraitData trait = traits[i];
                if (trait == null) continue;

                string label = SOLocalization.GetName(
                    "trait_" + trait.LocalizationKey, trait.DisplayName);
                string valueText = FormatTraitValue(trait, runData, wave);

                var go = Instantiate(_rowPrefab, _container);
                var row = go.GetComponent<SpecCardStatRow>();
                if (row == null) row = go.AddComponent<SpecCardStatRow>();
                // Both the trait NAME and its value use the trait color so trait
                // rows are clearly distinct from spec-card stat rows.
                row.Setup(label, valueText, _traitColor, _traitColor);
            }
        }

        private static float Norm(float value) => value > 1f ? value * 0.01f : value;

        private string FormatTraitValue(TraitData trait, RunData runData, int wave)
        {
            // Effective value (base * permanent shop upgrades) — matches what
            // RunStatModifiers actually applies, so upgraded traits are visible here.
            float effectValue = BalanceDescriptionFormatter.GetDisplayEffectValue(trait);

            switch (trait.EffectType)
            {
                // Damage per 100 gold spent — grows as the player spends.
                case TraitEffectType.DamagePerGoldSpent:
                {
                    float cur = Norm(effectValue) * (runData.TotalGoldSpent / 100f);
                    return $"+{SmartPercent(cur * 100f)}%";
                }

                // Damage per owned turret — grows as turrets are placed.
                case TraitEffectType.DamagePerOwnedTurret:
                {
                    float cur = Norm(effectValue) * runData.TurretsPlaced;
                    return $"+{SmartPercent(cur * 100f)}%";
                }

                // All stats per wave — exponential, matches GetWaveScalingMultiplier.
                case TraitEffectType.AllStatsPerWave:
                {
                    float cur = Mathf.Pow(1f + Norm(effectValue), Mathf.Max(0, wave)) - 1f;
                    return $"+{SmartPercent(cur * 100f)}%";
                }

                // Flat counts.
                case TraitEffectType.ChainTargetBonus:
                    return $"+{Mathf.RoundToInt(effectValue)}";

                // Everything else: static percent effect.
                default:
                    return $"+{SmartPercent(Norm(effectValue) * 100f)}%";
            }
        }

        // Adaptive precision so tiny-but-nonzero trait values never show as "0".
        // >=1 keeps 3 decimals so upgrade steps (1% -> 1.15% -> 1.75%) stay visible.
        private static string SmartPercent(float p)
        {
            float a = Mathf.Abs(p);
            if (a < 1e-7f) return "0";
            if (a >= 1f) return p.ToString("0.###");
            if (a >= 0.01f) return p.ToString("0.####");
            return p.ToString("0.######");
        }

        private void AddRow(string locKey, string fallbackLabel, float value,
            StatFormat format, Color valueColor)
        {
            if (Mathf.Abs(value) < 0.0001f) return; // skip zero rows

            var go = Instantiate(_rowPrefab, _container);
            var row = go.GetComponent<SpecCardStatRow>();
            if (row == null) row = go.AddComponent<SpecCardStatRow>();

            string label = LocalizationManager.Get(locKey, fallbackLabel);
            string valueText = FormatValue(value, format);
            row.Setup(label, valueText, _labelColor, valueColor);
        }

        private void AddRowSpecial(string locKey, string fallbackLabel, float value)
        {
            if (value <= 0.0001f) return;
            var go = Instantiate(_rowPrefab, _container);
            var row = go.GetComponent<SpecCardStatRow>();
            if (row == null) row = go.AddComponent<SpecCardStatRow>();
            string label = LocalizationManager.Get(locKey, fallbackLabel);
            row.Setup(label, "Active", _labelColor, _specialColor);
        }

        // Spec bonuses are stored normalized as fractions (0.1 = 10%), so a percent
        // row converts with x100. Sums here can legitimately exceed 1.0 (e.g. +250%
        // damage), so we must NOT apply the ">1 means already-a-percent" normalization
        // used for single raw data values in BalanceDescriptionFormatter.
        private static string FormatValue(float v, StatFormat format) => format switch
        {
            StatFormat.Percent => $"+{v * 100f:0.##}%",
            StatFormat.PercentNegative => $"-{v * 100f:0.##}%",
            StatFormat.FlatInt => $"+{Mathf.RoundToInt(v)}",
            _ => $"+{v:F2}"
        };

        private enum StatFormat { Percent, PercentNegative, FlatInt, Raw }
    }

   
}