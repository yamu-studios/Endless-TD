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

namespace ETD.UI
{
    public class SpecCardStatsUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _closeButton;

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

        private bool _isOpen;

        private void Awake()
        {
            _closeButton?.onClick.AddListener(Close);
            EnsureDraggable();
            if (_panel != null) _panel.SetActive(false);
        }

        private void Update()
        {
            if (KeybindingManager.GetKeyDown(KeybindAction.ToggleSpecCardStats))
                Toggle();
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

        private static string FormatValue(float v, StatFormat format) => format switch
        {
            StatFormat.Percent => $"+{v:F1}%",
            StatFormat.PercentNegative => $"-{v:F1}%",
            StatFormat.FlatInt => $"+{Mathf.RoundToInt(v)}",
            _ => $"+{v:F2}"
        };

        private enum StatFormat { Percent, PercentNegative, FlatInt, Raw }
    }

   
}