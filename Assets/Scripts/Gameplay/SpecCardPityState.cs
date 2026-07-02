using System.Collections.Generic;
using ETD.Data;

namespace ETD.Gameplay
{
    public class SpecCardPityState
    {
        public int OffersSinceRarePlus { get; private set; }
        public int OffersSinceUniquePlus { get; private set; }
        public int OffersSinceLegendary { get; private set; }

        public void Reset()
        {
            OffersSinceRarePlus = 0;
            OffersSinceUniquePlus = 0;
            OffersSinceLegendary = 0;
        }

        public void RegisterOffer(IReadOnlyList<SpecCardData> offeredCards)
        {
            bool offeredRarePlus = false;
            bool offeredUniquePlus = false;
            bool offeredLegendary = false;

            for (int i = 0; i < offeredCards.Count; i++)
            {
                SpecCardData card = offeredCards[i];
                if (card == null) continue;

                if (card.Rarity >= SpecCardRarity.Rare)
                    offeredRarePlus = true;

                if (card.Rarity >= SpecCardRarity.Unique)
                    offeredUniquePlus = true;

                if (card.Rarity == SpecCardRarity.Legendary)
                    offeredLegendary = true;
            }

            OffersSinceRarePlus = offeredRarePlus ? 0 : OffersSinceRarePlus + 1;
            OffersSinceUniquePlus = offeredUniquePlus ? 0 : OffersSinceUniquePlus + 1;
            OffersSinceLegendary = offeredLegendary ? 0 : OffersSinceLegendary + 1;
        }
    }
}