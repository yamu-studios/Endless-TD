// ============================================================================
// ETD.Gameplay - RunDamageStatsTracker.cs
// Collects end-game damage breakdown by turret family.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using ETD.Core;
using ETD.Data;
using ETD.Enemies;

namespace ETD.Gameplay
{
    public sealed class RunDamageStatsTracker : MonoBehaviour, IDamageStatsSink
    {
        [Serializable]
        public sealed class DamageBucket
        {
            public float Total;
            public float Normal;
            public float Critical;
            public float Pure;
            public float Burn;
            public float Chain;
            public float Laser;
            public float Area;
            public float Bonus;
        }

        public readonly struct TurretDamageStat
        {
            public readonly TurretType Type;
            public readonly float Amount;

            public TurretDamageStat(TurretType type, float amount)
            {
                Type = type;
                Amount = amount;
            }
        }

        // Keep this driven by the enum so newly-added turret families are recorded
        // automatically. Families that deal no damage simply produce no row.
        private static readonly TurretType[] DisplayTurretTypes =
            (TurretType[])Enum.GetValues(typeof(TurretType));

        private readonly Dictionary<int, DamageBucket> _byTurretType = new();
        private readonly float[] _byTurretFamily = new float[Enum.GetValues(typeof(TurretType)).Length];
        private float _totalDamage;
        private bool _registered;

        public static RunDamageStatsTracker Active { get; private set; }

        public float TotalDamage => _totalDamage;
        public IReadOnlyDictionary<int, DamageBucket> ByTurretType => _byTurretType;

        public void Initialize()
        {
            ResetStats();
            Register();
        }

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
        }

        private void Register()
        {
            if (_registered)
                return;

            // Prevent duplicate scene/runtime trackers from overwriting each other.
            // The old v9 warning came from a scene tracker registering, then
            // RunManager.AddComponent creating a second tracker and overwriting it.
            if (ServiceLocator.TryGet(out RunDamageStatsTracker existingTracker)
                && existingTracker != null
                && existingTracker != this)
            {
                Active = existingTracker;
                EnemyController.InvalidateDamageStatsSink();
                return;
            }

            if (ServiceLocator.TryGet(out IDamageStatsSink existingSink)
                && existingSink != null
                && !ReferenceEquals(existingSink, this))
            {
                EnemyController.InvalidateDamageStatsSink();
                return;
            }

            ServiceLocator.Register<IDamageStatsSink>(this);
            ServiceLocator.Register(this);
            Active = this;
            EnemyController.InvalidateDamageStatsSink();
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            // Only the instance that is currently registered is allowed to unregister.
            // This avoids a disabled duplicate removing the active run tracker.
            if (ServiceLocator.TryGet(out IDamageStatsSink sink) && ReferenceEquals(sink, this))
                ServiceLocator.Unregister<IDamageStatsSink>();

            if (ServiceLocator.TryGet(out RunDamageStatsTracker tracker) && tracker == this)
                ServiceLocator.Unregister<RunDamageStatsTracker>();

            if (Active == this)
                Active = null;

            EnemyController.InvalidateDamageStatsSink();
            _registered = false;
        }

        public void ResetStats()
        {
            _totalDamage = 0f;
            _byTurretType.Clear();
            Array.Clear(_byTurretFamily, 0, _byTurretFamily.Length);
        }

        public void OnDamageDealt(int sourceTurretType, int sourceTurretId, int damageKind, float amount)
        {
            if (amount <= 0f || !TryGetDisplayTurretType(sourceTurretType, out TurretType turretType))
                return;

            _totalDamage += amount;

            int turretIndex = (int)turretType;
            if ((uint)turretIndex < (uint)_byTurretFamily.Length)
                _byTurretFamily[turretIndex] += amount;

            if (!_byTurretType.TryGetValue(turretIndex, out DamageBucket bucket))
            {
                bucket = new DamageBucket();
                _byTurretType[turretIndex] = bucket;
            }

            DamageNumberKind kind = ToDamageKind(damageKind);
            bucket.Total += amount;
            AddToBucket(bucket, kind, amount);
        }

        public void GetTurretDamageStats(List<TurretDamageStat> results, float minDamage = 0.5f)
        {
            if (results == null)
                return;

            results.Clear();
            for (int i = 0; i < DisplayTurretTypes.Length; i++)
            {
                TurretType type = DisplayTurretTypes[i];
                int index = (int)type;
                if ((uint)index >= (uint)_byTurretFamily.Length)
                    continue;

                float value = _byTurretFamily[index];
                if (value > minDamage)
                    results.Add(new TurretDamageStat(type, value));
            }
        }

