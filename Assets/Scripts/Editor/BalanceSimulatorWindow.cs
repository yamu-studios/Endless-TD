// ============================================================================
// ETD.Editor - BalanceSimulatorWindow.cs
// Wave-by-wave economy/difficulty simulator. Answers "at which wave does enemy
// EHP collide with the DPS a player can actually afford?" without pressing Play.
//
// Model (kept deliberately simple and inspectable):
// - Enemy side uses the REAL EnemyData assets and the REAL GetScaledHealth()
//   curve, wave budget formula, and tier multipliers.
// - Player side simulates one turret archetype (default: Tower_Basic numbers)
//   bought/upgraded greedily by marginal DPS-per-gold each wave. Traits, spec
//   cards, support auras and multi-turret variety are approximated by the
//   single "Player power multiplier" knob.
// - A wave is "cleared" if teamDPS >= waveEHP / clearWindowSeconds.
//
// This is a planning tool: it predicts where the curves collide, not exact
// player experience. Tune constants here first, then playtest.
// ============================================================================
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using ETD.Data;

namespace ETD.EditorTools
{
    public class BalanceSimulatorWindow : EditorWindow
    {
        // --- Wave/budget model (mirror WaveManager serialized defaults) ---
        private int _maxWave = 150;
        private int _baseBudget = 50;
        private int _budgetPerWave = 8;
        private float _budgetQuadraticScale = 0.3f;
        private float _baseSpawnInterval = 0.6f;
        private float _spawnIntervalReduction = 0.005f;
        private float _minSpawnInterval = 0.2f;
        private int _eliteEveryNWaves = 5;
        private int _bossEveryNWaves = 25;
        private int _eliteFrequencyDoubleWave = 75;

        // --- Economy model ---
        private float _goldKillWaveScale = 0.07f;
        private float _eliteGoldMult = 5f;
        private float _bossGoldMult = 25f;
        private int _startingGold = 60;
        private float _incomeMultiplier = 1f;

        // --- Player model (turret archetype; defaults = Tower_Basic) ---
        private float _turretBaseDps = 20f;      // 10 dmg / 0.5s interval
        private int _turretPlaceCost = 5;
        private int _upgradeBaseCost = 8;
        private float _upgradeCostMult = 1.325f;
        private float _damageGrowthPerLevel = 0.15f;
        private int _boardSlots = 60;
        private float _playerPowerMultiplier = 2f;

        // --- Clear condition ---
        private float _traversalSeconds = 20f;

        // --- Output ---
        private int _rowStep = 5;
        private readonly List<string> _rows = new();
        private string _verdict = "";
        private Vector2 _scroll;

        [MenuItem("ETD/Balance Simulator")]
        public static void Open()
        {
            GetWindow<BalanceSimulatorWindow>("Balance Simulator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Wave / Budget", EditorStyles.boldLabel);
            _maxWave = EditorGUILayout.IntField("Simulate To Wave", _maxWave);
            _baseBudget = EditorGUILayout.IntField("Base Budget", _baseBudget);
            _budgetPerWave = EditorGUILayout.IntField("Budget Per Wave", _budgetPerWave);
            _budgetQuadraticScale = EditorGUILayout.FloatField("Budget Quadratic", _budgetQuadraticScale);

            EditorGUILayout.LabelField("Economy", EditorStyles.boldLabel);
            _goldKillWaveScale = EditorGUILayout.FloatField("Gold Kill Wave Scale", _goldKillWaveScale);
            _startingGold = EditorGUILayout.IntField("Starting Gold", _startingGold);
            _incomeMultiplier = EditorGUILayout.FloatField("Income Multiplier (meta)", _incomeMultiplier);

            EditorGUILayout.LabelField("Player (turret archetype)", EditorStyles.boldLabel);
            _turretBaseDps = EditorGUILayout.FloatField("Turret Base DPS", _turretBaseDps);
            _turretPlaceCost = EditorGUILayout.IntField("Place Cost", _turretPlaceCost);
            _upgradeBaseCost = EditorGUILayout.IntField("Upgrade Base Cost", _upgradeBaseCost);
            _upgradeCostMult = EditorGUILayout.FloatField("Upgrade Cost Mult", _upgradeCostMult);
            _damageGrowthPerLevel = EditorGUILayout.FloatField("Damage Growth / Level", _damageGrowthPerLevel);
            _boardSlots = EditorGUILayout.IntField("Board Slots", _boardSlots);
            _playerPowerMultiplier = EditorGUILayout.FloatField("Player Power Multiplier", _playerPowerMultiplier);
            _traversalSeconds = EditorGUILayout.FloatField("Traversal Seconds", _traversalSeconds);
            _rowStep = Mathf.Max(1, EditorGUILayout.IntField("Print Every N Waves", _rowStep));

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run Simulation", GUILayout.Height(28)))
                    RunSimulation();

                if (_rows.Count > 0 && GUILayout.Button("Copy CSV", GUILayout.Height(28), GUILayout.Width(90)))
                    CopyCsv();
            }

