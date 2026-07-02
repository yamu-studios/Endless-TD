// ============================================================================
// ETD.EditorTools - ChallengeDebugWindow.cs
// Dev-only challenge testing window.
// Put in: Assets/Scripts/Editor/ChallengeDebugWindow.cs
// Requires the ChallengeTracker debug patch from ChallengeTracker_DebugPatch.txt.
// ============================================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ETD.Core;
using ETD.Data;
using ETD.Meta;

namespace ETD.EditorTools
{
    public class ChallengeDebugWindow : EditorWindow
    {
        private Vector2 _scroll;
        private ChallengeData[] _challenges = Array.Empty<ChallengeData>();

        private int _waveNumber = 20;
        private int _buildCount = 25;
        private int _upgradeCount = 50;
        private int _goldAmount = 30000;
        private int _chainHits = 1500;
        private int _criticalHits = 100;
        private float _burnDamage = 75000f;
        private float _slowSeconds = 600f;
        private float _laserSeconds = 15f;
        private float _sniperDamage = 50000f;
        private float _percentHpDamage = 50000f;

        private bool _autoRefreshChallenges = true;

        [MenuItem("Tools/ETD/Debug/Challenge Debug Window")]
        public static void Open()
        {
            GetWindow<ChallengeDebugWindow>("Challenge Debug");
        }

        private void OnEnable()
        {
            RefreshChallenges();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Challenge Debug Tools", EditorStyles.boldLabel);

                if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                    RefreshChallenges();
            }

            EditorGUILayout.HelpBox(
                "Use this in Play Mode when a ChallengeTracker exists in the scene. " +
                "The top buttons publish real gameplay events. The lower section directly sets challenge progress through a dev-only ChallengeTracker API.",
                MessageType.Info);

            bool isPlaying = EditorApplication.isPlaying;
            if (!isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to publish runtime events or edit live challenge progress.", MessageType.Warning);
            }

            ChallengeTracker tracker = FindFirstObjectByType<ChallengeTracker>();
            if (isPlaying && tracker == null)
            {
                EditorGUILayout.HelpBox("No ChallengeTracker found in the active scene. Start a run or add one to the scene.", MessageType.Error);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawEventSimulationSection(isPlaying);
            DrawDirectProgressSection(isPlaying, tracker);
            DrawSaveToolsSection(isPlaying, tracker);

            EditorGUILayout.EndScrollView();
        }

        // =====================================================================
        // EVENT SIMULATION
        // =====================================================================

        private void DrawEventSimulationSection(bool isPlaying)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("1) Real Event Simulation", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!isPlaying))
            {
                _waveNumber = EditorGUILayout.IntField("Wave Number", _waveNumber);
                if (GUILayout.Button($"Publish Wave Completed: {_waveNumber}"))
                    EventBus.Publish(new WaveCompletedEvent { WaveNumber = _waveNumber });

                EditorGUILayout.Space(4);

                _buildCount = EditorGUILayout.IntField("Build Count", _buildCount);
                if (GUILayout.Button($"Publish Turret Placed x{_buildCount}"))
                {
                    for (int i = 0; i < _buildCount; i++)
                    {
                        EventBus.Publish(new TurretPlacedEvent
                        {
                            TurretId = i,
                            GridPos = new Vector2Int(i % 10, i / 10)
                        });
                    }
                }

                _upgradeCount = EditorGUILayout.IntField("Upgrade Count", _upgradeCount);
                if (GUILayout.Button($"Publish Turret Upgraded x{_upgradeCount}"))
                {
                    for (int i = 0; i < _upgradeCount; i++)
                    {
                        EventBus.Publish(new TurretUpgradedEvent
                        {
                            TurretId = i,
                            NewLevel = 2
                        });
                    }
                }

                EditorGUILayout.Space(4);

                _goldAmount = EditorGUILayout.IntField("Gold Amount", _goldAmount);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button($"Earn Gold +{_goldAmount}"))
                        EventBus.Publish(new GoldChangedEvent { Current = _goldAmount, Delta = _goldAmount });

