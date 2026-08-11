// ============================================================================
// ETD.UI.Wiki - WikiTurretPage.cs  [NEW]
// Per-turret page with a computed level table. Every value runs through
// ETD.Data.TurretStatMath, the same math a placed turret uses, so the table is
// correct without a turret existing in the scene.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;
using ETD.Meta;

namespace ETD.UI.Wiki
{
    public sealed class TurretWikiPage : IWikiPage, IWikiListEntry
    {
        /// <summary>Levels sampled in the scaling table. Chosen to show the curve's shape.</summary>
        private static readonly int[] SampleLevels = { 1, 10, 25, 50 };

        private readonly TurretData _data;

        /// <summary>
        /// Support and Radar never fire. Their Damage and AttackInterval fields still hold
        /// values, so anything reading them straight off the asset would print numbers the
        /// game never uses — see RunStatModifiers.TurretTypeAttacks.
        /// </summary>
        private bool Attacks => _data != null
                                && _data.Type != TurretType.Support
                                && _data.Type != TurretType.Radar;

        public TurretWikiPage(TurretData data)
        {
            _data = data;
        }

        public string Id => "turret_" + (_data != null ? _data.Id : "null");

        public string Title => _data == null
            ? "?"
            : SOLocalization.GetName("turret_" + _data.LocalizationKey, _data.DisplayName);

        public string Category => LocalizationManager.Get("wiki_category_turrets", "TURRETS");

        public Sprite Icon => _data != null ? _data.Icon : null;

        /// <summary>
        /// Unlock state is resolved per query rather than cached, so a turret unlocked
        /// during this session stops reading as locked without the window being rebuilt.
        /// </summary>
        public bool IsLocked
        {
            get
            {
                if (_data == null || _data.IsUnlockedByDefault) return false;
                return ServiceLocator.TryGet<MetaProgressionManager>(out var meta)
                       && !meta.IsTurretUnlocked(_data.Id);
            }
        }

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();
            if (_data == null) return sections;

            // Locked turrets lead with how to unlock them. Listing every turret including
            // the ones you cannot build yet is the point of the page — a player should be
            // able to see what exists and what it costs to get there.
            AddUnlockSection(sections);

            string description = SOLocalization.GetDesc("turret_" + _data.LocalizationKey, _data.Description);
            var overview = new WikiSection(
                LocalizationManager.Get("wiki_overview", "Overview"),
                string.IsNullOrWhiteSpace(description) ? null : description);

            AddFormsStrip(overview);
            AddBaseStats(overview);
            sections.Add(overview);

            sections.Add(BuildScalingSection(_data.Damage, _data.AttackInterval, _data.Range,
                LocalizationManager.Get("wiki_turret_scaling", "Scaling by level")));

            AddEvolutionSection(sections, _data.PathA,
                LocalizationManager.Get("wiki_path_a", "Evolution A"), "path_a", _data.EvolveLevel);
            AddEvolutionSection(sections, _data.PathB,
                LocalizationManager.Get("wiki_path_b", "Evolution B"), "path_b", _data.EvolveLevel);
            AddEvolutionSection(sections, _data.Tier2,
                LocalizationManager.Get("wiki_path_c", "Evolution C"), "tier2", _data.EvolveLevel2);

            AddSuppressionSection(sections);
            AddScannerSections(sections);

            return sections;
        }

        /// <summary>
        /// Base, Path A, Path B and Tier 2 icons in one strip. Slots with no distinct
        /// artwork are skipped rather than repeating the base icon, so the strip only ever
        /// claims a form exists when it really has its own look.
        /// </summary>
        private void AddFormsStrip(WikiSection section)
        {
            section.IconEntry(_data.Icon,
                LocalizationManager.Get("wiki_form_base", "Base"),
                Title, 0);

            AddFormEntry(section, _data.PathA, LocalizationManager.Get("wiki_path_a", "Evolution A"), 1);
            AddFormEntry(section, _data.PathB, LocalizationManager.Get("wiki_path_b", "Evolution B"), 2);
            AddFormEntry(section, _data.Tier2, LocalizationManager.Get("wiki_path_c", "Evolution C"), 3);
        }

        private void AddFormEntry(WikiSection section, TurretEvolutionData evo, string caption, int slot)
        {
            if (evo == null || evo.Icon == null || evo.Icon == _data.Icon)
                return;

            string key = "turret_" + _data.LocalizationKey + "_" +
                         (evo == _data.PathA ? "path_a" : evo == _data.PathB ? "path_b" : "tier2");

            section.IconEntry(evo.Icon, caption, SOLocalization.GetName(key, evo.Name), slot);
        }

