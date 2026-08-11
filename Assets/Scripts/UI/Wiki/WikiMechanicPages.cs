// ============================================================================
// ETD.UI.Wiki - WikiMechanicPages.cs  [NEW]
// The four pages that answer the build-planning questions players actually ask:
// how bonuses stack, how slow resolves, how burn stacks, how chain scales.
//
// Numbers come from ETD.Data.BalanceConstants / TurretStatMath / turret assets.
// Rules that a number cannot express (max-not-sum, replace-weakest, per-enemy-
// not-per-blast) are carried in row Notes so they are impossible to miss.
// ============================================================================
using System.Collections.Generic;
using ETD.Core;
using ETD.Data;

namespace ETD.UI.Wiki
{
    // =====================================================================
    // INCREASES VS MULTIPLIERS
    // =====================================================================

    public sealed class StatStackingWikiPage : IWikiPage, IWikiListEntry
    {
        public string Id => "stacking";
        public string Title => LocalizationManager.Get("wiki_page_stacking", "Order of Operations");

        public string Category => WikiCategories.Fundamentals;
        public UnityEngine.Sprite Icon => null;
        public bool IsLocked => false;

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();

            sections.Add(new WikiSection(
                LocalizationManager.Get("wiki_stacking_rule_heading", "The rule"),
                LocalizationManager.Get("wiki_stacking_rule_body",
                    "Bonuses to the SAME stat add together first, then that combined total " +
                    "multiplies against everything else. Two +50% damage sources give +100% " +
                    "(x2.0), not x2.25. But a damage bonus and an elite bonus are different " +
                    "buckets, so they multiply.")));

            var order = new WikiSection(
                LocalizationManager.Get("wiki_stacking_order_heading", "Order of operations"),
                LocalizationManager.Get("wiki_stacking_order_body",
                    "Damage resolves in this order. Each step multiplies the result of the last."));

            order.Row("1. " + LocalizationManager.Get("wiki_step_base", "Base damage"),
                      LocalizationManager.Get("wiki_from_turret", "from turret"));
            order.Row("2. " + LocalizationManager.Get("wiki_step_level", "Upgrade levels"),
                      LocalizationManager.Get("wiki_compounding", "compounding"),
                      LocalizationManager.GetFormat("wiki_step_level_note",
                          "Each level multiplies damage, capped at {0} per level.",
                          WikiFormat.PercentSigned(BalanceConstants.MaxDamageGrowthPerLevel)));
            order.Row("3. " + LocalizationManager.Get("wiki_step_global", "Global bonuses"),
                      LocalizationManager.Get("wiki_additive_bucket", "added together"),
                      LocalizationManager.Get("wiki_step_global_note",
                          "All traits and spec cards that raise damage share one bucket."));
            order.Row("4. " + LocalizationManager.Get("wiki_step_tile", "Tile modifiers"),
                      LocalizationManager.Get("wiki_separate_mult", "separate multiplier"));
            order.Row("5. " + LocalizationManager.Get("wiki_step_support", "Support aura"),
                      LocalizationManager.Get("wiki_separate_mult", "separate multiplier"));
            order.Row("6. " + LocalizationManager.Get("wiki_step_conditional", "Conditional bonuses"),
                      LocalizationManager.Get("wiki_separate_mult", "separate multiplier"),
                      LocalizationManager.Get("wiki_step_conditional_note",
                          "vs. elites, vs. burning, vs. slowed/frozen — each is its own multiplier."));
            order.Row("7. " + LocalizationManager.Get("wiki_step_crit", "Critical hit"),
                      WikiFormat.Multiplier(BalanceConstants.BaseCritDamageMultiplier));
            sections.Add(order);

            if (mods != null)
            {
                var live = new WikiSection(WikiFormat.YourBuildLabel);
                live.Row(LocalizationManager.Get("wiki_global_damage", "Global damage multiplier"),
                         WikiFormat.Multiplier(mods.GetGlobalDamageMultiplier()));
                live.Row(LocalizationManager.Get("wiki_global_attack_speed", "Global attack speed multiplier"),
                         WikiFormat.Multiplier(mods.GetGlobalAttackSpeedMultiplier()));
                live.Row(LocalizationManager.Get("wiki_global_range", "Global range multiplier"),
                         WikiFormat.Multiplier(mods.GetGlobalRangeMultiplier()));
                live.Row(LocalizationManager.Get("wiki_vs_elite", "Damage vs. elites/bosses"),
                         WikiFormat.Multiplier(mods.GetDamageVsEliteMultiplier()));
                live.Row(LocalizationManager.Get("wiki_crit_chance", "Critical chance"),
                         WikiFormat.Percent(mods.GetCritChance(), 1));
                sections.Add(live);
            }

