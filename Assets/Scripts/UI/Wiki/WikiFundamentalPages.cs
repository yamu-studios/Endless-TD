// ============================================================================
// ETD.UI.Wiki - WikiFundamentalPages.cs  [NEW]
// The FUNDAMENTALS tier of the mechanics wiki: the systems every player is
// already interacting with in their first run, whether or not they know the
// rules. Mitigation explains why a turret "does nothing" to some enemies,
// targeting explains what a turret shoots at, economy explains where gold goes.
//
// Same contract as WikiMechanicPages: numbers are COMPUTED from
// ETD.Data.BalanceConstants / GameConstants / turret assets, never authored into
// a loc string, so rebalancing cannot leave the wiki lying.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;

namespace ETD.UI.Wiki
{
    /// <summary>
    /// Headings the mechanic pages are filed under. Previously every non-turret page
    /// fell into one undifferentiated MECHANICS bucket, which gave a first-run player
    /// and a wave-80 optimizer the same undirected list.
    /// </summary>
    public static class WikiCategories
    {
        public static string Fundamentals
            => LocalizationManager.Get("wiki_category_fundamentals", "FUNDAMENTALS");

        public static string StatusEffects
            => LocalizationManager.Get("wiki_category_status", "STATUS EFFECTS");

        public static string RunSystems
            => LocalizationManager.Get("wiki_category_run", "RUN SYSTEMS");
    }

    // =====================================================================
    // DAMAGE & MITIGATION
    // =====================================================================

    public sealed class DamageMitigationWikiPage : IWikiPage, IWikiListEntry
    {
        public string Id => "mitigation";
        public string Title => LocalizationManager.Get("wiki_page_mitigation", "Damage & Mitigation");

        public string Category => WikiCategories.Fundamentals;
        public Sprite Icon => null;
        public bool IsLocked => false;

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();

            sections.Add(new WikiSection(
                LocalizationManager.Get("wiki_mit_rule_heading", "Damage is reduced twice before it lands"),
                LocalizationManager.Get("wiki_mit_rule_body",
                    "Turret-type affinity applies first: an enemy resistant to Frost takes less " +
                    "from every Frost source, and nothing else. Armor applies second and reduces " +
                    "every source equally. The two multiply, so a resistant AND armored enemy is " +
                    "much tougher than either stat alone suggests.")));

            var formula = new WikiSection(
                LocalizationManager.Get("wiki_mit_formula_heading", "The formula"),
                LocalizationManager.Get("wiki_mit_formula_body",
                    "raw damage x (1 - affinity) x (1 - armor) x (1 + expose)"));

            formula.Row(LocalizationManager.Get("wiki_mit_armor_cap", "Armor, maximum"),
                        WikiFormat.Percent(BalanceConstants.MaxArmorMitigation),
                        LocalizationManager.Get("wiki_mit_armor_cap_note",
                            "No enemy mitigates more than this, however high the wave."));
            formula.Row(LocalizationManager.Get("wiki_mit_resist_cap", "Resistance, maximum"),
                        WikiFormat.Percent(BalanceConstants.MaxAffinityResistance),
                        LocalizationManager.Get("wiki_mit_resist_cap_note",
                            "Applies only to the turret type the enemy resists."));
            formula.Row(LocalizationManager.Get("wiki_mit_weak_cap", "Weakness, maximum"),
                        WikiFormat.Multiplier(1f - BalanceConstants.MaxAffinityWeakness),
                        LocalizationManager.Get("wiki_mit_weak_cap_note",
                            "Affinity can go negative. A turret type an enemy is weak to deals up to double."));
            formula.Row(LocalizationManager.Get("wiki_mit_min_hit", "Minimum per mitigated hit"),
                        WikiFormat.Number(BalanceConstants.MinDamagePerHit, 0),
                        LocalizationManager.Get("wiki_mit_min_hit_note",
                            "Mitigation can stall a build but never completely wall it."));
            sections.Add(formula);