            if (!string.IsNullOrEmpty(_verdict))
                EditorGUILayout.HelpBox(_verdict, MessageType.Info);

            if (_rows.Count > 0)
            {
                EditorGUILayout.LabelField(
                    "wave | enemies | perEnemyHP | waveEHP | income | cumGold | teamDPS | reqDPS | ratio",
                    EditorStyles.miniBoldLabel);

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                for (int i = 0; i < _rows.Count; i++)
                    EditorGUILayout.LabelField(_rows[i], EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
            }
        }

        // =====================================================================
        // SIMULATION
        // =====================================================================

        private void RunSimulation()
        {
            _rows.Clear();
            _verdict = "";

            List<EnemyData> normals = LoadEnemies(eliteOnly: false, bossOnly: false);
            List<EnemyData> elites = LoadEnemies(eliteOnly: true, bossOnly: false);
            List<EnemyData> bosses = LoadEnemies(eliteOnly: false, bossOnly: true);

            if (normals.Count == 0)
            {
                _verdict = "No normal EnemyData assets found.";
                return;
            }

            var turretLevels = new List<int>();
            double gold = _startingGold;
            double cumulativeGold = _startingGold;
            int firstWallWave = -1;
            int consecutiveWallWaves = 0;

            for (int wave = 1; wave <= _maxWave; wave++)
            {
                // ---- Enemy side ----
                double avgWeight = 0, avgHp = 0, avgGold = 0;
                int unlocked = 0;
                for (int i = 0; i < normals.Count; i++)
                {
                    EnemyData e = normals[i];
                    if (!e.IsAvailableAt(wave))
                        continue;

                    unlocked++;
                    avgWeight += Mathf.Max(1, e.SpawnWeight);
                    avgHp += e.GetScaledHealth(wave, EnemyTier.Normal);
                    avgGold += e.GoldReward * (1f + wave * _goldKillWaveScale);
                }

                if (unlocked == 0)
                    continue;

                avgWeight /= unlocked;
                avgHp /= unlocked;
                avgGold /= unlocked;

                int budget = _baseBudget + wave * _budgetPerWave
                    + Mathf.RoundToInt(wave * wave * _budgetQuadraticScale);
                int count = Mathf.Max(1, (int)(budget / avgWeight));

                double waveEhp = count * avgHp;
                double waveGold = count * avgGold;

                // Elites / bosses on their schedules
                if (wave % _eliteEveryNWaves == 0 && wave % _bossEveryNWaves != 0 && elites.Count > 0)
                {
                    int eliteCount = 1 + wave / 15;
                    if (wave >= _eliteFrequencyDoubleWave) eliteCount *= 2;
                    double eliteHp = AverageScaledHp(elites, wave, EnemyTier.Elite);
                    double eliteGold = AverageBaseGold(elites) * (1f + wave * _goldKillWaveScale) * _eliteGoldMult;
                    waveEhp += eliteCount * eliteHp;
                    waveGold += eliteCount * eliteGold;
                }

                if (wave % _bossEveryNWaves == 0 && bosses.Count > 0)
                {
                    double bossHp = AverageScaledHp(bosses, wave, EnemyTier.Boss);
                    double bossGold = AverageBaseGold(bosses) * (1f + wave * _goldKillWaveScale) * _bossGoldMult;
                    waveEhp += bossHp;
                    waveGold += bossGold;
                }

                // ---- Income ----
                waveGold *= _incomeMultiplier;

                gold += waveGold;
                cumulativeGold += waveGold;

                // ---- Player spends greedily on best marginal DPS per gold ----
                gold = SpendGold(gold, turretLevels);
                double teamDps = TeamDps(turretLevels) * _playerPowerMultiplier;

                // ---- Clear condition ----
                float interval = Mathf.Max(_minSpawnInterval, _baseSpawnInterval - wave * _spawnIntervalReduction);
                double clearWindow = count * interval + _traversalSeconds;
                double requiredDps = waveEhp / System.Math.Max(1.0, clearWindow);
                double ratio = teamDps > 0 ? requiredDps / teamDps : double.PositiveInfinity;

                if (ratio > 1.0)
                {
                    consecutiveWallWaves++;
                    if (firstWallWave < 0 && consecutiveWallWaves >= 3)
                        firstWallWave = wave - 2;
                }
                else
                {
                    consecutiveWallWaves = 0;
                }

                if (wave % _rowStep == 0 || wave == 1 || ratio > 1.0)
                {
                    string status = ratio > 1.0 ? "WALL" : ratio > 0.7 ? "PRESSURE" : "ok";
                    _rows.Add(
                        $"w{wave,4} | {count,4} | {Fmt(avgHp),10} | {Fmt(waveEhp),11} | {Fmt(waveGold),8} | {Fmt(cumulativeGold),9} | {Fmt(teamDps),9} | {Fmt(requiredDps),9} | {ratio,5:0.00} {status}");
                }
            }

            _verdict = firstWallWave > 0
                ? $"Sustained wall begins around wave {firstWallWave} (required DPS exceeds affordable team DPS for 3+ consecutive waves)."
                : $"No sustained wall up to wave {_maxWave} — enemy curve may be too soft, or player multiplier too generous.";
        }