        /// <summary>
        /// Level-1 stats as glyph + number. The icons and their colours come from
        /// WikiStatIcons so damage/range/speed look the same here as they did on the
        /// Turrets tab, and a unit is appended wherever the bare number is ambiguous.
        /// </summary>
        private void AddBaseStats(WikiSection section)
        {
            if (Attacks)
            {
                section.StatRow(WikiStatIcons.Damage, WikiStatIcons.DamageColor,
                    LocalizationManager.Get("damage", "Damage"),
                    _data.IsContinuousBeam
                        ? WikiFormat.Number(_data.Damage, 0) + " " + LocalizationManager.Get("wiki_unit_dps", "DPS")
                        : WikiFormat.Number(_data.Damage, 0));
            }

            // Radar's Range field is unread at runtime — RevealRange is the real reach —
            // so showing it would state a number the game never uses.
            if (_data.Type != TurretType.Radar)
            {
                section.StatRow(WikiStatIcons.Range, WikiStatIcons.RangeColor,
                    LocalizationManager.Get("range", "Range"),
                    WikiFormat.Number(_data.Range, 1) + " " + LocalizationManager.Get("wiki_unit_tiles", "tiles"));
            }

            // A beam has no discrete shot, and a tower that never fires has no rate.
            bool hasRate = Attacks && !_data.IsContinuousBeam;

            if (hasRate)
            {
                float perSecond = _data.AttackInterval > 0f ? 1f / _data.AttackInterval : 0f;
                section.StatRow(WikiStatIcons.Speed, WikiStatIcons.SpeedColor,
                    LocalizationManager.Get("attack_speed", "Attack Speed"),
                    WikiFormat.Number(perSecond, 2) + " " +
                    LocalizationManager.Get("wiki_unit_per_second", "shots/s"));
            }

            if (_data.Type == TurretType.Radar)
            {
                section.StatRow(WikiStatIcons.Range, WikiStatIcons.SpeedColor,
                    LocalizationManager.Get("wiki_radar_reveal_heading", "Reveal range"),
                    WikiFormat.Number(_data.RevealRange, 1) + " " +
                    LocalizationManager.Get("wiki_unit_tiles", "tiles"));
            }
        }

        /// <summary>
        /// Unlock condition with live progress, carried over from the old Turrets window.
        /// Unlocked turrets get a one-line confirmation instead of nothing, so the section
        /// does not appear and disappear as the player scrolls the list.
        /// </summary>
        private void AddUnlockSection(List<WikiSection> sections)
        {
            if (!IsLocked)
                return;

            // isAlert: drawn in red by WikiWindowUI. This section is the first thing on the
            // page, directly above the forms strip, so a locked turret states what it costs
            // to get before it shows off what it becomes.
            var section = new WikiSection(
                LocalizationManager.Get("wiki_locked_heading", "Locked"),
                LocalizationManager.Get("wiki_locked_body",
                    "This turret is not available yet. Meet the condition below to add it to " +
                    "your build options."),
                isAlert: true);

            string condition = SOLocalization.GetUnlock(
                "turret_" + _data.LocalizationKey, _data.UnlockCondition);

            if (string.IsNullOrWhiteSpace(condition))
                condition = LocalizationManager.Get("wiki_locked_unknown", "Keep playing to unlock.");

            if (_data.UnlockConditionTarget > 0f)
            {
                float progress = Mathf.Min(
                    SaveSystem.GetChallengeProgress(SaveSystem.Load(), (int)_data.UnlockConditionType),
                    _data.UnlockConditionTarget);

                section.Row(condition,
                    WikiFormat.Number(progress, 0) + " / " + WikiFormat.Number(_data.UnlockConditionTarget, 0));
            }
            else
            {
                section.Row(LocalizationManager.Get("wiki_unlock_condition", "Requirement"), condition);
            }

            sections.Add(section);
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

                // The level is its own label row, then the stats follow as icon rows. The
                // label breaks the run, so each level gets its own horizontal stat bar
                // rather than every level's numbers running together on one line.
                section.Subheading(LocalizationManager.GetFormat("wiki_level_short", "Lv {0}", level));

                if (Attacks)
                {
                    section.StatRow(WikiStatIcons.Damage, WikiStatIcons.DamageColor,
                        LocalizationManager.Get("damage", "Damage"),
                        _data.IsContinuousBeam
                            ? WikiFormat.Number(damage) + " " + LocalizationManager.Get("wiki_unit_dps", "DPS")
                            : WikiFormat.Number(damage));
                }

                section.StatRow(WikiStatIcons.Range, WikiStatIcons.RangeColor,
                    LocalizationManager.Get("range", "Range"),
                    WikiFormat.Number(range) + " " + LocalizationManager.Get("wiki_unit_tiles", "tiles"));

                // Continuous beams express damage as DPS and have no meaningful interval.
                if (Attacks && !_data.IsContinuousBeam)
                {
                    float interval = TurretStatMath.LeveledAttackInterval(
                        baseInterval, _data.AttackSpeedPerLevel, levelIndex,
                        BalanceConstants.MinProjectileAttackInterval);
                    float perSecond = interval > 0f ? 1f / interval : 0f;
                    section.StatRow(WikiStatIcons.Speed, WikiStatIcons.SpeedColor,
                        LocalizationManager.Get("attack_speed", "Attack Speed"),
                        WikiFormat.Number(perSecond, 2) + " " +
                        LocalizationManager.Get("wiki_unit_per_second", "shots/s"));
                }
            }