            return sections;
        }
    }

    // =====================================================================
    // SLOW
    // =====================================================================

    public sealed class SlowWikiPage : IWikiPage, IWikiListEntry
    {
        public string Id => "slow";
        public string Title => LocalizationManager.Get("wiki_page_slow", "Slow, Freeze & Shock");

        public string Category => WikiCategories.StatusEffects;
        public UnityEngine.Sprite Icon => null;
        public bool IsLocked => false;

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();

            // The single most-asked question: does +100% slow halve enemy speed?
            sections.Add(new WikiSection(
                LocalizationManager.Get("wiki_slow_rule_heading", "Slow bonuses scale the slow, not the speed"),
                LocalizationManager.Get("wiki_slow_rule_body",
                    "A slow-strength bonus increases the slow AMOUNT. It does not halve enemy " +
                    "speed. With +100% slow strength, a 25% slow becomes a 50% slow — the enemy " +
                    "moves at 50% speed, not 25%.")));

            var math = new WikiSection(LocalizationManager.Get("wiki_slow_math_heading", "How it resolves"));
            math.Row(LocalizationManager.Get("wiki_slow_step1", "1. Effective slow"),
                     LocalizationManager.Get("wiki_slow_step1_val", "base slow x (1 + slow strength)"));
            math.Row(LocalizationManager.Get("wiki_slow_step2", "2. Enemy speed"),
                     LocalizationManager.Get("wiki_slow_step2_val", "base speed x (1 - effective slow)"));
            math.Row(LocalizationManager.Get("wiki_slow_floor", "Speed floor"),
                     WikiFormat.Percent(BalanceConstants.MinSlowSpeedMultiplier),
                     LocalizationManager.Get("wiki_slow_floor_note",
                         "Slow alone can never take an enemy below this. Only Freeze and Shock fully stop movement."));
            sections.Add(math);

            var stacking = new WikiSection(
                LocalizationManager.Get("wiki_slow_stacking_heading", "Multiple slow sources"),
                LocalizationManager.Get("wiki_slow_stacking_body",
                    "Slows do NOT add up. If a Frost hit and a Suppression aura both affect an " +
                    "enemy, only the STRONGEST applies. Stacking two weak slows is wasted — raise " +
                    "one instead."));
            sections.Add(stacking);

            var identity = new WikiSection(
                LocalizationManager.Get("wiki_slow_level_heading", "Slow grows with turret level"));
            identity.Row(LocalizationManager.Get("wiki_slow_per_level", "Slow strength & duration"),
                         WikiFormat.PerLevel(BalanceConstants.IdentityScalingPerLevel));
            identity.Row(LocalizationManager.Get("wiki_slow_level_cap", "Maximum from levels"),
                         WikiFormat.PercentSigned(BalanceConstants.IdentityScalingCap),
                         LocalizationManager.GetFormat("wiki_slow_level_cap_note",
                             "Reached at level {0}.",
                             Mathf_RoundToIntSafe(BalanceConstants.IdentityScalingCap / BalanceConstants.IdentityScalingPerLevel) + 1));
            sections.Add(identity);

            // Freeze and Shock are the only things that actually stop movement, which is
            // why they belong on this page rather than one of their own: the question a
            // player has is "why is my slow not enough", and the answer is these.
            var freeze = new WikiSection(
                LocalizationManager.Get("wiki_freeze_heading", "Freeze stops movement completely"),
                LocalizationManager.Get("wiki_freeze_body",
                    "Freeze is not a very strong slow — it is a hard stop, and it ignores the " +
                    "speed floor that limits slows. Frozen targets also take amplified damage if " +
                    "you have invested in that, which makes a freeze window the moment to land " +
                    "your biggest hits."));

            freeze.Row(LocalizationManager.Get("wiki_freeze_immunity", "Freeze immunity"),
                       WikiFormat.Seconds(BalanceConstants.DefaultFreezeImmunity),
                       LocalizationManager.Get("wiki_freeze_immunity_note",
                           "After a freeze ends, that enemy cannot be refrozen for this long. Without it, a fast turret would freeze-lock a target permanently."));
            freeze.Row(LocalizationManager.Get("wiki_freeze_locked_out", "Freezing an immune enemy"),
                       LocalizationManager.GetFormat("wiki_freeze_locked_out_val",
                           "becomes a {0} slow", WikiFormat.Percent(BalanceConstants.FreezeLockoutSlow)),
                       LocalizationManager.Get("wiki_freeze_locked_out_note",
                           "The application is not wasted — it is downgraded, so Frost keeps contributing during the lockout."));
            freeze.Row(LocalizationManager.Get("wiki_freeze_stacking", "Refreezing early"),
                       LocalizationManager.Get("wiki_freeze_stacking_val", "does not extend"),
                       LocalizationManager.Get("wiki_freeze_stacking_note",
                           "A freeze is only applied to a target that is not already frozen. More freeze sources means better coverage, not longer freezes."));
            sections.Add(freeze);

            var shock = new WikiSection(
                LocalizationManager.Get("wiki_shock_heading", "Shock — a brief stop, rolled per hit"),
                LocalizationManager.Get("wiki_shock_body",
                    "Shock is the other hard stop. It is far shorter than a freeze but has no " +
                    "immunity window, so it is rolled fresh on every single hit. Its value scales " +
                    "with how often you shoot, not with how hard you hit."));
            shock.Row(LocalizationManager.Get("wiki_shock_duration", "Duration"),
                      WikiFormat.Seconds(BalanceConstants.ShockDuration));
            shock.Row(LocalizationManager.Get("wiki_shock_roll", "Rolled"),
                      LocalizationManager.Get("wiki_shock_roll_val", "once per hit"),
                      LocalizationManager.Get("wiki_shock_roll_note",
                          "Fast turrets get far more shock uptime than slow ones at the same chance."));
            sections.Add(shock);

            if (mods != null)
            {
                var live = new WikiSection(WikiFormat.YourBuildLabel);
                float slowMult = mods.GetSlowStrengthMultiplier();
                live.Row(LocalizationManager.Get("wiki_slow_strength_mult", "Slow strength multiplier"),
                         WikiFormat.Multiplier(slowMult));
                live.Row(LocalizationManager.Get("wiki_slow_duration_mult", "Slow duration multiplier"),
                         WikiFormat.Multiplier(mods.GetSlowDurationMultiplier()));

                // Worked example against the player's actual bonuses.
                const float exampleBase = 0.25f;
                float effective = TurretStatMath.EffectiveSlowStrength(exampleBase, slowMult);
                live.Row(LocalizationManager.GetFormat("wiki_slow_example",
                             "A {0} base slow becomes", WikiFormat.Percent(exampleBase)),
                         WikiFormat.Percent(effective),
                         LocalizationManager.GetFormat("wiki_slow_example_note",
                             "Enemy moves at {0} of normal speed.",
                             WikiFormat.Percent(TurretStatMath.SlowSpeedMultiplier(effective))));

                live.Row(LocalizationManager.Get("wiki_freeze_amp_mult", "Damage vs. frozen targets"),
                         WikiFormat.Multiplier(mods.GetFreezeAmplifierMultiplier()));
                live.Row(LocalizationManager.Get("wiki_shock_chance", "Shock chance per hit"),
                         WikiFormat.Percent(mods.GetShockChance(), 1));
                sections.Add(live);
            }

            return sections;
        }

        private static int Mathf_RoundToIntSafe(float v)
            => v <= 0f ? 0 : UnityEngine.Mathf.RoundToInt(v);
    }

    // =====================================================================
    // BURN
    // =====================================================================

    public sealed class BurnWikiPage : IWikiPage, IWikiListEntry
    {
        public string Id => "burn";
        public string Title => LocalizationManager.Get("wiki_page_burn", "Burn & Stacking");

        public string Category => WikiCategories.StatusEffects;
        public UnityEngine.Sprite Icon => null;
        public bool IsLocked => false;

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();

            sections.Add(new WikiSection(
                LocalizationManager.Get("wiki_burn_intro_heading", "Burn is a stacking damage-over-time"),
                LocalizationManager.Get("wiki_burn_intro_body",
                    "Every hit that applies burn adds its OWN independent stack, each with its " +
                    "own damage and timer. Total burn damage is the SUM of all active stacks — " +
                    "it is not capped to a single application.")));

            var numbers = new WikiSection(LocalizationManager.Get("wiki_burn_numbers_heading", "Per stack"));
            numbers.Row(LocalizationManager.Get("wiki_burn_per_stack", "One stack deals"),
                        WikiFormat.Percent(BalanceConstants.BurnHitPercent),
                        LocalizationManager.Get("wiki_burn_per_stack_note",
                            "Of the hit that applied it, spread over the burn duration. Crits apply stronger burns."));
            numbers.Row(LocalizationManager.Get("wiki_burn_cap", "Maximum stacks"),
                        BalanceConstants.DefaultMaxBurnStacks.ToString(),
                        LocalizationManager.Get("wiki_burn_cap_note",
                            "The Ember Stacker and Wildfire evolutions raise this."));
            sections.Add(numbers);

            var rules = new WikiSection(LocalizationManager.Get("wiki_burn_rules_heading", "Stacking rules"));
            rules.Row(LocalizationManager.Get("wiki_burn_rule_blast", "Area hits"),
                      LocalizationManager.Get("wiki_burn_rule_blast_val", "one stack per enemy"),
                      LocalizationManager.Get("wiki_burn_rule_blast_note",
                          "A blast applies burn to each enemy it catches individually, not once for the whole blast."));
            rules.Row(LocalizationManager.Get("wiki_burn_rule_sources", "Different towers"),
                      LocalizationManager.Get("wiki_burn_rule_sources_val", "separate stacks"),
                      LocalizationManager.Get("wiki_burn_rule_sources_note",
                          "Stacks from different towers and different levels coexist. Nothing is overwritten by source."));
            rules.Row(LocalizationManager.Get("wiki_burn_rule_atcap", "When at maximum stacks"),
                      LocalizationManager.Get("wiki_burn_rule_atcap_val", "strongest wins"),
                      LocalizationManager.Get("wiki_burn_rule_atcap_note",
                          "A stronger hit replaces the weakest stack. A weaker hit only refreshes that stack's timer, adding no damage."));
            rules.Row(LocalizationManager.Get("wiki_burn_rule_spread", "Spread on death"),
                      WikiFormat.Percent(BalanceConstants.BurnSpreadPower),
                      LocalizationManager.Get("wiki_burn_rule_spread_note",
                          "Spread burns carry this share of the original damage and duration, and compete for a stack slot like any other application."));
            sections.Add(rules);

            if (mods != null)
            {
                var live = new WikiSection(WikiFormat.YourBuildLabel);
                live.Row(LocalizationManager.Get("wiki_burn_damage_mult", "Burn damage multiplier"),
                         WikiFormat.Multiplier(mods.GetBurnDamageMultiplier()));
                live.Row(LocalizationManager.Get("wiki_burn_duration_mult", "Burn duration multiplier"),
                         WikiFormat.Multiplier(mods.GetBurnDurationMultiplier()));

                float hitFraction = BalanceConstants.BurnHitPercent + mods.GetBurnFromHitBonus();
                live.Row(LocalizationManager.Get("wiki_burn_hit_fraction", "Burn from each hit"),
                         WikiFormat.Percent(hitFraction, 1),
                         LocalizationManager.Get("wiki_burn_hit_fraction_note",
                             "Includes Catalytic Burn cards."));
                live.Row(LocalizationManager.Get("wiki_burn_spread_chance", "Burn spread chance"),
                         WikiFormat.Percent(mods.GetBurnSpreadChance(), 1));
                sections.Add(live);
            }

            return sections;
        }
    }

    // =====================================================================
    // CHAIN
    // =====================================================================

    public sealed class ChainWikiPage : IWikiPage, IWikiListEntry
    {
        public string Id => "chain";
        public string Title => LocalizationManager.Get("wiki_page_chain", "Chain Lightning");

        public string Category => WikiCategories.StatusEffects;
        public UnityEngine.Sprite Icon => null;
        public bool IsLocked => false;

        private readonly GameDatabase _database;

        public ChainWikiPage(GameDatabase database)
        {
            _database = database;
        }

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();

            float falloff = FindChainFalloff();

            sections.Add(new WikiSection(
                LocalizationManager.Get("wiki_chain_intro_heading", "Each hop is weaker than the last"),
                LocalizationManager.GetFormat("wiki_chain_intro_body",
                    "A chain keeps {0} of its damage on every hop, compounding. The falloff is " +
                    "fixed — it does NOT improve with turret level or evolution. Levels raise the " +
                    "starting damage; evolutions and bonuses raise the number of hops.",
                    WikiFormat.Percent(falloff))));

            // Concrete hop table — the "what is the exact %" question.
            var table = new WikiSection(
                LocalizationManager.Get("wiki_chain_table_heading", "Damage by hop"),
                LocalizationManager.Get("wiki_chain_table_body",
                    "As a share of the primary target's hit."));
            for (int hop = 0; hop <= 4; hop++)
            {
                string label = hop == 0
                    ? LocalizationManager.Get("wiki_chain_primary", "Primary target")
                    : LocalizationManager.GetFormat("wiki_chain_hop", "Hop {0}", hop);
                table.Row(label, WikiFormat.Percent(TurretStatMath.ChainDamageAtHop(1f, falloff, hop), 1));
            }
            sections.Add(table);

            var inherit = new WikiSection(
                LocalizationManager.Get("wiki_chain_inherit_heading", "Chain damage uses your damage bonuses"),
                LocalizationManager.Get("wiki_chain_inherit_body",
                    "Chain hits are not a separate flat calculation. They start from the turret's " +
                    "fully-modified damage, and every hop re-checks conditional bonuses and can " +
                    "critically hit on its own."));
            sections.Add(inherit);

            var bounce = new WikiSection(
                LocalizationManager.Get("wiki_chain_bounce_heading", "Bounce-back (Chain Overload)"),
                LocalizationManager.Get("wiki_chain_bounce_body",
                    "Yes — the chain CAN return to the target it started on. Each secondary hop " +
                    "rolls independently, so more hops means more chances to bounce back."));
            bounce.Row(LocalizationManager.Get("wiki_chain_bounce_damage", "Bounce-back damage"),
                       WikiFormat.Percent(BalanceConstants.ChainBounceBackDamageFraction),
                       LocalizationManager.Get("wiki_chain_bounce_damage_note",
                           "Of the hop that triggered it — not of the original hit."));
            sections.Add(bounce);

            if (mods != null)
            {
                var live = new WikiSection(WikiFormat.YourBuildLabel);
                live.Row(LocalizationManager.Get("wiki_chain_damage_mult", "Chain damage multiplier"),
                         WikiFormat.Multiplier(mods.GetChainDamageMultiplier()));
                live.Row(LocalizationManager.Get("wiki_chain_bonus_targets", "Bonus chain targets"),
                         "+" + mods.GetBonusChainTargets());
                live.Row(LocalizationManager.Get("wiki_chain_range_mult", "Chain range multiplier"),
                         WikiFormat.Multiplier(mods.GetChainRangeMultiplier()));
                live.Row(LocalizationManager.Get("wiki_chain_bounce_chance", "Bounce-back chance"),
                         WikiFormat.Percent(mods.GetChainBounceBackChance(), 1),
                         LocalizationManager.Get("wiki_chain_bounce_chance_note", "Rolled per hop."));
                sections.Add(live);
            }

            return sections;
        }

        /// <summary>
        /// Read the falloff off the real chain turret rather than hardcoding it, so
        /// retuning the asset retunes this page.
        /// </summary>
        private float FindChainFalloff()
        {
            if (_database?.Turrets == null) return 0.7f;

            for (int i = 0; i < _database.Turrets.Length; i++)
            {
                var t = _database.Turrets[i];
                if (t != null && t.Type == TurretType.Lightning && t.ChainDamageFalloff > 0f)
                    return t.ChainDamageFalloff;
            }
            return 0.7f;
        }
    }
}
