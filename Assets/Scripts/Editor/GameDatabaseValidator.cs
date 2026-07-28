#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ETD.Data;
using ETD.Turrets;
using UnityEditor;
using UnityEngine;

namespace ETD.EditorTools
{
    /// <summary>
    /// Editor-only validator for ETD ScriptableObject data.
    /// Put this file anywhere inside an Editor folder, for example:
    /// Assets/Scripts/Editor/GameDatabaseValidator.cs
    ///
    /// Usage:
    /// Tools/ETD/Validate Game Database
    /// </summary>
    public static class GameDatabaseValidator
    {
        private enum Severity
        {
            Error,
            Warning
        }

        private readonly struct Issue
        {
            public readonly Severity Severity;
            public readonly UnityEngine.Object Context;
            public readonly string Message;

            public Issue(Severity severity, UnityEngine.Object context, string message)
            {
                Severity = severity;
                Context = context;
                Message = message;
            }
        }

        [MenuItem("Tools/ETD/Validate Game Database")]
        public static bool ValidateFromMenu()
        {
            bool valid = ValidateAllDatabases(showDialog: true, logSuccess: true);
            return valid;
        }

        /// <summary>
        /// Returns true when there are no validation errors.
        /// Warnings do not fail validation.
        /// </summary>
        public static bool ValidateAllDatabases(bool showDialog = false, bool logSuccess = false)
        {
            var issues = new List<Issue>();
            var databases = FindAssetsByType<GameDatabase>();

            if (databases.Count == 0)
            {
                issues.Add(new Issue(Severity.Error, null,
                    "No GameDatabase asset found in the project."));
            }

            foreach (var db in databases)
            {
                ValidateDatabase(db, issues);
            }

            PrintIssues(issues, databases.Count, logSuccess);

            int errorCount = issues.Count(i => i.Severity == Severity.Error);
            int warningCount = issues.Count(i => i.Severity == Severity.Warning);

            if (showDialog)
            {
                if (errorCount > 0)
                {
                    EditorUtility.DisplayDialog(
                        "ETD Database Validation Failed",
                        $"Found {errorCount} error(s) and {warningCount} warning(s).\n\nCheck the Console for details.",
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "ETD Database Validation Passed",
                        warningCount > 0
                            ? $"No errors found. Found {warningCount} warning(s). Check Console for details."
                            : "No errors or warnings found.",
                        "OK");
                }
            }

            return errorCount == 0;
        }

        private static void ValidateDatabase(GameDatabase db, List<Issue> issues)
        {
            if (db == null)
            {
                issues.Add(new Issue(Severity.Error, null, "GameDatabase reference is null."));
                return;
            }

            ValidateIdArray(db.Enemies, "Enemies", e => e.Id, db, issues);
            ValidateIdArray(db.Turrets, "Turrets", t => t.Id, db, issues);
            ValidateIdArray(db.Traits, "Traits", t => t.Id, db, issues);
            ValidateIdArray(db.SpecCards, "SpecCards", c => c.Id, db, issues);
            ValidateIdArray(db.Challenges, "Challenges", c => c.Id, db, issues);
            ValidateIdArray(db.DynamicTiles, "DynamicTiles", t => t.Id, db, issues);

            ValidateUniqueDynamicTileTypes(db.DynamicTiles, db, issues);
            ValidateGradeColors(db, issues);

            ValidateEnemies(db.Enemies, issues);
            ValidateTurrets(db.Turrets, issues);
            ValidateTurretPrefabBalanceFields(db.Turrets, issues);
            ValidateTraits(db.Traits, issues);
            ValidateSpecCards(db.SpecCards, issues);
            ValidateChallenges(db.Challenges, issues);
            ValidateDynamicTiles(db.DynamicTiles, issues);
        }

        private static void ValidateIdArray<T>(
            IReadOnlyList<T> items,
            string groupName,
            Func<T, string> getId,
            UnityEngine.Object databaseContext,
            List<Issue> issues)
            where T : UnityEngine.Object
        {
            if (items == null)
            {
                issues.Add(new Issue(Severity.Error, databaseContext,
                    $"{groupName} array is null."));
                return;
            }

            var seen = new Dictionary<string, T>();

            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                if (item == null)
                {
                    issues.Add(new Issue(Severity.Error, databaseContext,
                        $"{groupName}[{i}] is null."));
                    continue;
                }

                string id = getId(item);
                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Add(new Issue(Severity.Error, item,
                        $"{groupName}: '{item.name}' has empty Id."));
                    continue;
                }

