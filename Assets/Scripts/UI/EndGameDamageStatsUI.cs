// ============================================================================
// ETD.UI - EndGameDamageStatsUI.cs
// Polished end-run damage breakdown with icons, labels, values and bars.
// Shows every turret family that dealt damage during the run.
// ============================================================================
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ETD.Core;
using ETD.Data;
using ETD.Gameplay;

namespace ETD.UI
{
    public sealed class EndGameDamageStatsUI : MonoBehaviour
    {
        [Serializable]
        public sealed class TurretDamageIcon
        {
            public TurretType Type;
            public Sprite Icon;
        }

        [Header("Text")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _totalDamageText;
        [SerializeField] private TMP_Text _emptyText;

        [Header("Rows")]
        [SerializeField] private RectTransform _rowContainer;
        [SerializeField] private EndGameDamageStatRowUI _rowPrefab;
        [SerializeField] private EndGameDamageStatRowUI[] _staticRows;
        [SerializeField] private float _hideBelowDamage = 0.5f;

        [Header("Turret Family Icons")]
        [SerializeField] private TurretDamageIcon[] _icons;

        private readonly List<EndGameDamageStatRowUI> _runtimeRows = new(8);
        private readonly List<RunDamageStatsTracker.TurretDamageStat> _stats = new(8);

        public void Show(RunDamageStatsTracker tracker)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (_titleText != null)
                _titleText.text = LocalizationManager.Get("end_stats_damage_title", "Damage Stats");

            if (tracker == null || tracker.TotalDamage <= _hideBelowDamage)
            {
                SetTotal(0f);
                HideAllRows();
                if (_emptyText != null)
                {
                    _emptyText.gameObject.SetActive(true);
                    _emptyText.text = LocalizationManager.Get("end_stats_no_damage", "No turret damage recorded.");
                }
                return;
            }

            SetTotal(tracker.TotalDamage);
            if (_emptyText != null)
                _emptyText.gameObject.SetActive(false);

            tracker.GetTurretDamageStats(_stats, _hideBelowDamage);
            _stats.Sort((a, b) => b.Amount.CompareTo(a.Amount));

            int rowCount = _stats.Count;
            float maxDamage = rowCount > 0 ? Mathf.Max(1f, _stats[0].Amount) : 1f;

            for (int i = 0; i < rowCount; i++)
            {
                var stat = _stats[i];
                EndGameDamageStatRowUI row = GetRow(i);
                if (row == null)
                    continue;

                row.Set(
                    GetIcon(stat.Type),
                    RunDamageStatsTracker.GetTurretDamageName(stat.Type),
                    RunDamageStatsTracker.FormatNumber(stat.Amount),
                    stat.Amount / maxDamage);
            }

            HideRowsFrom(rowCount);
            _stats.Clear();
        }

        public void Clear()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            SetTotal(0f);
            HideAllRows();
            if (_emptyText != null)
                _emptyText.gameObject.SetActive(false);
        }

        private void SetTotal(float totalDamage)
        {
            if (_totalDamageText == null)
                return;

            _totalDamageText.text = LocalizationManager.GetFormat(
                "end_stats_total_damage_format",
                "Total Damage: {0}",
                RunDamageStatsTracker.FormatNumber(totalDamage));
        }

        private EndGameDamageStatRowUI GetRow(int index)
        {
            if (_staticRows != null && index < _staticRows.Length && _staticRows[index] != null)
                return _staticRows[index];

            if (_rowPrefab == null || _rowContainer == null)
                return null;

            while (_runtimeRows.Count <= index)
            {
                EndGameDamageStatRowUI row = Instantiate(_rowPrefab, _rowContainer);
                _runtimeRows.Add(row);
            }

            return _runtimeRows[index];
        }

        private void HideRowsFrom(int startIndex)
        {
            if (_staticRows != null)
            {
                for (int i = startIndex; i < _staticRows.Length; i++)
                    _staticRows[i]?.Hide();
            }

            for (int i = startIndex; i < _runtimeRows.Count; i++)
                _runtimeRows[i]?.Hide();
        }

        private void HideAllRows() => HideRowsFrom(0);

        private Sprite GetIcon(TurretType type)
        {
            if (_icons == null)
                return null;

            for (int i = 0; i < _icons.Length; i++)
            {
                if (_icons[i] != null && _icons[i].Type == type)
                    return _icons[i].Icon;
            }

            return null;
        }
    }
}
