// ============================================================================
// ETD.UI.Wiki - WikiTurretPage.cs  [NEW]
// Per-turret page with a computed level table. Every value runs through
// ETD.Data.TurretStatMath, the same math a placed turret uses, so the table is
// correct without a turret existing in the scene.
// ============================================================================
using System.Collections.Generic;
using ETD.Core;
using ETD.Data;

namespace ETD.UI.Wiki
{
    public sealed class TurretWikiPage : IWikiPage
    {
        /// <summary>Levels sampled in the scaling table. Chosen to show the curve's shape.</summary>
        private static readonly int[] SampleLevels = { 1, 10, 25, 50 };

        private readonly TurretData _data;

        public TurretWikiPage(TurretData data)
        {
            _data = data;
        }

        public string Id => "turret_" + (_data != null ? _data.Id : "null");

        public string Title => _data == null
            ? "?"
            : SOLocalization.GetName("turret_" + _data.LocalizationKey, _data.DisplayName);

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();
            if (_data == null) return sections;

            string description = SOLocalization.GetDesc("turret_" + _data.LocalizationKey, _data.Description);
            if (!string.IsNullOrWhiteSpace(description))
                sections.Add(new WikiSection(LocalizationManager.Get("wiki_overview", "Overview"), description));

            sections.Add(BuildScalingSection(_data.Damage, _data.AttackInterval, _data.Range,
                LocalizationManager.Get("wiki_turret_scaling", "Scaling by level")));

            AddEvolutionSection(sections, _data.PathA, LocalizationManager.Get("wiki_path_a", "Evolution A"));
            AddEvolutionSection(sections, _data.PathB, LocalizationManager.Get("wiki_path_b", "Evolution B"));

            AddSuppressionSection(sections);

            return sections;
        }

        private WikiSection BuildScalingSection(float baseDamage, float baseInterval, float baseRange, string heading)
        {
            var section = new WikiSection(heading,
                LocalizationManager.Get("wiki_turret_scaling_body",
                    "Damage compounds per level. Range grows linearly. These are base values " +
                    "before run bonuses."));

            foreach (int level in SampleLevels)
            {
                int levelIndex = level - 1;

                float damage = TurretStatMath.LeveledDamage(
                    baseDamage, _data.DamagePerLevel, levelIndex, BalanceConstants.MaxDamageGrowthPerLevel);
                float range = TurretStatMath.LeveledRange(baseRange, _data.RangePerLevel, levelIndex);

                string value = LocalizationManager.GetFormat("wiki_turret_row",
                    "{0} dmg  •  {1} rng", WikiFormat.Number(damage), WikiFormat.Number(range));

                // Continuous beams express damage as DPS and have no meaningful interval.
                if (!_data.IsContinuousBeam)
                {
                    float interval = TurretStatMath.LeveledAttackInterval(
                        baseInterval, _data.AttackSpeedPerLevel, levelIndex,
                        BalanceConstants.MinProjectileAttackInterval);
                    float perSecond = interval > 0f ? 1f / interval : 0f;
                    value += LocalizationManager.GetFormat("wiki_turret_row_speed",
                        "  •  {0}/s", WikiFormat.Number(perSecond, 2));
                }

                section.Row(LocalizationManager.GetFormat("wiki_level", "Level {0}", level), value);
            }

            return section;
        }

        private void AddEvolutionSection(List<WikiSection> sections, TurretEvolutionData evo, string heading)
        {
            if (evo == null) return;

            var section = new WikiSection(heading + (string.IsNullOrWhiteSpace(evo.Name) ? "" : ": " + evo.Name));

            bool anyOverride = false;
            if (evo.DamageOverride >= 0f)
            {
                section.Row(LocalizationManager.Get("damage", "Damage"), WikiFormat.Number(evo.DamageOverride));
                anyOverride = true;
            }
            if (evo.AttackIntervalOverride >= 0f)
            {
                float perSecond = evo.AttackIntervalOverride > 0f ? 1f / evo.AttackIntervalOverride : 0f;
                section.Row(LocalizationManager.Get("attack_speed", "Attack Speed"),
                            WikiFormat.Number(perSecond, 2) + "/s");
                anyOverride = true;
            }
            if (evo.RangeOverride >= 0f)
            {
                section.Row(LocalizationManager.Get("range", "Range"), WikiFormat.Number(evo.RangeOverride));
                anyOverride = true;
            }

            if (!anyOverride)
                section.Body = LocalizationManager.Get("wiki_evo_no_stat_change",
                    "Keeps the base turret's stats and changes its behaviour instead.");

            sections.Add(section);
        }

        /// <summary>
        /// Suppression Field is an evolution, not a separate tower, so players looking
        /// for its numbers land here. Its slow is the one aura NOT scaled by AuraPower.
        /// </summary>
        private void AddSuppressionSection(List<WikiSection> sections)
        {
            var pathB = _data.PathB;
            if (_data.Type != TurretType.Support || pathB == null || pathB.EnemySlowAura <= 0f)
                return;

            var section = new WikiSection(
                LocalizationManager.Get("wiki_suppression_heading", "Suppression Field slow"),
                LocalizationManager.Get("wiki_suppression_body",
                    "Both the slow and the radius grow with level. The radius is this turret's " +
                    "own range, so range bonuses widen the field."));

            section.Row(LocalizationManager.Get("wiki_suppression_base", "Slow at level 1"),
                        WikiFormat.Percent(pathB.EnemySlowAura));
            section.Row(LocalizationManager.Get("wiki_suppression_per_level", "Slow per level"),
                        WikiFormat.PercentSigned(_data.SupportEnemySlowAuraPerLevel, 1));

            foreach (int level in SampleLevels)
            {
                if (level == 1) continue;
                float slow = TurretStatMath.SupportEnemySlow(
                    pathB.EnemySlowAura, _data.SupportEnemySlowAuraPerLevel, level - 1);
                section.Row(LocalizationManager.GetFormat("wiki_level", "Level {0}", level),
                            WikiFormat.Percent(slow, 1));
            }

            section.Row(LocalizationManager.Get("wiki_suppression_aura_power", "Aura Power trait"),
                        LocalizationManager.Get("wiki_no_effect", "No effect"),
                        LocalizationManager.Get("wiki_suppression_aura_power_note",
                            "Aura Power raises damage and attack-speed auras only. It does not strengthen this slow."));

            sections.Add(section);
        }
    }
}
