// ============================================================================
// ETD.UI.Wiki - WikiRunSystemPages.cs  [NEW]
// The RUN SYSTEMS tier of the Phase 7 content pass: the systems a player only
// starts planning around once they are pushing for a personal best — how fast
// the wave curve outruns them, what each enemy type actually does, how card
// offers are rolled, and what the tiles are worth.
//
// Everything is computed from GameDatabase assets / BalanceConstants /
// GameConstants, so a rebalance moves these pages with it.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;

namespace ETD.UI.Wiki
{
    // =====================================================================
    // WAVES & SCALING
    // =====================================================================

    public sealed class WavesWikiPage : IWikiPage, IWikiListEntry
    {
        /// <summary>Waves sampled in the growth table. Chosen to show the curve bending.</summary>
        private static readonly int[] SampleWaves = { 10, 25, 50, 100 };

        private readonly GameDatabase _database;

        public WavesWikiPage(GameDatabase database)
        {
            _database = database;
        }

        public string Id => "waves";
        public string Title => LocalizationManager.Get("wiki_page_waves", "Waves & Scaling");

        public string Category => WikiCategories.RunSystems;
        public Sprite Icon => null;
        public bool IsLocked => false;

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();

            sections.Add(new WikiSection(
                LocalizationManager.Get("wiki_waves_rule_heading", "Enemy health accelerates — your damage does not"),
                LocalizationManager.Get("wiki_waves_rule_body",
                    "Health does not grow at a fixed rate. The rate itself climbs, from gentle " +
                    "early growth to a much steeper late curve, so every run is designed to end. " +
                    "The question is never whether the curve catches you, only how far you get " +
                    "first.")));

            var reference = FindReferenceEnemy();
            if (reference != null)
            {
                var growth = new WikiSection(
                    LocalizationManager.Get("wiki_waves_growth_heading", "Health growth per wave"),
                    LocalizationManager.Get("wiki_waves_growth_body",
                        "How much each wave multiplies enemy health, at that point in the run."));

                for (int i = 0; i < SampleWaves.Length; i++)
                {
                    int wave = SampleWaves[i];
                    double rate = reference.GetHealthGrowthForWave(wave);
                    growth.Row(LocalizationManager.GetFormat("wiki_wave_n", "Wave {0}", wave),
                               WikiFormat.PercentSigned((float)(rate - 1.0), 1));
                }
                growth.Row(LocalizationManager.Get("wiki_waves_growth_cap", "Maximum growth rate"),
                           WikiFormat.PercentSigned(
                               (float)(reference.HealthGrowthBase - 1.0 + reference.HealthGrowthRampCap), 1),
                           LocalizationManager.Get("wiki_waves_growth_cap_note",
                               "The rate stops climbing here. It never stops compounding."));
                sections.Add(growth);

                AddSpeedSection(sections);
            }

            var composition = new WikiSection(
                LocalizationManager.Get("wiki_waves_size_heading", "Wave size"),
                LocalizationManager.Get("wiki_waves_size_body",
                    "Each wave spends a budget on enemies. The budget grows both linearly and " +
                    "quadratically, so later waves are not just tougher one at a time — there " +
                    "are far more of them, arriving faster."));
            composition.Row(LocalizationManager.Get("wiki_waves_budget_base", "Starting budget"),
                            BalanceConstants.WaveBaseBudget.ToString());
            composition.Row(LocalizationManager.Get("wiki_waves_budget_linear", "Budget per wave"),
                            "+" + BalanceConstants.WaveBudgetPerWave);
            composition.Row(LocalizationManager.Get("wiki_waves_budget_quad", "Plus, per wave squared"),
                            "+" + WikiFormat.Number(BalanceConstants.WaveBudgetQuadraticScale, 2));
            composition.Row(LocalizationManager.Get("wiki_waves_spawn_interval", "Spawn interval"),
                            WikiFormat.Seconds(BalanceConstants.WaveBaseSpawnInterval),
                            LocalizationManager.GetFormat("wiki_waves_spawn_interval_note",
                                "Shrinks by {0} each wave, down to a floor of {1}.",
                                WikiFormat.Seconds(BalanceConstants.WaveSpawnIntervalReduction),
                                WikiFormat.Seconds(BalanceConstants.WaveMinSpawnInterval)));
            sections.Add(composition);

