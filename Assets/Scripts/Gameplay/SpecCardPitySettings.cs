using System;
using UnityEngine;

namespace ETD.Gameplay
{
    [Serializable]
    public class SpecCardRarityWeights
    {
        public float Common = 60f;
        public float Uncommon = 25f;
        public float Rare = 10f;
        public float Unique = 4f;
        public float Legendary = 1f;
    }

    [CreateAssetMenu(menuName = "ETD/Spec Cards/Pity Settings")]
    public class SpecCardPitySettings : ScriptableObject
    {
        [Header("Base Rarity Weights")]
        public SpecCardRarityWeights BaseWeights = new();

        [Header("Offer")]
        public int CardsPerOffer = 3;
        public bool PreventDuplicateCards = true;

        [Header("Rare+ Pity")]
        public int RarePlusSoftStart = 4;
        public int RarePlusHardPity = 6;
        public int RarePlusMinWave = 0;
        public float RareBonusPerMiss = 2f;
        public float UniqueBonusFromRarePity = 0.25f;
        public float LegendaryBonusFromRarePity = 0.05f;

        [Header("Unique+ Pity")]
        public int UniquePlusSoftStart = 10;
        public int UniquePlusHardPity = 15;
        public int UniquePlusMinWave = 30;
        public float UniqueBonusPerMiss = 0.75f;
        public float LegendaryBonusFromUniquePity = 0.15f;

        [Header("Legendary Pity")]
        public int LegendarySoftStart = 20;
        public int LegendaryHardPity = 30;
        public int LegendaryMinWave = 70;
        public float LegendaryBonusPerMiss = 0.25f;

        [Header("Early Game Control")]
        [Range(0f, 1f)]
        public float UniqueWeightBeforeMinWaveMultiplier = 0.5f;

        public bool BlockLegendaryBeforeMinWave = true;

        [Header("Debug")]
        public bool LogOffers = false;
    }
}