            var pierce = new WikiSection(
                LocalizationManager.Get("wiki_mit_pierce_heading", "Cutting through it"),
                LocalizationManager.Get("wiki_mit_pierce_body",
                    "Four different effects reduce mitigation, and they compose rather than " +
                    "overwriting each other."));

            pierce.Row(LocalizationManager.Get("wiki_mit_armor_pierce", "Armor Pierce"),
                       LocalizationManager.Get("wiki_mit_armor_pierce_val", "subtracted from armor"),
                       LocalizationManager.Get("wiki_mit_armor_pierce_note",
                           "A flat subtraction that works on every enemy, whatever its type."));
            pierce.Row(LocalizationManager.Get("wiki_mit_affinity_pierce", "Affinity Pierce"),
                       LocalizationManager.Get("wiki_mit_affinity_pierce_val", "reduces resistance only"),
                       LocalizationManager.Get("wiki_mit_affinity_pierce_note",
                           "It cannot deepen a weakness. Against an enemy already weak to that turret type it does nothing."));
            pierce.Row(LocalizationManager.Get("wiki_mit_weaken", "Weaken (Void)"),
                       LocalizationManager.Get("wiki_mit_weaken_val", "corrodes armor for a duration"),
                       LocalizationManager.Get("wiki_mit_weaken_note",
                           "A timed status, so it benefits the whole team while it lasts, not only the Void turret."));
            pierce.Row(LocalizationManager.Get("wiki_mit_shred", "Armor shred (Scanner Tier 2)"),
                       LocalizationManager.Get("wiki_mit_shred_val", "multiplies armor down"),
                       LocalizationManager.Get("wiki_mit_shred_note",
                           "Applied before the flat pierces, so shred and pierce stack usefully instead of one making the other redundant."));
            sections.Add(pierce);

            var expose = new WikiSection(
                LocalizationManager.Get("wiki_mit_expose_heading", "Expose amplifies what gets through"),
                LocalizationManager.Get("wiki_mit_expose_body",
                    "Expose (Railgun) and the Scanner's mark share ONE additive bucket, applied " +
                    "last — after resistance and armor. Because it scales the damage that " +
                    "survived mitigation, it is worth most on targets you are already hurting, " +
                    "and least on ones you cannot dent."));
            sections.Add(expose);

            var pure = new WikiSection(
                LocalizationManager.Get("wiki_mit_pure_heading", "Pure damage ignores all of it"),
                LocalizationManager.Get("wiki_mit_pure_body",
                    "Toxin's signature damage and health-decay effects bypass affinity, armor and " +
                    "the minimum-damage floor entirely. They gain nothing from Expose either. " +
                    "Against a heavily armored boss, pure damage is the most predictable damage " +
                    "in the game."));
            sections.Add(pure);

            if (mods != null)
            {
                var live = new WikiSection(WikiFormat.YourBuildLabel);
                live.Row(LocalizationManager.Get("wiki_mit_live_armor_pierce", "Armor pierce"),
                         WikiFormat.Percent(mods.GetArmorPierce(), 1),
                         LocalizationManager.Get("wiki_mit_live_armor_pierce_note",
                             "Subtracted from every enemy's armor."));
                live.Row(LocalizationManager.Get("wiki_mit_live_affinity_pierce", "Affinity pierce"),
                         WikiFormat.Percent(mods.GetAffinityPierce(), 1));

                // Only meaningful while the Plague Covenant is active; showing a flat 0%
                // to every other build would just be noise on a page that is already dense.
                float leak = mods.GetPlagueLeakFraction();
                if (leak > 0f)
                {
                    live.Row(LocalizationManager.Get("wiki_mit_live_plague", "Plague Covenant leak"),
                             WikiFormat.Percent(leak, 1),
                             LocalizationManager.Get("wiki_mit_live_plague_note",
                                 "Share of Toxin's pure damage that your non-Toxin turrets also apply."));
                }
                sections.Add(live);
            }