            var tiers = new WikiSection(
                LocalizationManager.Get("wiki_waves_tiers_heading", "Elites and bosses"),
                LocalizationManager.Get("wiki_waves_tiers_body",
                    "Elite and boss waves are on fixed schedules, so they are the one part of the " +
                    "run you can prepare for exactly. A boss wave never also spawns elites."));
            tiers.Row(LocalizationManager.Get("wiki_waves_elite_every", "Elite wave"),
                      LocalizationManager.GetFormat("wiki_waves_every_n", "every {0} waves",
                          GameConstants.ELITE_WAVE_INTERVAL));
            tiers.Row(LocalizationManager.Get("wiki_waves_boss_every", "Boss wave"),
                      LocalizationManager.GetFormat("wiki_waves_every_n", "every {0} waves",
                          GameConstants.BOSS_WAVE_INTERVAL),
                      LocalizationManager.Get("wiki_waves_boss_note",
                          "The boss always spawns last in its wave."));
            tiers.Row(LocalizationManager.Get("wiki_waves_elite_count", "Elites per elite wave"),
                      LocalizationManager.GetFormat("wiki_waves_elite_count_val",
                          "1, plus 1 every {0} waves", BalanceConstants.EliteCountWaveStep),
                      LocalizationManager.GetFormat("wiki_waves_elite_count_note",
                          "Doubled from wave {0} onward.", GameConstants.INCREASED_ELITE_FREQUENCY_WAVE));
            tiers.Row(LocalizationManager.Get("wiki_waves_elite_hp", "Elite health"),
                      WikiFormat.Multiplier(GameConstants.ELITE_HP_MULTIPLIER));
            tiers.Row(LocalizationManager.Get("wiki_waves_boss_hp", "Boss health"),
                      WikiFormat.Multiplier(GameConstants.BOSS_HP_MULTIPLIER),
                      LocalizationManager.Get("wiki_waves_tier_hp_note",
                          "On top of the wave scaling above. Gold rewards use the same multipliers."));
            sections.Add(tiers);

            return sections;
        }

        /// <summary>
        /// Speed scaling is per-enemy and most of the roster does not use it at all — only
        /// a handful of assets carry a non-1 SpeedScalePerWave. Quoting one enemy's figure
        /// as though it applied to everything would be the exact kind of lie this wiki
        /// exists to avoid, so the section reports what the roster actually does.
        /// </summary>
        private void AddSpeedSection(List<WikiSection> sections)
        {
            if (_database?.Enemies == null) return;

            int scaling = 0, total = 0;
            float maxRate = 0f;

            for (int i = 0; i < _database.Enemies.Length; i++)
            {
                var e = _database.Enemies[i];
                if (e == null) continue;
                total++;
                float rate = e.SpeedScalePerWave - 1f;
                if (rate <= 0.00001f) continue;
                scaling++;
                if (rate > maxRate) maxRate = rate;
            }

            if (total == 0) return;

            var section = new WikiSection(
                LocalizationManager.Get("wiki_waves_speed_heading", "Movement speed barely moves"),
                scaling == 0
                    ? LocalizationManager.Get("wiki_waves_speed_none",
                        "Enemies do not get faster as the waves go on — only tougher and more " +
                        "numerous. A slow bought at wave 10 is worth exactly as much at wave 100.")
                    : LocalizationManager.GetFormat("wiki_waves_speed_some",
                        "Most enemies do not speed up at all as the waves go on. A few — bosses " +
                        "mainly — gain up to {0} per wave, which is nothing next to how fast " +
                        "health climbs. Slows keep their value for the whole run.",
                        WikiFormat.PercentSigned(maxRate, 1)));

            sections.Add(section);
        }