            return section;
        }

        /// <summary>
        /// One evolution, rendered the same way whichever slot it is. Every path gets a
        /// one-line description and a full stat bar led by the level it unlocks at —
        /// previously a path with no stat overrides (Tier 2 on most turrets) rendered as a
        /// bare heading and read as though it were a different kind of thing.
        /// </summary>
        private void AddEvolutionSection(List<WikiSection> sections, TurretEvolutionData evo,
                                         string heading, string locSuffix, int unlockLevel)
        {
            if (evo == null) return;

            string name = SOLocalization.GetName(
                "turret_" + _data.LocalizationKey + "_" + locSuffix, evo.Name);

            var section = new WikiSection(
                heading + (string.IsNullOrWhiteSpace(name) ? "" : ": " + name),
                SOLocalization.GetDesc("turret_" + _data.LocalizationKey + "_" + locSuffix, evo.Description));

            section.Subheading(LocalizationManager.GetFormat("wiki_level_short", "Lv {0}", unlockLevel));

            // Unset overrides fall back to the base stat, so the bar always states what the
            // evolved turret actually has rather than only what changed.
            if (Attacks)
            {
                float damage = evo.DamageOverride >= 0f ? evo.DamageOverride : _data.Damage;
                section.StatRow(WikiStatIcons.Damage, WikiStatIcons.DamageColor,
                    LocalizationManager.Get("damage", "Damage"),
                    _data.IsContinuousBeam
                        ? WikiFormat.Number(damage, 0) + " " + LocalizationManager.Get("wiki_unit_dps", "DPS")
                        : WikiFormat.Number(damage, 0));
            }

            float range = evo.RangeOverride >= 0f ? evo.RangeOverride : _data.Range;
            if (_data.Type != TurretType.Radar)
            {
                section.StatRow(WikiStatIcons.Range, WikiStatIcons.RangeColor,
                    LocalizationManager.Get("range", "Range"),
                    WikiFormat.Number(range, 1) + " " + LocalizationManager.Get("wiki_unit_tiles", "tiles"));
            }
            else
            {
                section.StatRow(WikiStatIcons.Range, WikiStatIcons.SpeedColor,
                    LocalizationManager.Get("wiki_radar_reveal_heading", "Reveal range"),
                    WikiFormat.Number(_data.RevealRange, 1) + " " +
                    LocalizationManager.Get("wiki_unit_tiles", "tiles"));
            }

            if (Attacks && !_data.IsContinuousBeam)
            {
                float interval = evo.AttackIntervalOverride >= 0f
                    ? evo.AttackIntervalOverride : _data.AttackInterval;
                float perSecond = interval > 0f ? 1f / interval : 0f;
                section.StatRow(WikiStatIcons.Speed, WikiStatIcons.SpeedColor,
                    LocalizationManager.Get("attack_speed", "Attack Speed"),
                    WikiFormat.Number(perSecond, 2) + " " +
                    LocalizationManager.Get("wiki_unit_per_second", "shots/s"));
            }

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

        /// <summary>
        /// The Scanner's evolutions change no base stat, so the generic evolution
        /// sections above would render them as "behaviour only" and hide the numbers
        /// that actually matter. Both paths key off reveal range, which is also the only
        /// stat the Scanner's levels buy, so it is listed here rather than in the
        /// generic scaling table (which reports the unused combat Range).
        /// </summary>
        private void AddScannerSections(List<WikiSection> sections)
        {
            if (_data.Type != TurretType.Radar)
                return;

            var reveal = new WikiSection(
                LocalizationManager.Get("wiki_radar_reveal_heading", "Reveal range"),
                LocalizationManager.Get("wiki_radar_reveal_body",
                    "Reveal range is the Scanner's real stat: it sets the reveal bubble and " +
                    "the radius of both evolutions. Signature bonuses scale it."));

            foreach (int level in SampleLevels)
            {
                float range = _data.RevealRange + Mathf.Max(0f, _data.RevealRangePerLevel) * (level - 1);
                reveal.Row(LocalizationManager.GetFormat("wiki_level", "Level {0}", level),
                           WikiFormat.Number(range));
            }
            sections.Add(reveal);

            var pathA = _data.PathA;
            if (pathA != null && pathA.MarkEnemies)
            {
                var section = new WikiSection(
                    LocalizationManager.Get("wiki_radar_mark_heading", "Fire Control Array mark"),
                    LocalizationManager.Get("wiki_radar_mark_body",
                        "Every enemy inside the reveal range is marked, stealth or not. The mark " +
                        "raises the damage they take from every source, and does not stack " +
                        "between multiple Scanners — the strongest mark wins."));

                foreach (int level in SampleLevels)
                {
                    float amp = pathA.MarkDamageAmp + Mathf.Max(0f, pathA.MarkAmpPerLevel) * (level - 1);
                    section.Row(LocalizationManager.GetFormat("wiki_level", "Level {0}", level),
                                WikiFormat.PercentSigned(amp, 1));
                }

                AddTier2MarkRows(section);
                sections.Add(section);
            }

            var pathB = _data.PathB;
            if (pathB != null && pathB.ShareStealthVision)
            {
                var section = new WikiSection(
                    LocalizationManager.Get("wiki_radar_uplink_heading", "Spotter Uplink"),
                    LocalizationManager.Get("wiki_radar_uplink_body",
                        "Turrets inside the reveal range can target stealth enemies anywhere in " +
                        "their own range, not only inside the bubble, and gain extra range."));

                foreach (int level in SampleLevels)
                {
                    float bonus = pathB.AllyRangeAura + Mathf.Max(0f, pathB.AllyRangeAuraPerLevel) * (level - 1);
                    section.Row(LocalizationManager.GetFormat("wiki_level", "Level {0}", level),
                                WikiFormat.PercentSigned(bonus, 1));
                }

                AddTier2UplinkRows(section);
                sections.Add(section);
            }
        }

        private void AddTier2MarkRows(WikiSection section)
        {
            var tier2 = _data.Tier2;
            if (tier2 == null) return;

            if (tier2.MarkArmorShredBonus > 0f)
            {
                section.Row(LocalizationManager.Get("wiki_radar_tier2_shred", "Tier 2: armor stripped"),
                            WikiFormat.Percent(tier2.MarkArmorShredBonus, 0));
            }
            if (tier2.MarkCurrentHPPercentPerSecond > 0f)
            {
                section.Row(LocalizationManager.Get("wiki_radar_tier2_pulse", "Tier 2: HP drained per second"),
                            WikiFormat.Percent(tier2.MarkCurrentHPPercentPerSecond, 1));
            }
            if (tier2.MarkLingerDuration > 0f)
            {
                section.Row(LocalizationManager.Get("wiki_radar_tier2_linger", "Tier 2: mark lingers"),
                            WikiFormat.Number(tier2.MarkLingerDuration, 1) + "s");
            }
        }

        private void AddTier2UplinkRows(WikiSection section)
        {
            var tier2 = _data.Tier2;
            if (tier2 == null) return;

            if (tier2.AllyRangeAura > 0f)
            {
                section.Row(LocalizationManager.Get("wiki_radar_tier2_range", "Tier 2: extra ally range"),
                            WikiFormat.PercentSigned(tier2.AllyRangeAura, 0));
            }
            if (tier2.AllyCritDamageAura > 0f)
            {
                section.Row(LocalizationManager.Get("wiki_radar_tier2_crit", "Tier 2: ally crit damage"),
                            WikiFormat.PercentSigned(tier2.AllyCritDamageAura, 0));
            }
        }
    }
}