        /// <summary>
        /// Greedy purchasing: repeatedly buy whichever is cheapest per point of
        /// DPS — a new turret (while board slots remain) or the single upgrade
        /// with the best marginal value. Mirrors how strong players actually
        /// optimize, which is what balance must be measured against.
        /// </summary>
        private double SpendGold(double gold, List<int> turretLevels)
        {
            while (true)
            {
                double bestCostPerDps = double.MaxValue;
                int bestAction = -1; // -2 = place, >=0 = upgrade index
                double bestCost = 0;

                if (turretLevels.Count < _boardSlots && _turretPlaceCost <= gold)
                {
                    double costPerDps = _turretPlaceCost / (double)_turretBaseDps;
                    if (costPerDps < bestCostPerDps)
                    {
                        bestCostPerDps = costPerDps;
                        bestAction = -2;
                        bestCost = _turretPlaceCost;
                    }
                }

                for (int i = 0; i < turretLevels.Count; i++)
                {
                    int level = turretLevels[i];
                    double cost = _upgradeBaseCost * System.Math.Pow(_upgradeCostMult, level - 1);
                    if (cost > gold)
                        continue;

                    double deltaDps = _turretBaseDps
                        * System.Math.Pow(1.0 + _damageGrowthPerLevel, level - 1)
                        * _damageGrowthPerLevel;
                    double costPerDps = cost / System.Math.Max(0.0001, deltaDps);
                    if (costPerDps < bestCostPerDps)
                    {
                        bestCostPerDps = costPerDps;
                        bestAction = i;
                        bestCost = cost;
                    }
                }

                if (bestAction == -1)
                    return gold; // nothing affordable

                gold -= bestCost;
                if (bestAction == -2)
                    turretLevels.Add(1);
                else
                    turretLevels[bestAction]++;
            }
        }

        private double TeamDps(List<int> turretLevels)
        {
            double total = 0;
            for (int i = 0; i < turretLevels.Count; i++)
                total += _turretBaseDps * System.Math.Pow(1.0 + _damageGrowthPerLevel, turretLevels[i] - 1);
            return total;
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private static List<EnemyData> LoadEnemies(bool eliteOnly, bool bossOnly)
        {
            var result = new List<EnemyData>();
            string[] guids = AssetDatabase.FindAssets("t:EnemyData");
            for (int i = 0; i < guids.Length; i++)
            {
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (data == null)
                    continue;

                if (data.IsEliteOnly == eliteOnly && data.IsBossOnly == bossOnly)
                    result.Add(data);
            }
            return result;
        }

        private static double AverageScaledHp(List<EnemyData> pool, int wave, EnemyTier tier)
        {
            if (pool.Count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < pool.Count; i++)
                sum += pool[i].GetScaledHealth(wave, tier);
            return sum / pool.Count;
        }

        private static double AverageBaseGold(List<EnemyData> pool)
        {
            if (pool.Count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < pool.Count; i++)
                sum += pool[i].GoldReward;
            return sum / pool.Count;
        }

        private static string Fmt(double v)
        {
            if (v >= 1_000_000_000) return (v / 1_000_000_000).ToString("0.##") + "B";
            if (v >= 1_000_000) return (v / 1_000_000).ToString("0.##") + "M";
            if (v >= 10_000) return (v / 1_000).ToString("0.#") + "K";
            return v.ToString("0");
        }

        private void CopyCsv()
        {
            var sb = new StringBuilder(_rows.Count * 64);
            sb.AppendLine("wave,enemies,perEnemyHP,waveEHP,income,cumGold,teamDPS,requiredDPS,ratio");
            for (int i = 0; i < _rows.Count; i++)
                sb.AppendLine(_rows[i].Replace(" ", "").Replace("|", ","));
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
        }
    }
}
