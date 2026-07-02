using ETD.Data;

namespace ETD.Gameplay
{
    public static class SpecCardRarityUtility
    {
        public static SpecCardRarity GetRarity(SpecCardData card)
        {
            /*
             IMPORTANT:
             Change this line depending on your actual SpecCardData field name.

             If your SpecCardData has:
                 public Grade Grade;
             use:
                 return ConvertFromGrade(card.Grade);

             If your SpecCardData has:
                 public CardGrade Rarity;
             use:
                 return ConvertFromGrade(card.Rarity);

             If your SpecCardData already uses Common/Uncommon/Rare/Unique/Legendary,
             map it below.
            */

            return ConvertFromGrade(card.Rarity);
        }

        private static SpecCardRarity ConvertFromGrade(object grade)
        {
            string value = grade.ToString();

            switch (value)
            {
                case "Common":
                    return SpecCardRarity.Common;

                case "Uncommon":
                    return SpecCardRarity.Uncommon;

                case "Rare":
                    return SpecCardRarity.Rare;

                case "Unique":
                    return SpecCardRarity.Unique;

                case "Legendary":
                    return SpecCardRarity.Legendary;

                default:
                    return SpecCardRarity.Common;
            }
        }

        public static bool IsRarePlus(SpecCardRarity rarity)
        {
            return rarity >= SpecCardRarity.Rare;
        }

        public static bool IsUniquePlus(SpecCardRarity rarity)
        {
            return rarity >= SpecCardRarity.Unique;
        }

        public static bool IsLegendary(SpecCardRarity rarity)
        {
            return rarity == SpecCardRarity.Legendary;
        }
    }
}