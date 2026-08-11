// ============================================================================
// ETD.UI.Wiki - WikiCatalog.cs  [NEW]
// Assembles the wiki's page list. The window prefab asks for pages and renders
// whatever it gets, so adding a page never touches UI code.
// ============================================================================
using System.Collections.Generic;
using ETD.Core;
using ETD.Data;

namespace ETD.UI.Wiki
{
    public static class WikiCatalog
    {
        /// <summary>
        /// Turrets first, then mechanics. The order is deliberate and load-bearing: the
        /// wiki opens on the first page in this list, and leading with the full turret
        /// roster — locked entries included — is how players discover that turrets they
        /// have never seen exist and what unlocks them. Mechanics answer questions a
        /// player already knows they have, so they can sit below.
        ///
        /// Rebuild whenever the language changes — page titles are localized.
        /// </summary>
        public static List<IWikiPage> BuildPages(GameDatabase database)
        {
            var pages = new List<IWikiPage>();

            if (database?.Turrets != null)
            {
                for (int i = 0; i < database.Turrets.Length; i++)
                {
                    var turret = database.Turrets[i];
                    if (turret != null)
                        pages.Add(new TurretWikiPage(turret));
                }
            }

            // FUNDAMENTALS, ordered by how early a player runs into the system rather
            // than by complexity: mitigation explains the first "why is this turret doing
            // nothing" moment, targeting the first "why is it shooting THAT" moment.
            pages.Add(new DamageMitigationWikiPage());
            pages.Add(new StatStackingWikiPage());
            pages.Add(new TargetingWikiPage());
            pages.Add(new EconomyWikiPage(database));

            // STATUS EFFECTS. Grouping is by consecutive runs in this list, so these must
            // stay together — a Fundamentals page inserted below would open a second
            // FUNDAMENTALS heading rather than joining the first.
            pages.Add(new SlowWikiPage());
            pages.Add(new BurnWikiPage());
            pages.Add(new ChainWikiPage(database));
            pages.Add(new ArmorDebuffsWikiPage(database));
            pages.Add(new PoisonDecayWikiPage(database));

            // RUN SYSTEMS. Last because these answer questions a player only has once
            // they are optimising a run, not while learning one.
            pages.Add(new WavesWikiPage(database));
            pages.Add(new EnemyTypesWikiPage(database));
            pages.Add(new SpecCardsWikiPage(database));
            pages.Add(new DynamicTilesWikiPage(database));

            return pages;
        }

        /// <summary>
        /// Category heading a page is filed under in the list. Every page shipped today
        /// declares one; the default only catches a future page that forgets to, and files
        /// it somewhere visible rather than dropping it into whatever group precedes it.
        /// </summary>
        public static string CategoryOf(IWikiPage page)
        {
            return page is IWikiListEntry entry && !string.IsNullOrWhiteSpace(entry.Category)
                ? entry.Category
                : LocalizationManager.Get("wiki_category_mechanics", "MECHANICS");
        }

        /// <summary>
        /// Live modifiers when a run is active, otherwise null so pages fall back to
        /// base values. The Hub has no RunManager, which is exactly why the formulas
        /// were extracted into ETD.Data.TurretStatMath.
        /// </summary>
        public static IRunStatModifiers TryGetLiveModifiers()
        {
            return ServiceLocator.TryGet<IRunStatModifiers>(out var mods) ? mods : null;
        }
    }
}