                    if (GUILayout.Button($"Spend Gold -{_goldAmount}"))
                        EventBus.Publish(new GoldChangedEvent { Current = 0, Delta = -_goldAmount });
                }

                EditorGUILayout.Space(4);

                _burnDamage = EditorGUILayout.FloatField("Burn Damage", _burnDamage);
                if (GUILayout.Button($"Simulate Burn Applications ≈ {_burnDamage:0} Damage"))
                {
                    // Current ChallengeTracker adds 100 burn damage per Burn status application.
                    int count = Mathf.CeilToInt(_burnDamage / 100f);
                    for (int i = 0; i < count; i++)
                    {
                        EventBus.Publish(new EnemyStatusAppliedEvent
                        {
                            EnemyId = i,
                            StatusType = (int)StatusEffectType.Burn,
                            Duration = 3f
                        });
                    }
                }

                _slowSeconds = EditorGUILayout.FloatField("Slow Seconds", _slowSeconds);
                if (GUILayout.Button($"Simulate Slow Total Seconds +{_slowSeconds:0}"))
                {
                    EventBus.Publish(new EnemyStatusAppliedEvent
                    {
                        EnemyId = 1,
                        StatusType = (int)StatusEffectType.Slow,
                        Duration = _slowSeconds
                    });
                }

                //if (GUILayout.Button("Simulate Use All Effects: Burn + Slow/Freeze"))
                //{
                //    EventBus.Publish(new EnemyStatusAppliedEvent
                //    {
                //        EnemyId = 1,
                //        StatusType = (int)StatusEffectType.Burn,
                //        Duration = 3f
                //    });

                //    EventBus.Publish(new EnemyStatusAppliedEvent
                //    {
                //        EnemyId = 2,
                //        StatusType = (int)StatusEffectType.Freeze,
                //        Duration = 2f
                //    });
                //}
                if (GUILayout.Button("Simulate Elementalist: Inferno + Frost + Lightning"))
                {
                    EventBus.Publish(new EnemyStatusAppliedEvent
                    {
                        EnemyId = 1,
                        StatusType = (int)StatusEffectType.Burn,
                        Duration = 3f
                    });

                    EventBus.Publish(new EnemyStatusAppliedEvent
                    {
                        EnemyId = 2,
                        StatusType = (int)StatusEffectType.Slow,
                        Duration = 3f
                    });

                    EventBus.Publish(new ChainLightningHitEvent
                    {
                        Count = 1

                        // Add this only if you update the event:
                        // EnemyId = 3
                    });
                }


                // Replace/add Combo Master debug button with this:
                if (GUILayout.Button("Simulate Combo Master: Inferno + Frost + Lightning On Same Enemy"))
                {
                    const int enemyId = 999;

                    EventBus.Publish(new EnemyStatusAppliedEvent
                    {
                        EnemyId = enemyId,
                        StatusType = (int)StatusEffectType.Burn,
                        Duration = 3f
                    });

                    EventBus.Publish(new EnemyStatusAppliedEvent
                    {
                        EnemyId = enemyId,
                        StatusType = (int)StatusEffectType.Slow,
                        Duration = 3f
                    });

                    EventBus.Publish(new ChainLightningHitEvent
                    {
                        Count = 1,

                        // Required for Combo Master:
                         EnemyId = enemyId
                    });
                }


                EditorGUILayout.Space(4);

                _chainHits = EditorGUILayout.IntField("Chain Hits", _chainHits);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button($"Chain Damage +{_chainHits}"))
                        EventBus.Publish(new ChainLightningHitEvent { Count = _chainHits });

                    if (GUILayout.Button("Chain Fired 10 Hits"))
                    {
                        EventBus.Publish(new ChainLightningFiredEvent
                        {
                            HitCount = 10,
                            Origin = Vector3.zero
                        });
                    }
                }

                _laserSeconds = EditorGUILayout.FloatField("Laser Seconds", _laserSeconds);
                if (GUILayout.Button($"Laser Hit Time +{_laserSeconds:0.00}s"))
                {
                    EventBus.Publish(new LaserHitEvent
                    {
                        TargetEnemyId = 1,
                        DeltaTime = _laserSeconds
                    });

                    // Your current tracker does not call CheckAllChallenges() inside OnLaserHit every frame.
                    // Use direct progress section for exact completion verification.
                }

                EditorGUILayout.Space(4);

                _criticalHits = EditorGUILayout.IntField("Critical Hits", _criticalHits);
                if (GUILayout.Button($"Critical Hit x{_criticalHits}"))
                {
                    for (int i = 0; i < _criticalHits; i++)
                        EventBus.Publish(new CriticalHitEvent());
                }

                _sniperDamage = EditorGUILayout.FloatField("Sniper Damage", _sniperDamage);
                if (GUILayout.Button($"Sniper Damage +{_sniperDamage:0}"))
                    EventBus.Publish(new SniperDamageEvent { Damage = _sniperDamage });

                _percentHpDamage = EditorGUILayout.FloatField("Percent HP Damage", _percentHpDamage);
                if (GUILayout.Button($"Percent HP Damage +{_percentHpDamage:0}"))
                    EventBus.Publish(new PercentHPDamageEvent { DamageAmount = _percentHpDamage });

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Burning Enemy Kill"))
                        EventBus.Publish(new EnemyBurnedKilledEvent { EnemyId = 1 });

                    if (GUILayout.Button("Elite Enemy Kill"))
                        EventBus.Publish(new EnemyKilledEvent
                        {
                            EnemyId = 1,
                            EnemyTier = (int)EnemyTier.Elite,
                            GoldReward = 100,
                            XPReward = 10,
                            Position = Vector3.zero
                        });

                    if (GUILayout.Button("Leak Enemy"))
                        EventBus.Publish(new EnemyReachedEndEvent { EnemyId = 1, Damage = 1 });
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Projectile Fired"))
                        EventBus.Publish(new ProjectileSpawnedEvent());

                    if (GUILayout.Button("Buff 12 Turrets"))
                        EventBus.Publish(new BuffTurretsEvent { Count = 12 });
                }
            }
        }

        // =====================================================================
        // DIRECT PROGRESS
        // =====================================================================

        private void DrawDirectProgressSection(bool isPlaying, ChallengeTracker tracker)
        {
            EditorGUILayout.Space(14);
            EditorGUILayout.LabelField("2) Direct Challenge Progress", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _autoRefreshChallenges = EditorGUILayout.ToggleLeft("Auto refresh challenge asset list", _autoRefreshChallenges);
                if (GUILayout.Button("Refresh Challenge Assets", GUILayout.Width(180)))
                    RefreshChallenges();
            }

            if (_autoRefreshChallenges && Event.current.type == EventType.Layout && _challenges.Length == 0)
                RefreshChallenges();

            using (new EditorGUI.DisabledScope(!isPlaying || tracker == null))
            {
                if (_challenges == null || _challenges.Length == 0)
                {
                    EditorGUILayout.HelpBox("No ChallengeData assets found.", MessageType.Warning);
                    return;
                }

                EditorGUILayout.Space(4);

                foreach (ChallengeData ch in _challenges)
                {
                    if (ch == null) continue;

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField($"{ch.DisplayName}  [{ch.ConditionType}]", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"ID: {ch.Id}");
                        EditorGUILayout.LabelField($"Target: {ch.TargetValue:0.##}");

                        if (tracker != null)
                        {
                            float progress = tracker.GetProgress(ch);
                            EditorGUILayout.LabelField($"Current: {progress:0.##} / {ch.TargetValue:0.##}");
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Set 50%"))
                                tracker.DebugSetProgress(ch.ConditionType, ch.TargetValue * 0.5f);

                            if (GUILayout.Button("Complete"))
                                tracker.DebugSetProgress(ch.ConditionType, ch.TargetValue);

                            if (GUILayout.Button("+10%"))
                                tracker.DebugAddProgress(ch.ConditionType, ch.TargetValue * 0.1f);
                        }
                    }
                }
            }
        }

        // =====================================================================
        // SAVE TOOLS
        // =====================================================================

        private void DrawSaveToolsSection(bool isPlaying, ChallengeTracker tracker)
        {
            EditorGUILayout.Space(14);
            EditorGUILayout.LabelField("3) Save / Reset Tools", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!isPlaying || tracker == null))
            {
                if (GUILayout.Button("Debug Save Current Challenge Progress"))
                    tracker.DebugSaveProgress();

                if (GUILayout.Button("Clear Challenge Progress + Completed Challenge IDs"))
                {
                    bool ok = EditorUtility.DisplayDialog(
                        "Clear Challenge Progress?",
                        "This clears saved challenge progress and completed challenge IDs. Use only in development.",
                        "Clear",
                        "Cancel");

                    if (ok)
                        tracker.DebugClearChallengeProgressAndCompletions();
                }
            }
        }

        private void RefreshChallenges()
        {
            List<ChallengeData> result = new();

            string[] guids = AssetDatabase.FindAssets("t:ChallengeData", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ChallengeData ch = AssetDatabase.LoadAssetAtPath<ChallengeData>(path);
                if (ch != null)
                    result.Add(ch);
            }

            result.Sort((a, b) =>
            {
                int cat = a.Category.CompareTo(b.Category);
                if (cat != 0) return cat;
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            _challenges = result.ToArray();
            Repaint();
        }
    }
}
#endif
