// ============================================================================
// ETD.UI.Wiki - WikiStatusPages.cs  [NEW]
// The STATUS EFFECTS pages added in the Phase 7b content pass: the v1.0
// Void / Railgun / Scanner debuff layer and the Toxin / decay damage-over-time
// layer, neither of which had any wiki coverage at all.
//
// Values come from the turret assets rather than being retyped here, so
// retuning Tower_Void.asset retunes the page. Where an effect has no asset
// (Entropy Engine decay), the number comes from BalanceConstants.
//
// Deliberately NOT documented: StatusEffectType.ArmorBreak. The Basic turret
// applies it and it drives VFX and challenge tracking, but nothing in
// EnemyController.ApplyMitigation ever reads it, so it currently reduces no
// armor. Documenting an inert mechanic would be worse than omitting it — see
// the tracked follow-up to wire it up, then add its row here.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;

namespace ETD.UI.Wiki
{
    // =====================================================================
    // ARMOR DEBUFFS
    // =====================================================================

    public sealed class ArmorDebuffsWikiPage : IWikiPage, IWikiListEntry
    {
        private readonly GameDatabase _database;

        public ArmorDebuffsWikiPage(GameDatabase database)
        {
            _database = database;
        }

        public string Id => "debuffs";
        public string Title => LocalizationManager.Get("wiki_page_debuffs", "Armor Debuffs");

        public string Category => WikiCategories.StatusEffects;
        public Sprite Icon => null;
        public bool IsLocked => false;

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();

            sections.Add(new WikiSection(
                LocalizationManager.Get("wiki_debuff_rule_heading", "Two ways to make a target take more"),
                LocalizationManager.Get("wiki_debuff_rule_body",
                    "You can strip what an enemy has, or amplify what you land. Weaken strips " +
                    "armor. Expose amplifies the damage that survived armor. They apply at " +
                    "different points in the damage formula, so running both is not redundant — " +
                    "it is the strongest thing you can do to a single tough target.")));

            var weaken = new WikiSection(
                LocalizationManager.Get("wiki_debuff_weaken_heading", "Weaken — strips armor"),
                LocalizationManager.Get("wiki_debuff_weaken_body",
                    "The Void turret's signature. It subtracts from the target's effective armor " +
                    "for its duration, so every turret shooting that target benefits, not just " +
                    "the Void turret that applied it."));
            // Unsigned: the label already says "removed", so "+25%" would read as though
            // the debuff were adding armor.
            AddTurretStatusRows(weaken, TurretType.Void,
                t => t.WeakenPercent, t => t.WeakenDuration,
                LocalizationManager.Get("wiki_debuff_armor_removed", "Armor removed"),
                signed: false);
            weaken.Row(LocalizationManager.Get("wiki_debuff_weaken_scaling", "Grows with turret level"),
                       WikiFormat.PerLevel(BalanceConstants.IdentityScalingPerLevel),
                       LocalizationManager.GetFormat("wiki_debuff_weaken_scaling_note",
                           "Strength and duration both scale, up to {0}.",
                           WikiFormat.PercentSigned(BalanceConstants.IdentityScalingCap)));
            sections.Add(weaken);

            var expose = new WikiSection(
                LocalizationManager.Get("wiki_debuff_expose_heading", "Expose — amplifies damage taken"),
                LocalizationManager.Get("wiki_debuff_expose_body",
                    "The Railgun's signature. It multiplies ALL mitigated damage the target takes, " +
                    "applied as the very last step — after resistance and armor. It does not affect " +
                    "pure damage, which already bypasses mitigation entirely."));
            AddTurretStatusRows(expose, TurretType.Railgun,
                t => t.ExposePercent, t => t.ExposeDuration,
                LocalizationManager.Get("wiki_debuff_damage_amp", "Damage taken"));
            sections.Add(expose);

            var mark = new WikiSection(
                LocalizationManager.Get("wiki_debuff_mark_heading", "The Scanner's mark shares Expose's slot"),
                LocalizationManager.Get("wiki_debuff_mark_body",
                    "Fire Control Array marks every enemy in reveal range, and that mark is ADDED " +
                    "to Expose rather than multiplying with it. A Railgun and a Scanner covering " +
                    "the same corner amplify strongly, not explosively — that is deliberate. " +
                    "Marks from two Scanners do not stack either: the strongest wins."));
            AddMarkRows(mark);
            sections.Add(mark);

