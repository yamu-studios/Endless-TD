// ============================================================================
// ETD.Editor - ETDDevToolsWindow.cs
// Play-mode testing cheats (wave jump, infinite gold/lives, crystals) plus a
// balance simulator that evaluates the SAME scaling code the game runs
// (EnemyData.GetScaledHealth / GetHealthGrowthForWave).
// Menu: Tools > ETD > Dev Tools
// ============================================================================
using UnityEditor;
using UnityEngine;
using ETD.Core;
using ETD.Data;
using ETD.Gameplay;
using ETD.Turrets;
using ETD.Waves;

namespace ETD.EditorTools
{
    public class ETDDevToolsWindow : EditorWindow
    {
        private int _tab;
        private static readonly string[] Tabs = { "Cheats", "Balance Sim" };

        // --- Cheats state ---
        private int _targetWave = 100;
        private bool _infiniteGold;
        private bool _infiniteLives;
        private int _crystalsToAdd = 1000;
        private const int InfiniteGoldAmount = int.MaxValue;

        // --- Achievement test state ---
        private string _achApiName = "ACH_FIRST_TRAIT";

        // --- Balance sim state ---
        private EnemyData _simEnemy;
        private float _referenceDps = 1000f;
        private static readonly int[] SimWaves = { 1, 25, 50, 100, 150, 200, 250, 300 };
        private Vector2 _scroll;

        [MenuItem("Tools/ETD/Dev Tools")]
        public static void Open() => GetWindow<ETDDevToolsWindow>("ETD Dev Tools");

        private void OnEnable() => EditorApplication.update += OnEditorUpdate;
        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (Application.isPlaying && ServiceLocator.TryGet<RunManager>(out var rm))
                rm.DebugInfiniteGold = false;
        }

        // Keeps the infinite toggles enforced every editor tick during play mode.
        private void OnEditorUpdate()
        {
            if (!Application.isPlaying) return;
            if (!ServiceLocator.TryGet<RunManager>(out var rm) || rm.RunData == null) return;

            var run = rm.RunData;
            rm.DebugInfiniteGold = _infiniteGold;

            if (_infiniteGold && run.Gold != InfiniteGoldAmount)
            {
                run.Gold = InfiniteGoldAmount;
                EventBus.Publish(new GoldChangedEvent { Current = run.Gold, Delta = 0 });
            }

            if (_infiniteLives && run.Lives < run.MaxLives)
            {
                run.Lives = run.MaxLives;
                EventBus.Publish(new LivesChangedEvent { Current = run.Lives, Max = run.MaxLives });
            }
        }

        private void OnGUI()
        {
            _tab = GUILayout.Toolbar(_tab, Tabs);
            EditorGUILayout.Space(6);

            if (_tab == 0) DrawCheats();
            else DrawBalanceSim();
        }

        // =================================================================
        // CHEATS
        // =================================================================