                if (seen.TryGetValue(id, out T duplicate))
                {
                    issues.Add(new Issue(Severity.Error, item,
                        $"{groupName}: duplicate Id '{id}' used by '{duplicate.name}' and '{item.name}'."));
                    continue;
                }

                seen.Add(id, item);
            }
        }

        private static void ValidateEnemies(IReadOnlyList<EnemyData> enemies, List<Issue> issues)
        {
            if (enemies == null) return;

            foreach (var enemy in enemies.Where(e => e != null))
            {
                RequireText(enemy.DisplayName, enemy, "Enemy DisplayName", issues);
                WarnText(enemy.LocalizationKey, enemy, "Enemy LocalizationKey", issues);

                if (enemy.Prefab == null)
                    issues.Add(new Issue(Severity.Error, enemy, $"Enemy '{enemy.name}' has no Prefab."));

                if (enemy.Icon == null)
                    issues.Add(new Issue(Severity.Warning, enemy, $"Enemy '{enemy.name}' has no Icon."));

                if (enemy.MaxHealth <= 0f)
                    issues.Add(new Issue(Severity.Error, enemy, $"Enemy '{enemy.name}' MaxHealth must be > 0."));

                if (enemy.MoveSpeed <= 0f)
                    issues.Add(new Issue(Severity.Error, enemy, $"Enemy '{enemy.name}' MoveSpeed must be > 0."));

                if (enemy.SpawnWeight <= 0)
                    issues.Add(new Issue(Severity.Error, enemy, $"Enemy '{enemy.name}' SpawnWeight must be > 0."));

                if (enemy.UnlockWave < 1)
                    issues.Add(new Issue(Severity.Error, enemy, $"Enemy '{enemy.name}' UnlockWave must be >= 1."));

                if (enemy.MaxWave > 0 && enemy.MaxWave < enemy.UnlockWave)
                    issues.Add(new Issue(Severity.Error, enemy,
                        $"Enemy '{enemy.name}' MaxWave cannot be lower than UnlockWave."));
            }
        }

        private static void ValidateTurrets(IReadOnlyList<TurretData> turrets, List<Issue> issues)
        {
            if (turrets == null) return;

            foreach (var turret in turrets.Where(t => t != null))
            {
                RequireText(turret.DisplayName, turret, "Turret DisplayName", issues);
                WarnText(turret.LocalizationKey, turret, "Turret LocalizationKey", issues);

                if (turret.Prefab == null)
                    issues.Add(new Issue(Severity.Error, turret, $"Turret '{turret.name}' has no Prefab."));

                if (turret.Icon == null)
                    issues.Add(new Issue(Severity.Warning, turret, $"Turret '{turret.name}' has no Icon."));

                if (turret.Damage < 0f)
                    issues.Add(new Issue(Severity.Error, turret, $"Turret '{turret.name}' Damage cannot be negative."));

                if (!turret.IsContinuousBeam && turret.AttackInterval <= 0f)
                    issues.Add(new Issue(Severity.Error, turret, $"Turret '{turret.name}' AttackInterval must be > 0."));

                if (turret.Range <= 0f)
                    issues.Add(new Issue(Severity.Error, turret, $"Turret '{turret.name}' Range must be > 0."));

                if (turret.Cost < 0)
                    issues.Add(new Issue(Severity.Error, turret, $"Turret '{turret.name}' Cost cannot be negative."));

                if (turret.BaseUpgradeCost < 0)
                    issues.Add(new Issue(Severity.Error, turret, $"Turret '{turret.name}' BaseUpgradeCost cannot be negative."));

                if (turret.UpgradeCostMultiplier < 1f)
                    issues.Add(new Issue(Severity.Warning, turret,
                        $"Turret '{turret.name}' UpgradeCostMultiplier is below 1. This may make upgrades cheaper forever."));

                ValidateEvolutionData(turret, turret.PathA, "PathA", issues);
                ValidateEvolutionData(turret, turret.PathB, "PathB", issues);
            }
        }

        private static void ValidateEvolutionData(
            TurretData turret,
            TurretEvolutionData evolution,
            string pathName,
            List<Issue> issues)
        {
            if (evolution == null)
            {
                issues.Add(new Issue(Severity.Warning, turret,
                    $"Turret '{turret.name}' has no {pathName} evolution data."));
                return;
            }

            if (string.IsNullOrWhiteSpace(evolution.Id))
                issues.Add(new Issue(Severity.Warning, turret,
                    $"Turret '{turret.name}' {pathName} evolution has empty Id."));

            if (string.IsNullOrWhiteSpace(evolution.Name))
                issues.Add(new Issue(Severity.Warning, turret,
                    $"Turret '{turret.name}' {pathName} evolution has empty Name."));

            if (evolution.EvolvedPrefab == null)
                issues.Add(new Issue(Severity.Warning, turret,
                    $"Turret '{turret.name}' {pathName} has no EvolvedPrefab. This is okay only if you intentionally keep the same prefab."));
        }

        // =================================================================
        // WIKI BALANCE-CONSTANT DRIFT
        // =================================================================

        /// <summary>
        /// The in-game wiki computes its numbers from ETD.Data.BalanceConstants, but
        /// gameplay reads the serialized field on each turret prefab. Those agree today
        /// only because no prefab overrides them. If a constant is changed while a
        /// prefab holds a stale serialized copy, the wiki would confidently print a
        /// number the game does not use — so flag any divergence.
        /// </summary>
        private static readonly (string Field, float Expected, string ConstantName)[] WikiBalanceFields =
        {
            ("_minimumProjectileAttackInterval",     BalanceConstants.MinProjectileAttackInterval,     nameof(BalanceConstants.MinProjectileAttackInterval)),
            ("_maxProjectileAttackSpeed",            BalanceConstants.MaxProjectileAttackSpeed,        nameof(BalanceConstants.MaxProjectileAttackSpeed)),
            ("_maxMultiplicativeDamageGrowthPerLevel", BalanceConstants.MaxDamageGrowthPerLevel,       nameof(BalanceConstants.MaxDamageGrowthPerLevel)),
            ("_baseCritDamageMultiplier",            BalanceConstants.BaseCritDamageMultiplier,        nameof(BalanceConstants.BaseCritDamageMultiplier)),
            ("_identityScalingPerLevel",             BalanceConstants.IdentityScalingPerLevel,         nameof(BalanceConstants.IdentityScalingPerLevel)),
            ("_identityScalingCap",                  BalanceConstants.IdentityScalingCap,              nameof(BalanceConstants.IdentityScalingCap)),
            ("_burnHitPercent",                      BalanceConstants.BurnHitPercent,                  nameof(BalanceConstants.BurnHitPercent)),
            ("_blastfireAreaDamagePercent",          BalanceConstants.BlastfireAreaDamagePercent,      nameof(BalanceConstants.BlastfireAreaDamagePercent)),
        };

        private const float BalanceFieldEpsilon = 0.0001f;

        private static void ValidateTurretPrefabBalanceFields(
            IReadOnlyList<TurretData> turrets,
            List<Issue> issues)
        {
            if (turrets == null) return;

            // Base and evolved prefabs both spawn live turrets, so both must agree.
            var checkedPrefabs = new HashSet<GameObject>();

            foreach (var turret in turrets.Where(t => t != null))
            {
                CheckPrefabBalanceFields(turret.Prefab, turret, checkedPrefabs, issues);
                if (turret.PathA != null)
                    CheckPrefabBalanceFields(turret.PathA.EvolvedPrefab, turret, checkedPrefabs, issues);
                if (turret.PathB != null)
                    CheckPrefabBalanceFields(turret.PathB.EvolvedPrefab, turret, checkedPrefabs, issues);
            }
        }

        private static void CheckPrefabBalanceFields(
            GameObject prefab,
            TurretData owner,
            HashSet<GameObject> alreadyChecked,
            List<Issue> issues)
        {
            if (prefab == null || !alreadyChecked.Add(prefab))
                return;

            var controller = prefab.GetComponentInChildren<TurretController>(true);
            if (controller == null)
                return;

            // SerializedObject reads the in-memory value, which is the C# field
            // initializer when the prefab YAML predates the field. That is exactly the
            // value gameplay will use, so absent-from-YAML correctly reads as matching.
            var so = new SerializedObject(controller);

            foreach (var (field, expected, constantName) in WikiBalanceFields)
            {
                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    issues.Add(new Issue(Severity.Warning, prefab,
                        $"Turret prefab '{prefab.name}' has no field '{field}'. " +
                        $"BalanceConstants.{constantName} may be documenting a field that no longer exists."));
                    continue;
                }

                if (Mathf.Abs(prop.floatValue - expected) > BalanceFieldEpsilon)
                {
                    issues.Add(new Issue(Severity.Warning, prefab,
                        $"Turret prefab '{prefab.name}' (used by '{owner.name}') has {field} = {prop.floatValue}, " +
                        $"but BalanceConstants.{constantName} = {expected}. The in-game wiki quotes the constant, " +
                        $"so it would show a number this turret does not use. Re-sync the prefab or the constant."));
                }
            }

            so.Dispose();
        }

        private static void ValidateTraits(IReadOnlyList<TraitData> traits, List<Issue> issues)
        {
            if (traits == null) return;

            foreach (var trait in traits.Where(t => t != null))
            {
                RequireText(trait.DisplayName, trait, "Trait DisplayName", issues);
                WarnText(trait.LocalizationKey, trait, "Trait LocalizationKey", issues);

                if (trait.Icon == null)
                    issues.Add(new Issue(Severity.Warning, trait, $"Trait '{trait.name}' has no Icon."));

                if (!trait.IsUnlockedByDefault && trait.UnlockConditionTarget <= 0f)
                    issues.Add(new Issue(Severity.Warning, trait,
                        $"Trait '{trait.name}' is locked but UnlockConditionTarget is <= 0."));
            }
        }

        private static void ValidateSpecCards(IReadOnlyList<SpecCardData> cards, List<Issue> issues)
        {
            if (cards == null) return;

            foreach (var card in cards.Where(c => c != null))
            {
                RequireText(card.DisplayName, card, "SpecCard DisplayName", issues);
                WarnText(card.LocalizationKey, card, "SpecCard LocalizationKey", issues);

                if (card.Icon == null)
                    issues.Add(new Issue(Severity.Warning, card, $"SpecCard '{card.name}' has no Icon."));

                if (card.CanStack && card.MaxStacks <= 0)
                    issues.Add(new Issue(Severity.Error, card,
                        $"SpecCard '{card.name}' CanStack is true but MaxStacks is <= 0."));

                if (!card.CanStack && card.MaxStacks > 1)
                    issues.Add(new Issue(Severity.Warning, card,
                        $"SpecCard '{card.name}' CanStack is false but MaxStacks is > 1."));

                if (string.IsNullOrWhiteSpace(card.Theme))
                    issues.Add(new Issue(Severity.Warning, card, $"SpecCard '{card.name}' has empty Theme."));
            }
        }

        private static void ValidateChallenges(IReadOnlyList<ChallengeData> challenges, List<Issue> issues)
        {
            if (challenges == null) return;

            foreach (var challenge in challenges.Where(c => c != null))
            {
                RequireText(challenge.DisplayName, challenge, "Challenge DisplayName", issues);
                RequireText(challenge.Description, challenge, "Challenge Description", issues);
                WarnText(challenge.LocalizationKey, challenge, "Challenge LocalizationKey", issues);

                if (challenge.TargetValue <= 0f)
                    issues.Add(new Issue(Severity.Error, challenge,
                        $"Challenge '{challenge.name}' TargetValue must be > 0."));

                if (challenge.IncompleteIcon == null)
                    issues.Add(new Issue(Severity.Warning, challenge,
                        $"Challenge '{challenge.name}' has no IncompleteIcon."));

                if (challenge.CompleteIcon == null)
                    issues.Add(new Issue(Severity.Warning, challenge,
                        $"Challenge '{challenge.name}' has no CompleteIcon."));

                if (challenge.RewardIcon == null)
                    issues.Add(new Issue(Severity.Warning, challenge,
                        $"Challenge '{challenge.name}' has no RewardIcon."));
            }
        }

        private static void ValidateDynamicTiles(IReadOnlyList<DynamicTileData> tiles, List<Issue> issues)
        {
            if (tiles == null) return;

            foreach (var tile in tiles.Where(t => t != null))
            {
                RequireText(tile.DisplayName, tile, "DynamicTile DisplayName", issues);
                WarnText(tile.LocalizationKey, tile, "DynamicTile LocalizationKey", issues);

                if (tile.PrimaryEffect.Stat == TurretStatModifier.StatType.None)
                    issues.Add(new Issue(Severity.Warning, tile,
                        $"DynamicTile '{tile.name}' PrimaryEffect.Stat is None."));

                if (tile.TileMaterial == null)
                    issues.Add(new Issue(Severity.Warning, tile,
                        $"DynamicTile '{tile.name}' has no TileMaterial."));
            }
        }

        private static void ValidateUniqueDynamicTileTypes(
            IReadOnlyList<DynamicTileData> tiles,
            UnityEngine.Object databaseContext,
            List<Issue> issues)
        {
            if (tiles == null) return;

            var seen = new Dictionary<DynamicTileType, DynamicTileData>();
            foreach (var tile in tiles.Where(t => t != null))
            {
                if (seen.TryGetValue(tile.TileType, out var duplicate))
                {
                    issues.Add(new Issue(Severity.Error, tile,
                        $"DynamicTiles: duplicate TileType '{tile.TileType}' used by '{duplicate.name}' and '{tile.name}'."));
                }
                else
                {
                    seen.Add(tile.TileType, tile);
                }
            }

            foreach (DynamicTileType type in Enum.GetValues(typeof(DynamicTileType)))
            {
                if (!seen.ContainsKey(type))
                {
                    issues.Add(new Issue(Severity.Warning, databaseContext,
                        $"DynamicTiles: no asset assigned for TileType '{type}'."));
                }
            }
        }

        private static void ValidateGradeColors(GameDatabase db, List<Issue> issues)
        {
            int rarityCount = Enum.GetValues(typeof(SpecCardRarity)).Length;
            if (db.GradeColors == null)
            {
                issues.Add(new Issue(Severity.Warning, db, "GradeColors array is null."));
                return;
            }

            if (db.GradeColors.Length < rarityCount)
            {
                issues.Add(new Issue(Severity.Warning, db,
                    $"GradeColors has {db.GradeColors.Length} colors, but SpecCardRarity has {rarityCount} values."));
            }
        }

        private static void RequireText(
            string value,
            UnityEngine.Object context,
            string fieldName,
            List<Issue> issues)
        {
            if (string.IsNullOrWhiteSpace(value))
                issues.Add(new Issue(Severity.Error, context, $"{fieldName} is empty on '{context.name}'."));
        }

        private static void WarnText(
            string value,
            UnityEngine.Object context,
            string fieldName,
            List<Issue> issues)
        {
            if (string.IsNullOrWhiteSpace(value))
                issues.Add(new Issue(Severity.Warning, context, $"{fieldName} is empty on '{context.name}'."));
        }

        private static List<T> FindAssetsByType<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            var assets = new List<T>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                    assets.Add(asset);
            }

            return assets;
        }

        private static void PrintIssues(IReadOnlyList<Issue> issues, int databaseCount, bool logSuccess)
        {
            if (issues.Count == 0)
            {
                if (logSuccess)
                    Debug.Log($"ETD database validation passed. Checked {databaseCount} GameDatabase asset(s). No issues found.");
                return;
            }

            var builder = new StringBuilder();
            int errorCount = issues.Count(i => i.Severity == Severity.Error);
            int warningCount = issues.Count(i => i.Severity == Severity.Warning);

            builder.AppendLine($"ETD database validation found {errorCount} error(s), {warningCount} warning(s).");
            builder.AppendLine("Fix errors before building/releasing. Warnings are cleanup/balance/content reminders.");

            Debug.LogWarning(builder.ToString());

            foreach (Issue issue in issues)
            {
                if (issue.Severity == Severity.Error)
                    Debug.LogError(issue.Message, issue.Context);
                else
                    Debug.LogWarning(issue.Message, issue.Context);
            }
        }
    }
}
#endif