        public float GetDamageByTurretType(TurretType type)
        {
            int index = (int)type;
            return (uint)index < (uint)_byTurretFamily.Length ? _byTurretFamily[index] : 0f;
        }

        public string BuildLocalizedSummary(int maxTurretRows = int.MaxValue)
        {
            var sb = new StringBuilder(256);
            sb.AppendLine(LocalizationManager.Get("end_stats_damage_title", "Damage Stats"));
            sb.AppendLine(LocalizationManager.GetFormat(
                "end_stats_total_damage_format",
                "Total Damage: {0}",
                FormatNumber(_totalDamage)));

            if (_totalDamage <= 0.5f)
            {
                sb.AppendLine(LocalizationManager.Get("end_stats_no_damage", "No turret damage recorded."));
                return sb.ToString();
            }

            var rows = ListPool.Get();
            for (int i = 0; i < DisplayTurretTypes.Length; i++)
            {
                TurretType type = DisplayTurretTypes[i];
                float value = GetDamageByTurretType(type);
                if (value > 0.5f)
                    rows.Add(new Row(type, value));
            }

            rows.Sort((a, b) => b.Total.CompareTo(a.Total));

            int count = Mathf.Min(maxTurretRows, rows.Count);
            for (int i = 0; i < count; i++)
            {
                Row row = rows[i];
                sb.AppendLine(LocalizationManager.GetFormat(
                    "end_stats_turret_damage_format",
                    "{0}: {1}",
                    GetTurretDamageName(row.Type),
                    FormatNumber(row.Total)));
            }

            ListPool.Release(rows);
            return sb.ToString();
        }

        private static bool TryGetDisplayTurretType(int sourceTurretType, out TurretType turretType)
        {
            turretType = default;
            if (!Enum.IsDefined(typeof(TurretType), sourceTurretType))
                return false;

            turretType = (TurretType)sourceTurretType;
            return true;
        }

        private static void AddToBucket(DamageBucket bucket, DamageNumberKind kind, float amount)
        {
            switch (kind)
            {
                case DamageNumberKind.Critical: bucket.Critical += amount; break;
                case DamageNumberKind.Pure: bucket.Pure += amount; break;
                case DamageNumberKind.Burn: bucket.Burn += amount; break;
                case DamageNumberKind.Chain: bucket.Chain += amount; break;
                case DamageNumberKind.Laser: bucket.Laser += amount; break;
                case DamageNumberKind.Area: bucket.Area += amount; break;
                case DamageNumberKind.Bonus: bucket.Bonus += amount; break;
                default: bucket.Normal += amount; break;
            }
        }

        private static DamageNumberKind ToDamageKind(int damageKind)
        {
            return Enum.IsDefined(typeof(DamageNumberKind), damageKind)
                ? (DamageNumberKind)damageKind
                : DamageNumberKind.Normal;
        }

        public static string GetTurretDamageName(TurretType type)
        {
            string key = type switch
            {
                TurretType.Basic => "end_stats_damage_basic",
                TurretType.Frost => "end_stats_damage_frost",
                TurretType.Inferno => "end_stats_damage_inferno",
                TurretType.Laser => "end_stats_damage_laser",
                TurretType.Lightning => "end_stats_damage_lightning",
                TurretType.Void => "end_stats_damage_void",
                TurretType.Toxin => "end_stats_damage_toxin",
                TurretType.Railgun => "end_stats_damage_railgun",
                _ => "end_stats_damage_unknown"
            };

            string fallback = type switch
            {
                TurretType.Basic => "Normal / Basic",
                TurretType.Frost => "Frost",
                TurretType.Inferno => "Inferno",
                TurretType.Laser => "Laser",
                TurretType.Lightning => "Lightning",
                TurretType.Void => "Void",
                TurretType.Toxin => "Toxin",
                TurretType.Railgun => "Railgun",
                _ => type.ToString()
            };

            return LocalizationManager.Get(key, fallback);
        }

        public static string FormatNumber(float value)
        {
            // Delegate to the canonical formatter (adds T/Qa..Dc + scientific + NaN/Inf safety).
            return ETD.Core.NumberFormat.Compact(value);
        }

        private readonly struct Row
        {
            public readonly TurretType Type;
            public readonly float Total;

            public Row(TurretType type, float total)
            {
                Type = type;
                Total = total;
            }
        }

        private static class ListPool
        {
            private static readonly Stack<List<Row>> Pool = new();

            public static List<Row> Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new List<Row>(DisplayTurretTypes.Length);
            }

            public static void Release(List<Row> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