        private void DrawCheats()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode (in the Game scene) to use cheats. " +
                    "Crystal grant works outside play mode too.", MessageType.Info);
            }

            // --- Wave jump ---
            EditorGUILayout.LabelField("Wave Jump", EditorStyles.boldLabel);
            _targetWave = EditorGUILayout.IntField("Target Wave", Mathf.Max(1, _targetWave));

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button($"Jump to Wave {_targetWave} (starts prep phase)"))
                    JumpToWave(_targetWave);
            }

            EditorGUILayout.Space(10);

            // --- Infinite toggles ---
            EditorGUILayout.LabelField("Infinite Resources", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                _infiniteGold = EditorGUILayout.ToggleLeft(
                    "Infinite Gold (upgrade spending bypassed)", _infiniteGold);
                _infiniteLives = EditorGUILayout.ToggleLeft(
                    "Infinite Lives (kept at MaxLives)", _infiniteLives);
            }

            EditorGUILayout.Space(10);

            // --- Crystals ---
            EditorGUILayout.LabelField("Crystals (meta currency, persists in save!)", EditorStyles.boldLabel);
            _crystalsToAdd = EditorGUILayout.IntField("Amount", _crystalsToAdd);
            if (GUILayout.Button($"Add {_crystalsToAdd:N0} Crystals"))
            {
                var save = SaveSystem.Load();
                save.MetaCurrency += _crystalsToAdd;
                SaveSystem.Save(save);
                EventBus.Publish(new MetaCurrencyChangedEvent
                {
                    Current = save.MetaCurrency,
                    Delta = _crystalsToAdd
                });
                Debug.Log($"[ETDDevTools] Crystals: {save.MetaCurrency:N0} (+{_crystalsToAdd:N0})");
            }

            EditorGUILayout.Space(10);

            // --- Achievement ID tester ---
            EditorGUILayout.LabelField("Achievement ID Tester (play mode + Steam running)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Fires the real unlock path for an API name. Watch the Console: " +
                "'GetAchievement failed' = that ID does NOT exist in your Steamworks panel. " +
                "'Unlocked: ...' = ID is correct. Reset unlocked test achievements in Steamworks before re-testing.",
                MessageType.Info);

            _achApiName = EditorGUILayout.TextField("API Name", _achApiName);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button($"Test Unlock '{_achApiName}'"))
                    TestUnlock(_achApiName);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("ACH_FIRST_TRAIT")) TestUnlock("ACH_FIRST_TRAIT");
                    if (GUILayout.Button("ACH_ALL_EFFECTS")) TestUnlock("ACH_ALL_EFFECTS");
                    if (GUILayout.Button("ACH_LIGHTNING_NETWORK")) TestUnlock("ACH_LIGHTNING_NETWORK");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("ACH_VOID_WALKER")) TestUnlock("ACH_VOID_WALKER");
                    if (GUILayout.Button("ACH_TOXIC_TOUCH")) TestUnlock("ACH_TOXIC_TOUCH");
                    if (GUILayout.Button("ACH_MARKED_FOR_DEATH")) TestUnlock("ACH_MARKED_FOR_DEATH");
                    if (GUILayout.Button("ACH_CUTTING_EDGE")) TestUnlock("ACH_CUTTING_EDGE");
                }
            }
        }

        private static void TestUnlock(string apiName)
        {
            var mgr = FindFirstObjectByType<SteamAchievementManager>();
            if (mgr == null)
            {
                Debug.LogWarning("[ETDDevTools] SteamAchievementManager not found in scene.");
                return;
            }
            mgr.DebugUnlock(apiName);
        }

        private void JumpToWave(int wave)
        {
            if (!ServiceLocator.TryGet<RunManager>(out var rm) || rm.RunData == null)
            {
                Debug.LogWarning("[ETDDevTools] No active run (RunManager not found).");
                return;
            }
            if (!ServiceLocator.TryGet<WaveManager>(out var wm))
            {
                Debug.LogWarning("[ETDDevTools] WaveManager not found.");
                return;
            }

            // Same sequence RunSnapshotManager uses on restore: seed the run wave for
            // trait wave-scaling, set the wave counter, enter prep, recalc turrets.
            rm.RunData.CurrentWave = wave;
            wm.SetWave(wave);
            wm.StartPrepPhase();

            if (ServiceLocator.TryGet<TurretManager>(out var tm))
                tm.RecalculateAllTurretsAndRefreshAuras();

            Debug.Log($"[ETDDevTools] Jumped to wave {wave}. Prep phase started.");
        }

        // =================================================================
        // BALANCE SIM
        // =================================================================

        private void DrawBalanceSim()
        {
            _simEnemy = (EnemyData)EditorGUILayout.ObjectField("Enemy Asset", _simEnemy, typeof(EnemyData), false);
            _referenceDps = EditorGUILayout.FloatField(
                new GUIContent("Reference DPS", "Your estimated total tower DPS. TTK columns = HP / this."),
                Mathf.Max(1f, _referenceDps));

            if (_simEnemy == null)
            {
                EditorGUILayout.HelpBox("Assign an EnemyData asset (e.g. Enemy_Basic or Enemy_Stealth).", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(
                $"{_simEnemy.DisplayName}: base {_simEnemy.MaxHealth} HP, growth {_simEnemy.HealthGrowthBase} " +
                $"(+{_simEnemy.HealthGrowthRampPerWave}/wave, cap +{_simEnemy.HealthGrowthRampCap})",
                EditorStyles.miniLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Header
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Wave", GUILayout.Width(44));
                GUILayout.Label("Growth", GUILayout.Width(52));
                GUILayout.Label("Normal HP", GUILayout.Width(80));
                GUILayout.Label("Elite HP", GUILayout.Width(80));
                GUILayout.Label("Boss HP", GUILayout.Width(80));
                GUILayout.Label("TTK-N", GUILayout.Width(60));
                GUILayout.Label("TTK-B", GUILayout.Width(60));
            }

            foreach (int w in SimWaves)
            {
                float normal = _simEnemy.GetScaledHealth(w, EnemyTier.Normal);
                float elite = _simEnemy.GetScaledHealth(w, EnemyTier.Elite);
                float boss = _simEnemy.GetScaledHealth(w, EnemyTier.Boss);
                double growth = _simEnemy.GetHealthGrowthForWave(w);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(w.ToString(), GUILayout.Width(44));
                    GUILayout.Label(growth.ToString("0.####"), GUILayout.Width(52));
                    GUILayout.Label(NumberFormat.Compact(normal), GUILayout.Width(80));
                    GUILayout.Label(NumberFormat.Compact(elite), GUILayout.Width(80));
                    GUILayout.Label(NumberFormat.Compact(boss), GUILayout.Width(80));
                    GUILayout.Label(FormatTtk(normal / _referenceDps), GUILayout.Width(60));
                    GUILayout.Label(FormatTtk(boss / _referenceDps), GUILayout.Width(60));
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(
                "Uses the exact runtime scaling code (GetScaledHealth / GetHealthGrowthForWave). " +
                "TTK = time-to-kill at your Reference DPS. A boss TTK over ~120s means an HP wall.",
                MessageType.None);
        }

        private static string FormatTtk(float seconds)
        {
            if (seconds < 60f) return seconds.ToString("0.#") + "s";
            if (seconds < 3600f) return (seconds / 60f).ToString("0.#") + "m";
            return (seconds / 3600f).ToString("0.#") + "h";
        }
    }
}