            var order = new WikiSection(
                LocalizationManager.Get("wiki_debuff_order_heading", "Where each one lands"),
                LocalizationManager.Get("wiki_debuff_order_body",
                    "Armor reduction and damage amplification are different steps. See Damage & " +
                    "Mitigation for the full formula."));
            order.Row(LocalizationManager.Get("wiki_debuff_order_weaken", "Weaken, Armor Pierce, armor shred"),
                      LocalizationManager.Get("wiki_debuff_order_weaken_val", "cut armor"),
                      LocalizationManager.Get("wiki_debuff_order_weaken_note",
                          "Worth most against heavily armored targets, and nothing at all against unarmored ones."));
            order.Row(LocalizationManager.Get("wiki_debuff_order_expose", "Expose and Scanner mark"),
                      LocalizationManager.Get("wiki_debuff_order_expose_val", "amplify last"),
                      LocalizationManager.Get("wiki_debuff_order_expose_note",
                          "Worth the same percentage against every target, armored or not."));
            sections.Add(order);

            if (mods != null)
            {
                var live = new WikiSection(WikiFormat.YourBuildLabel);
                live.Row(LocalizationManager.Get("wiki_mit_live_armor_pierce", "Armor pierce"),
                         WikiFormat.Percent(mods.GetArmorPierce(), 1));
                live.Row(LocalizationManager.Get("wiki_mit_live_affinity_pierce", "Affinity pierce"),
                         WikiFormat.Percent(mods.GetAffinityPierce(), 1));
                sections.Add(live);
            }

            return sections;
        }

        /// <summary>
        /// Strength and duration read off the turret that owns the status, so a
        /// rebalance of the asset moves the page with it.
        /// </summary>
        private void AddTurretStatusRows(WikiSection section, TurretType type,
                                         System.Func<TurretData, float> percent,
                                         System.Func<TurretData, float> duration,
                                         string percentLabel, bool signed = true)
        {
            var turret = FindTurret(type);
            if (turret == null) return;

            section.Row(percentLabel, signed
                ? WikiFormat.PercentSigned(percent(turret))
                : WikiFormat.Percent(percent(turret)));
            section.Row(LocalizationManager.Get("wiki_debuff_duration", "Duration"),
                        WikiFormat.Seconds(duration(turret)),
                        LocalizationManager.Get("wiki_debuff_duration_note",
                            "Refreshed by every new application — it does not stack up, it stays on."));
        }

        private void AddMarkRows(WikiSection section)
        {
            var radar = FindTurret(TurretType.Radar);
            var pathA = radar != null ? radar.PathA : null;
            if (pathA == null || !pathA.MarkEnemies) return;

            section.Row(LocalizationManager.Get("wiki_debuff_mark_amp", "Damage taken at level 1"),
                        WikiFormat.PercentSigned(pathA.MarkDamageAmp),
                        LocalizationManager.Get("wiki_debuff_mark_amp_note",
                            "Grows per Scanner level. The per-level figure is on the Scanner's own page."));

            var tier2 = radar.Tier2;
            if (tier2 != null && tier2.MarkArmorShredBonus > 0f)
            {
                section.Row(LocalizationManager.Get("wiki_debuff_mark_shred", "Tier 2: armor stripped"),
                            WikiFormat.Percent(tier2.MarkArmorShredBonus),
                            LocalizationManager.Get("wiki_debuff_mark_shred_note",
                                "A proportional cut applied before flat pierce, so the Scanner's Tier 2 does both jobs at once."));
            }
        }

