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
        /// Mechanic pages first (the questions players actually ask), then one page
        /// per turret. Rebuild whenever the language changes — page titles are localized.
        /// </summary>
        public static List<IWikiPage> BuildPages(GameDatabase database)
        {
            var pages = new List<IWikiPage>
            {
                new StatStackingWikiPage(),
                new SlowWikiPage(),
                new BurnWikiPage(),
                new ChainWikiPage(database)
            };

            if (database?.Turrets != null)
            {
                for (int i = 0; i < database.Turrets.Length; i++)
                {
                    var turret = database.Turrets[i];
                    if (turret != null)
                        pages.Add(new TurretWikiPage(turret));
                }
            }

            return pages;
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