            return sections;
        }
    }

    // =====================================================================
    // TARGETING & PLACEMENT
    // =====================================================================

    public sealed class TargetingWikiPage : IWikiPage, IWikiListEntry
    {
        public string Id => "targeting";
        public string Title => LocalizationManager.Get("wiki_page_targeting", "Targeting & Placement");

        public string Category => WikiCategories.Fundamentals;
        public Sprite Icon => null;
        public bool IsLocked => false;

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();

            sections.Add(new WikiSection(
                LocalizationManager.Get("wiki_target_rule_heading", "Every turret picks one target per shot"),
                LocalizationManager.Get("wiki_target_rule_body",
                    "Which enemy it picks is set by that turret's targeting mode. You can change " +
                    "it per turret from the turret panel, and the choice is saved with your run. " +
                    "Support and Scanner have no mode — they affect everything in radius instead.")));

            var modes = new WikiSection(LocalizationManager.Get("wiki_target_modes_heading", "The four modes"));

            modes.Row(LocalizationManager.Get("targeting_mode_first", "First"),
                      LocalizationManager.Get("wiki_target_first_val", "closest to your base"),
                      LocalizationManager.Get("wiki_target_first_note",
                          "Measured by progress along the path, not spawn order. The default, and the right answer when leaks are what is killing you."));
            modes.Row(LocalizationManager.Get("targeting_mode_last", "Last"),
                      LocalizationManager.Get("wiki_target_last_val", "furthest from your base"),
                      LocalizationManager.Get("wiki_target_last_note",
                          "Softens a wave before it reaches your killzone. Pairs well with slows and burns, which need time to pay off."));
            modes.Row(LocalizationManager.Get("targeting_mode_strongest", "Strongest"),
                      LocalizationManager.Get("wiki_target_strongest_val", "highest maximum health"),
                      LocalizationManager.Get("wiki_target_strongest_note",
                          "Judged on MAXIMUM health, not current, so the turret does not drift off a target as it is worn down. Locks onto elites and bosses."));
            modes.Row(LocalizationManager.Get("targeting_mode_closest", "Closest"),
                      LocalizationManager.Get("wiki_target_closest_val", "nearest to the turret"),
                      LocalizationManager.Get("wiki_target_closest_note",
                          "Shortest projectile travel, so the least damage wasted on enemies that die in flight. Best for slow projectiles."));
            sections.Add(modes);

            var blind = new WikiSection(
                LocalizationManager.Get("wiki_target_blind_heading", "What a turret cannot see"),
                LocalizationManager.Get("wiki_target_blind_body",
                    "A target that cannot be seen is not a target at all — no targeting mode " +
                    "reaches it, and damage bonuses do nothing about it."));

            blind.Row(LocalizationManager.Get("wiki_target_stealth", "Stealth enemies"),
                      LocalizationManager.Get("wiki_target_stealth_val", "need a revealer"),
                      LocalizationManager.Get("wiki_target_stealth_note",
                          "Only turrets that can target stealth engage them. A Scanner reveals them for everything nearby, which is what it is for."));
            blind.Row(LocalizationManager.Get("wiki_target_flying", "Flying enemies"),
                      LocalizationManager.Get("wiki_target_flying_val", "ignore the path"),
                      LocalizationManager.Get("wiki_target_flying_note",
                          "They fly straight from entry to exit, covering far less ground and spending much less time in range. Cover that straight line, not only the road."));
            sections.Add(blind);

            var placement = new WikiSection(
                LocalizationManager.Get("wiki_target_placement_heading", "Placement reshapes the path"),
                LocalizationManager.Get("wiki_target_placement_body",
                    "Turrets occupy tiles and enemies walk around them, so where you build " +
                    "changes the route — and a longer route means more time inside your ranges. " +
                    "You can never seal the path completely: a placement that would leave no " +
                    "route to the exit is rejected. Lengthening the walk is a real strategy; " +
                    "walling is not an option."));
            sections.Add(placement);

            if (mods != null)
            {
                var live = new WikiSection(WikiFormat.YourBuildLabel);
                live.Row(LocalizationManager.Get("wiki_global_range", "Global range multiplier"),
                         WikiFormat.Multiplier(mods.GetGlobalRangeMultiplier()));
                live.Row(LocalizationManager.Get("wiki_target_support_radius", "Support/Scanner radius multiplier"),
                         WikiFormat.Multiplier(mods.GetSupportRadiusMultiplier()));
                sections.Add(live);
            }

            return sections;
        }
    }

    // =====================================================================
    // ECONOMY & UPGRADES
    // =====================================================================

    public sealed class EconomyWikiPage : IWikiPage, IWikiListEntry
    {
        /// <summary>Levels sampled in the upgrade-cost curve. Matches the turret pages' shape.</summary>
        private static readonly int[] SampleLevels = { 5, 10, 25, 50 };

        private readonly GameDatabase _database;

        public EconomyWikiPage(GameDatabase database)
        {
            _database = database;
        }

        public string Id => "economy";
        public string Title => LocalizationManager.Get("wiki_page_economy", "Economy & Upgrades");

        public string Category => WikiCategories.Fundamentals;
        public Sprite Icon => null;
        public bool IsLocked => false;

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();

            float growth = FindUpgradeCostMultiplier();

            sections.Add(new WikiSection(
                LocalizationManager.Get("wiki_econ_rule_heading", "Upgrade cost compounds faster than damage"),
                LocalizationManager.GetFormat("wiki_econ_rule_body",
                    "Each upgrade costs {0} of the last, while damage grows more slowly. Cost is " +
                    "deliberately priced at roughly the SQUARE of power: doubling a turret's " +
                    "damage costs about four times the gold. Deep upgrades and new turrets are " +
                    "meant to stay competitive with each other — neither is a trap.",
                    WikiFormat.Multiplier(growth))));

            var curve = new WikiSection(
                LocalizationManager.Get("wiki_econ_curve_heading", "Upgrade cost by level"),
                LocalizationManager.Get("wiki_econ_curve_body",
                    "As a multiple of the turret's level-1 upgrade cost."));
            for (int i = 0; i < SampleLevels.Length; i++)
            {
                int level = SampleLevels[i];
                curve.Row(LocalizationManager.GetFormat("wiki_level", "Level {0}", level),
                          WikiFormat.MultiplierLarge(Mathf.Pow(growth, level - 1)));
            }
            sections.Add(curve);

            var invest = new WikiSection(LocalizationManager.Get("wiki_econ_invest_heading", "Selling and evolving"));
            invest.Row(LocalizationManager.Get("wiki_econ_sell", "Sell refund"),
                       WikiFormat.Percent(GameConstants.TURRET_SELL_REFUND_PERCENT),
                       LocalizationManager.Get("wiki_econ_sell_note",
                           "Of the TOTAL gold invested, upgrades included — so relocating a developed turret is not ruinous."));
            invest.Row(LocalizationManager.Get("wiki_econ_evolve", "Evolution"),
                       LocalizationManager.GetFormat("wiki_econ_evolve_val", "level {0}", GameConstants.TURRET_EVOLVE_LEVEL),
                       LocalizationManager.Get("wiki_econ_evolve_note",
                           "Choosing a path is free. The second tier is not."));
            invest.Row(LocalizationManager.Get("wiki_econ_tier2", "Tier 2 cost"),
                       LocalizationManager.Get("wiki_econ_tier2_val", "one upgrade at current level"),
                       LocalizationManager.Get("wiki_econ_tier2_note",
                           "Priced off the curve above, so evolving a high-level turret is expensive by design."));
            sections.Add(invest);

            var income = new WikiSection(LocalizationManager.Get("wiki_econ_income_heading", "Income"));
            income.Row(LocalizationManager.Get("wiki_econ_start_gold", "Starting gold"),
                       GameConstants.STARTING_GOLD.ToString());
            income.Row(LocalizationManager.Get("wiki_econ_start_lives", "Starting lives"),
                       GameConstants.STARTING_LIVES.ToString());
            income.Row(LocalizationManager.Get("wiki_econ_kill_gold", "Gold per kill grows with the wave"),
                       WikiFormat.PercentSigned(GameConstants.GOLD_KILL_WAVE_SCALE),
                       LocalizationManager.Get("wiki_econ_kill_gold_note",
                           "Of the enemy's base reward, per wave reached. Income keeps pace with enemy health."));
            income.Row(LocalizationManager.Get("wiki_econ_elite_gold", "Elite kill"),
                       WikiFormat.Multiplier(GameConstants.ELITE_GOLD_MULTIPLIER));
            income.Row(LocalizationManager.Get("wiki_econ_boss_gold", "Boss kill"),
                       WikiFormat.Multiplier(GameConstants.BOSS_GOLD_MULTIPLIER));
            sections.Add(income);

            var levels = new WikiSection(
                LocalizationManager.Get("wiki_econ_xp_heading", "Levels and spec cards"),
                LocalizationManager.Get("wiki_econ_xp_body",
                    "Kills award XP as well as gold. Levelling offers spec cards, so clearing " +
                    "waves faster compounds into a stronger build, not just more gold."));
            levels.Row(LocalizationManager.Get("wiki_econ_xp_first", "XP for level 2"),
                       WikiFormat.Number(GameConstants.BASE_XP_REQUIRED, 0));
            levels.Row(LocalizationManager.Get("wiki_econ_xp_growth", "XP required per level"),
                       WikiFormat.Multiplier(GameConstants.XP_SCALING_FACTOR));
            levels.Row(LocalizationManager.Get("wiki_econ_xp_kill", "Kill XP grows with the wave"),
                       WikiFormat.PercentSigned(GameConstants.XP_KILL_WAVE_SCALE));
            levels.Row(LocalizationManager.Get("wiki_econ_cards", "Cards offered per level"),
                       GameConstants.SPEC_CARDS_PER_LEVELUP.ToString());
            sections.Add(levels);

            if (mods != null)
            {
                var live = new WikiSection(WikiFormat.YourBuildLabel);
                live.Row(LocalizationManager.Get("wiki_econ_live_cost", "Upgrade cost multiplier"),
                         WikiFormat.Multiplier(mods.GetUpgradeCostMultiplier()),
                         LocalizationManager.Get("wiki_econ_live_cost_note",
                             "Below x1.00 is a discount. Cursed and Greed tiles change this per turret on top of it."));
                sections.Add(live);
            }

            return sections;
        }

        /// <summary>
        /// Read the cost growth off the real turret assets rather than hardcoding it, so
        /// retuning the assets retunes this page. Turrets share the value today; if one
        /// ever diverges the most common value is still the honest thing to quote.
        /// </summary>
        private float FindUpgradeCostMultiplier()
        {
            const float fallback = 1.325f;
            if (_database?.Turrets == null) return fallback;

            float best = 0f;
            int bestCount = 0;

            for (int i = 0; i < _database.Turrets.Length; i++)
            {
                var candidate = _database.Turrets[i];
                if (candidate == null || candidate.UpgradeCostMultiplier <= 1f) continue;

                int count = 0;
                for (int j = 0; j < _database.Turrets.Length; j++)
                {
                    var other = _database.Turrets[j];
                    if (other != null && Mathf.Approximately(other.UpgradeCostMultiplier,
                                                             candidate.UpgradeCostMultiplier))
                        count++;
                }

                if (count > bestCount)
                {
                    bestCount = count;
                    best = candidate.UpgradeCostMultiplier;
                }
            }

            return best > 1f ? best : fallback;
        }
    }
}