        private TurretData FindTurret(TurretType type)
        {
            if (_database?.Turrets == null) return null;
            for (int i = 0; i < _database.Turrets.Length; i++)
            {
                var t = _database.Turrets[i];
                if (t != null && t.Type == type) return t;
            }
            return null;
        }
    }

    // =====================================================================
    // POISON & DECAY
    // =====================================================================

    public sealed class PoisonDecayWikiPage : IWikiPage, IWikiListEntry
    {
        private readonly GameDatabase _database;

        public PoisonDecayWikiPage(GameDatabase database)
        {
            _database = database;
        }

        public string Id => "poison";
        public string Title => LocalizationManager.Get("wiki_page_poison", "Poison & Decay");

        public string Category => WikiCategories.StatusEffects;
        public Sprite Icon => null;
        public bool IsLocked => false;

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();

            sections.Add(new WikiSection(
                LocalizationManager.Get("wiki_poison_rule_heading", "Damage measured in percent, not points"),
                LocalizationManager.Get("wiki_poison_rule_body",
                    "Most damage is a flat number that armor then reduces. These effects take a " +
                    "SHARE of the target's current health instead, so they are worth exactly as " +
                    "much against a wave-100 boss as against a wave-10 grunt. That is what keeps " +
                    "them relevant when enemy health has run away from your turrets.")));

            var toxin = new WikiSection(
                LocalizationManager.Get("wiki_poison_toxin_heading", "Toxin — pure percent damage"),
                LocalizationManager.Get("wiki_poison_toxin_body",
                    "Every Toxin hit deals bonus damage equal to a share of the target's CURRENT " +
                    "health, as PURE damage: it ignores armor, ignores resistance, ignores the " +
                    "minimum-damage floor, and gains nothing from Expose. It is the most " +
                    "predictable damage in the game and the answer to an enemy you cannot dent."));

            var toxinTurret = FindTurret(TurretType.Toxin);
            if (toxinTurret != null)
            {
                toxin.Row(LocalizationManager.Get("wiki_poison_per_hit", "Current HP removed per hit"),
                          WikiFormat.Percent(toxinTurret.ToxinPurePercent, 1));

                if (toxinTurret.Tier2 != null && toxinTurret.Tier2.ToxinPurePercentBonus > 0f)
                {
                    toxin.Row(LocalizationManager.Get("wiki_poison_tier2", "Tier 2 adds"),
                              WikiFormat.PercentSigned(toxinTurret.Tier2.ToxinPurePercentBonus, 1),
                              LocalizationManager.Get("wiki_poison_tier2_note",
                                  "Per hit, on top of the base rate."));
                }
            }

            toxin.Row(LocalizationManager.Get("wiki_poison_scaling", "Grows with turret level"),
                      WikiFormat.PerLevel(BalanceConstants.IdentityScalingPerLevel),
                      LocalizationManager.GetFormat("wiki_poison_scaling_note",
                          "Up to {0}. Attack speed matters more than raw damage here — the effect is per HIT, not per point of damage.",
                          WikiFormat.PercentSigned(BalanceConstants.IdentityScalingCap)));
            sections.Add(toxin);

            var covenant = new WikiSection(
                LocalizationManager.Get("wiki_poison_covenant_heading", "Plague Covenant spreads it"),
                LocalizationManager.Get("wiki_poison_covenant_body",
                    "With the covenant active, turrets that are NOT Toxin also apply a fraction of " +
                    "Toxin's percent damage on their hits, using Toxin's base rate since they have " +
                    "none of their own. A wide board of fast turrets gets far more out of it than " +
                    "a few slow heavy hitters."));
            sections.Add(covenant);

            var mitigated = new WikiSection(
                LocalizationManager.Get("wiki_poison_mitigated_heading", "Not all percent damage is pure"),
                LocalizationManager.Get("wiki_poison_mitigated_body",
                    "Some evolutions add current-HP damage that is folded into the normal hit and " +
                    "therefore still goes through armor and resistance. It scales the same way but " +
                    "is not immune to mitigation the way Toxin's is — against a heavily armored " +
                    "target the two behave very differently."));
            sections.Add(mitigated);

            var decay = new WikiSection(
                LocalizationManager.Get("wiki_poison_decay_heading", "Entropy Engine — decay that cannot kill"),
                LocalizationManager.Get("wiki_poison_decay_body",
                    "Decay removes a share of every enemy's current health each second, everywhere " +
                    "on the board at once, with no turret involved. It is strictly non-lethal: it " +
                    "stops at a floor and your turrets still have to finish the job. Treat it as " +
                    "softening, never as a win condition."));
            decay.Row(LocalizationManager.Get("wiki_poison_decay_floor", "Decay floor"),
                      WikiFormat.Percent(BalanceConstants.EntropyMinHpFraction, 1),
                      LocalizationManager.Get("wiki_poison_decay_floor_note",
                          "Of maximum health. Decay never takes an enemy below this, and never lands the killing blow."));
            decay.Row(LocalizationManager.Get("wiki_poison_decay_numbers", "Damage numbers"),
                      LocalizationManager.Get("wiki_poison_decay_numbers_val", "not shown"),
                      LocalizationManager.Get("wiki_poison_decay_numbers_note",
                          "Decay ticks every frame on every enemy, so it is silent by design. It still counts toward percent-HP damage totals."));
            sections.Add(decay);

            if (mods != null)
            {
                var live = new WikiSection(WikiFormat.YourBuildLabel);

                float leak = mods.GetPlagueLeakFraction();
                live.Row(LocalizationManager.Get("wiki_mit_live_plague", "Plague Covenant leak"),
                         leak > 0f ? WikiFormat.Percent(leak, 1)
                                   : LocalizationManager.Get("wiki_not_active", "Not active"));

                float decayRate = mods.GetMaxHPDecayPerSecond();
                live.Row(LocalizationManager.Get("wiki_poison_live_decay", "Current HP decay per second"),
                         decayRate > 0f ? WikiFormat.Percent(decayRate, 2)
                                        : LocalizationManager.Get("wiki_not_active", "Not active"));
                sections.Add(live);
            }

            return sections;
        }

        private TurretData FindTurret(TurretType type)
        {
            if (_database?.Turrets == null) return null;
            for (int i = 0; i < _database.Turrets.Length; i++)
            {
                var t = _database.Turrets[i];
                if (t != null && t.Type == type) return t;
            }
            return null;
        }
    }
}