        /// <summary>
        /// A normal-tier enemy to read the growth curve off. Every enemy carries its own
        /// curve, but they share the same tuning, so the first normal enemy is
        /// representative — and quoting a real asset beats hardcoding the shape here.
        /// </summary>
        private EnemyData FindReferenceEnemy()
        {
            if (_database?.Enemies == null) return null;
            for (int i = 0; i < _database.Enemies.Length; i++)
            {
                var e = _database.Enemies[i];
                if (e != null && !e.IsEliteOnly && !e.IsBossOnly)
                    return e;
            }
            return null;
        }
    }

    // =====================================================================
    // ENEMY TYPES
    // =====================================================================

    public sealed class EnemyTypesWikiPage : IWikiPage, IWikiListEntry
    {
        private readonly GameDatabase _database;

        public EnemyTypesWikiPage(GameDatabase database)
        {
            _database = database;
        }

        public string Id => "enemies";
        public string Title => LocalizationManager.Get("wiki_page_enemies", "Enemy Types");

        public string Category => WikiCategories.RunSystems;
        public Sprite Icon => null;
        public bool IsLocked => false;

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();

            sections.Add(new WikiSection(
                LocalizationManager.Get("wiki_enemies_rule_heading", "Each type breaks one assumption"),
                LocalizationManager.Get("wiki_enemies_rule_body",
                    "Most enemies just walk the path and die. The types below each invalidate " +
                    "something a defence normally relies on — the route, your line of sight, or " +
                    "the idea that damage dealt stays dealt. A build that handles all of them is " +
                    "what carries a run past the first boss.")));

            var behaviours = new WikiSection(
                LocalizationManager.Get("wiki_enemies_table_heading", "What each type does"));

            AddTypeRow(behaviours, EnemyType.Tank,
                LocalizationManager.Get("wiki_enemy_tank", "Tank"),
                LocalizationManager.Get("wiki_enemy_tank_val", "high health and armor"),
                LocalizationManager.Get("wiki_enemy_tank_note",
                    "The armor is the real problem, not the health. Pierce or pure damage, not more raw damage."));

            AddTypeRow(behaviours, EnemyType.Sprinter,
                LocalizationManager.Get("wiki_enemy_sprinter", "Sprinter"),
                LocalizationManager.Get("wiki_enemy_sprinter_val", "very fast, very fragile"),
                LocalizationManager.Get("wiki_enemy_sprinter_note",
                    "Dies instantly if it is hit at all. The danger is that it crosses your range before anything fires."));

            AddTypeRow(behaviours, EnemyType.Stealth,
                LocalizationManager.Get("wiki_enemy_stealth", "Stealth"),
                LocalizationManager.Get("wiki_enemy_stealth_val", "untargetable until revealed"),
                LocalizationManager.Get("wiki_enemy_stealth_note",
                    "Ignored entirely by turrets that cannot see stealth, however strong they are. A Scanner solves it for everything nearby."));

            AddTypeRow(behaviours, EnemyType.Flying,
                LocalizationManager.Get("wiki_enemy_flying", "Flying"),
                LocalizationManager.Get("wiki_enemy_flying_val", "ignores the path"),
                LocalizationManager.Get("wiki_enemy_flying_note",
                    "Flies straight from entry to exit, so it spends far less time in range. Fragile to compensate."));

            var berserk = FindEnemy(EnemyType.Berserk);
            AddTypeRow(behaviours, EnemyType.Berserk,
                LocalizationManager.Get("wiki_enemy_berserk", "Berserk"),
                berserk != null
                    ? LocalizationManager.GetFormat("wiki_enemy_berserk_val", "speeds up to {0} as it is hurt",
                        WikiFormat.Multiplier(berserk.BerserkMaxSpeedMultiplier))
                    : LocalizationManager.Get("wiki_enemy_berserk_val_plain", "speeds up as it is hurt"),
                LocalizationManager.Get("wiki_enemy_berserk_note",
                    "Chipping it is the worst outcome. Either kill it outright or keep it slowed."));

            var splitter = FindEnemy(EnemyType.Splitter);
            AddTypeRow(behaviours, EnemyType.Splitter,
                LocalizationManager.Get("wiki_enemy_splitter", "Splitter"),
                splitter != null
                    ? LocalizationManager.GetFormat("wiki_enemy_splitter_val", "splits into {0} on death",
                        splitter.SplitCount)
                    : LocalizationManager.Get("wiki_enemy_splitter_val_plain", "splits on death"),
                LocalizationManager.Get("wiki_enemy_splitter_note",
                    "Kill it early on the path, not at your base — the children still have the whole route left to walk."));

            var regen = FindEnemy(EnemyType.Regenerator);
            AddTypeRow(behaviours, EnemyType.Regenerator,
                LocalizationManager.Get("wiki_enemy_regen", "Regenerator"),
                regen != null
                    ? LocalizationManager.GetFormat("wiki_enemy_regen_val", "heals {0} of max health per second",
                        WikiFormat.Percent(regen.RegenPercentPerSecond, 1))
                    : LocalizationManager.Get("wiki_enemy_regen_val_plain", "heals over time"),
                LocalizationManager.Get("wiki_enemy_regen_note",
                    "Beats slow chip damage outright. Burst it down, or out-tick it with burn and poison."));

            var buffer = FindEnemy(EnemyType.Buffer);
            AddTypeRow(behaviours, EnemyType.Buffer,
                LocalizationManager.Get("wiki_enemy_buffer", "Buffer"),
                buffer != null
                    ? LocalizationManager.GetFormat("wiki_enemy_buffer_val", "strengthens nearby enemies by {0}",
                        WikiFormat.Percent(buffer.BuffPercent))
                    : LocalizationManager.Get("wiki_enemy_buffer_val_plain", "strengthens nearby enemies"),
                LocalizationManager.Get("wiki_enemy_buffer_note",
                    "Kill it first and the whole wave gets easier. Target it before the crowd it is buffing."));

            var debuffer = FindEnemy(EnemyType.Debuffer);
            AddTypeRow(behaviours, EnemyType.Debuffer,
                LocalizationManager.Get("wiki_enemy_debuffer", "Debuffer"),
                debuffer != null
                    ? LocalizationManager.GetFormat("wiki_enemy_debuffer_val", "weakens your turrets by {0}",
                        WikiFormat.Percent(debuffer.DebuffPercent))
                    : LocalizationManager.Get("wiki_enemy_debuffer_val_plain", "weakens your turrets"),
                LocalizationManager.Get("wiki_enemy_debuffer_note",
                    "Only affects turrets inside its radius, so it punishes tightly packed defences hardest."));

            sections.Add(behaviours);

            var tiers = new WikiSection(
                LocalizationManager.Get("wiki_enemies_tiers_heading", "Tiers stack on top of type"),
                LocalizationManager.Get("wiki_enemies_tiers_body",
                    "Elite and boss are not separate enemies — they are the same types with a " +
                    "health multiplier. An elite Regenerator still regenerates, and now has five " +
                    "times the health to regenerate. Some enemies only ever appear as elites or " +
                    "bosses and never in a normal wave."));
            sections.Add(tiers);

            return sections;
        }

        /// <summary>
        /// A type is only listed when the database actually ships an enemy of it, so a type
        /// that is removed from the roster stops being documented rather than becoming a
        /// promise the game does not keep.
        /// </summary>
        private void AddTypeRow(WikiSection section, EnemyType type,
                                string label, string value, string note)
        {
            if (FindEnemy(type) == null) return;
            section.Row(label, value, note);
        }

        private EnemyData FindEnemy(EnemyType type)
        {
            if (_database?.Enemies == null) return null;
            for (int i = 0; i < _database.Enemies.Length; i++)
            {
                var e = _database.Enemies[i];
                if (e != null && e.Type == type) return e;
            }
            return null;
        }
    }

    // =====================================================================
    // SPEC CARDS & RARITY
    // =====================================================================

    public sealed class SpecCardsWikiPage : IWikiPage, IWikiListEntry
    {
        private readonly GameDatabase _database;

        public SpecCardsWikiPage(GameDatabase database)
        {
            _database = database;
        }

        public string Id => "cards";
        public string Title => LocalizationManager.Get("wiki_page_cards", "Spec Cards & Rarity");

        public string Category => WikiCategories.RunSystems;
        public Sprite Icon => null;
        public bool IsLocked => false;

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();
            var s = _database != null ? _database.PitySettings : null;

            sections.Add(new WikiSection(
                LocalizationManager.Get("wiki_cards_rule_heading", "Bad luck is compensated, not just survived"),
                LocalizationManager.Get("wiki_cards_rule_body",
                    "Card rarity is weighted, but the weights are not fixed. Every offer that " +
                    "misses a rarity raises your odds for the next one, and past a certain number " +
                    "of misses the game simply gives it to you. A drought is temporary by design.")));

            if (s == null)
            {
                // Better an honest empty page than invented numbers: without the settings
                // asset there is nothing truthful to print.
                sections.Add(new WikiSection(
                    LocalizationManager.Get("wiki_cards_unavailable_heading", "Details unavailable"),
                    LocalizationManager.Get("wiki_cards_unavailable_body",
                        "The card settings could not be read, so the exact numbers are not shown here.")));
                return sections;
            }

            var weights = new WikiSection(
                LocalizationManager.Get("wiki_cards_weights_heading", "Base chances"),
                LocalizationManager.Get("wiki_cards_weights_body",
                    "Before any pity. These are relative weights across the cards you can still be offered."));
            float total = Mathf.Max(0.0001f,
                s.BaseWeights.Common + s.BaseWeights.Uncommon + s.BaseWeights.Rare
                + s.BaseWeights.Unique + s.BaseWeights.Legendary);
            weights.Row(LocalizationManager.Get("rarity_common", "Common"),
                        WikiFormat.Percent(s.BaseWeights.Common / total, 1));
            weights.Row(LocalizationManager.Get("rarity_uncommon", "Uncommon"),
                        WikiFormat.Percent(s.BaseWeights.Uncommon / total, 1));
            weights.Row(LocalizationManager.Get("rarity_rare", "Rare"),
                        WikiFormat.Percent(s.BaseWeights.Rare / total, 1));
            weights.Row(LocalizationManager.Get("rarity_unique", "Unique"),
                        WikiFormat.Percent(s.BaseWeights.Unique / total, 1));
            weights.Row(LocalizationManager.Get("rarity_legendary", "Legendary"),
                        WikiFormat.Percent(s.BaseWeights.Legendary / total, 1));
            sections.Add(weights);

            var pity = new WikiSection(
                LocalizationManager.Get("wiki_cards_pity_heading", "Guaranteed after enough misses"),
                LocalizationManager.Get("wiki_cards_pity_body",
                    "Each rarity has its own counter. Odds start improving at the first number " +
                    "and the rarity is guaranteed at the second."));
            AddPityRow(pity,
                LocalizationManager.Get("wiki_cards_pity_rare", "Rare or better"),
                s.RarePlusSoftStart, s.RarePlusHardPity, s.RarePlusMinWave);
            AddPityRow(pity,
                LocalizationManager.Get("wiki_cards_pity_unique", "Unique or better"),
                s.UniquePlusSoftStart, s.UniquePlusHardPity, s.UniquePlusMinWave);
            AddPityRow(pity,
                LocalizationManager.Get("wiki_cards_pity_legendary", "Legendary"),
                s.LegendarySoftStart, s.LegendaryHardPity, s.LegendaryMinWave);
            sections.Add(pity);

            var offer = new WikiSection(LocalizationManager.Get("wiki_cards_offer_heading", "Each offer"));
            offer.Row(LocalizationManager.Get("wiki_cards_per_offer", "Cards shown"),
                      s.CardsPerOffer.ToString());
            if (s.PreventDuplicateCards)
            {
                offer.Row(LocalizationManager.Get("wiki_cards_duplicates", "Duplicates in one offer"),
                          LocalizationManager.Get("wiki_cards_duplicates_val", "prevented"),
                          LocalizationManager.Get("wiki_cards_duplicates_note",
                              "You will never be shown the same card twice in a single offer."));
            }
            sections.Add(offer);

            var early = new WikiSection(
                LocalizationManager.Get("wiki_cards_early_heading", "The best cards are gated by wave"),
                LocalizationManager.Get("wiki_cards_early_body",
                    "High rarities are held back early so a lucky first level-up cannot decide the " +
                    "whole run. Their odds are suppressed before their minimum wave, and the pity " +
                    "counters keep running in the meantime — so they can land quickly once the gate opens."));
            sections.Add(early);

            return sections;
        }

        private void AddPityRow(WikiSection section, string label, int soft, int hard, int minWave)
        {
            string value = LocalizationManager.GetFormat("wiki_cards_pity_val",
                "improves after {0}, guaranteed at {1}", soft, hard);

            string note = minWave > 0
                ? LocalizationManager.GetFormat("wiki_cards_pity_minwave", "Not offered before wave {0}.", minWave)
                : null;

            section.Row(label, value, note);
        }
    }

    // =====================================================================
    // DYNAMIC TILES
    // =====================================================================

    public sealed class DynamicTilesWikiPage : IWikiPage, IWikiListEntry
    {
        private readonly GameDatabase _database;

        public DynamicTilesWikiPage(GameDatabase database)
        {
            _database = database;
        }

        public string Id => "tiles";
        public string Title => LocalizationManager.Get("wiki_page_tiles", "Dynamic Tiles");

        public string Category => WikiCategories.RunSystems;
        public Sprite Icon => null;
        public bool IsLocked => false;

        public IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods)
        {
            var sections = new List<WikiSection>();

            sections.Add(new WikiSection(
                LocalizationManager.Get("wiki_tiles_rule_heading", "The board is not uniform"),
                LocalizationManager.Get("wiki_tiles_rule_body",
                    "Some tiles change whatever is built on them. A tile's effect is a separate " +
                    "multiplier from your global bonuses, so it multiplies against everything else " +
                    "rather than being diluted by it — which is why matching the right turret to " +
                    "the right tile is worth going out of your way for.")));

            var chance = new WikiSection(LocalizationManager.Get("wiki_tiles_chance_heading", "How common they are"));
            chance.Row(LocalizationManager.Get("wiki_tiles_share", "Share of buildable tiles"),
                       WikiFormat.Percent(GameConstants.SPECIALTY_TILE_RATIO),
                       LocalizationManager.Get("wiki_tiles_share_note",
                           "Rolled when the map is generated, so a board's special tiles are fixed for the whole run."));
            sections.Add(chance);

            AddCategory(sections, TileSpecialty.Blessed,
                LocalizationManager.Get("wiki_tiles_blessed_heading", "Blessings — upside only"),
                LocalizationManager.Get("wiki_tiles_blessed_body",
                    "A straight bonus with no cost. Put your strongest turret on one."));

            AddCategory(sections, TileSpecialty.Cursed,
                LocalizationManager.Get("wiki_tiles_cursed_heading", "Curses — a penalty you are paid for"),
                LocalizationManager.Get("wiki_tiles_cursed_body",
                    "Most curses cripple one stat and compensate with another, and those are not " +
                    "traps: a turret that does not care about the penalty gets the compensation " +
                    "for free — a range penalty costs a short-range turret almost nothing. A few " +
                    "curses are pure downside, with nothing listed after the slash. Build on those " +
                    "only when you have run out of better ground."));

            AddCategory(sections, TileSpecialty.Greed,
                LocalizationManager.Get("wiki_tiles_greed_heading", "Greeds — power against gold"),
                LocalizationManager.Get("wiki_tiles_greed_body",
                    "Greed tiles trade upgrade cost for power in one direction or the other. " +
                    "Whether they are good depends entirely on how much gold you expect to sink " +
                    "into that turret for the rest of the run."));

            return sections;
        }

        /// <summary>
        /// One section per category, listing the tiles the database actually ships with
        /// their real primary effect and tradeoff, rather than a hand-written summary that
        /// would go stale the moment a tile is retuned.
        /// </summary>
        private void AddCategory(List<WikiSection> sections, TileSpecialty category,
                                 string heading, string body)
        {
            if (_database == null) return;

            var tiles = _database.GetTilesByCategory(category);
            if (tiles == null || tiles.Length == 0) return;

            var section = new WikiSection(heading, body);

            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null) continue;

                string name = SOLocalization.GetName(tile.LocalizationKey, tile.DisplayName);
                string effect = DescribeModifier(tile.PrimaryEffect);
                string tradeoff = DescribeModifier(tile.Tradeoff);

                string value = string.IsNullOrEmpty(tradeoff)
                    ? effect
                    : effect + "   /   " + tradeoff;

                section.Row(string.IsNullOrWhiteSpace(name) ? tile.Id : name, value);
            }

            sections.Add(section);
        }

        /// <summary>"+30% damage" / "-50% range". Empty when the tile has no tradeoff.</summary>
        private static string DescribeModifier(TurretStatModifier mod)
        {
            if (mod.Stat == TurretStatModifier.StatType.None || Mathf.Approximately(mod.Value, 0f))
                return "";

            return WikiFormat.PercentSigned(mod.Value) + " " + StatName(mod.Stat);
        }

        private static string StatName(TurretStatModifier.StatType stat) => stat switch
        {
            TurretStatModifier.StatType.Damage =>
                LocalizationManager.Get("damage", "Damage"),
            TurretStatModifier.StatType.AttackSpeed =>
                LocalizationManager.Get("attack_speed", "Attack Speed"),
            TurretStatModifier.StatType.Range =>
                LocalizationManager.Get("range", "Range"),
            TurretStatModifier.StatType.UpgradeCost =>
                LocalizationManager.Get("wiki_tiles_stat_cost", "upgrade cost"),
            TurretStatModifier.StatType.GoldFromKills =>
                LocalizationManager.Get("wiki_tiles_stat_gold", "gold from kills"),
            TurretStatModifier.StatType.ArmorPierce =>
                LocalizationManager.Get("wiki_mit_armor_pierce", "Armor Pierce"),
            _ => LocalizationManager.Get("wiki_tiles_stat_other", "effect")
        };
    }
}
