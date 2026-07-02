using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ETD.Data;

namespace ETD.Gameplay
{
    public class SpecCardOfferGenerator
    {
        private readonly SpecCardPitySettings _settings;
        private readonly SpecCardPityState _pityState;

        private readonly List<SpecCardData> _offer = new();
        private readonly List<SpecCardData> _candidates = new();

        public SpecCardOfferGenerator(SpecCardPitySettings settings, SpecCardPityState pityState)
        {
            _settings = settings;
            _pityState = pityState;
        }

        public List<SpecCardData> GenerateOffer(
            List<SpecCardData> availableCards,
            int currentWave,
            float gradeBonus,
            System.Random rng)
        {
            _offer.Clear();

            if (_settings == null)
            {
                Debug.LogError("[SpecCardOfferGenerator] Missing SpecCardPitySettings.");
                return new List<SpecCardData>();
            }

            if (availableCards == null || availableCards.Count == 0)
                return new List<SpecCardData>();

            rng ??= new System.Random();

            int cardCount = Mathf.Max(1, _settings.CardsPerOffer);

            for (int i = 0; i < cardCount; i++)
            {
                SpecCardData card = RollCard(availableCards, currentWave, gradeBonus, rng);

                if (card != null)
                    _offer.Add(card);
            }

            ApplyHardPityIfNeeded(availableCards, currentWave, gradeBonus, rng);

            _pityState.RegisterOffer(_offer);

            if (_settings.LogOffers)
                LogOffer(currentWave);

            return new List<SpecCardData>(_offer);
        }

        private SpecCardData RollCard(
            List<SpecCardData> availableCards,
            int currentWave,
            float gradeBonus,
            System.Random rng)
        {
            float totalWeight = 0f;

            for (int i = 0; i < availableCards.Count; i++)
            {
                SpecCardData card = availableCards[i];

                if (card == null)
                    continue;

                if (_settings.PreventDuplicateCards && _offer.Contains(card))
                    continue;

                totalWeight += GetWeight(card.Rarity, currentWave, gradeBonus);
            }

            if (totalWeight <= 0f)
                return RollAnyAvailableCard(availableCards, rng);

            float roll = (float)(rng.NextDouble() * totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < availableCards.Count; i++)
            {
                SpecCardData card = availableCards[i];

                if (card == null)
                    continue;

                if (_settings.PreventDuplicateCards && _offer.Contains(card))
                    continue;

                cumulative += GetWeight(card.Rarity, currentWave, gradeBonus);

                if (roll <= cumulative)
                    return card;
            }

            return RollAnyAvailableCard(availableCards, rng);
        }

        private float GetWeight(SpecCardRarity rarity, int currentWave, float gradeBonus)
        {
            float weight = rarity switch
            {
                SpecCardRarity.Common => _settings.BaseWeights.Common,
                SpecCardRarity.Uncommon => _settings.BaseWeights.Uncommon,
                SpecCardRarity.Rare => _settings.BaseWeights.Rare,
                SpecCardRarity.Unique => _settings.BaseWeights.Unique,
                SpecCardRarity.Legendary => _settings.BaseWeights.Legendary,
                _ => _settings.BaseWeights.Common
            };

            // Your Lucky Charm / grade bonus logic.
            if (gradeBonus > 0f && rarity >= SpecCardRarity.Rare)
                weight *= 1f + gradeBonus;

            // Early game control.
            if (rarity == SpecCardRarity.Unique &&
                currentWave < _settings.UniquePlusMinWave)
            {
                weight *= _settings.UniqueWeightBeforeMinWaveMultiplier;
            }

            if (rarity == SpecCardRarity.Legendary &&
                _settings.BlockLegendaryBeforeMinWave &&
                currentWave < _settings.LegendaryMinWave)
            {
                return 0f;
            }

            // Rare+ soft pity.
            if (currentWave >= _settings.RarePlusMinWave &&
                _pityState.OffersSinceRarePlus >= _settings.RarePlusSoftStart)
            {
                int misses = _pityState.OffersSinceRarePlus - _settings.RarePlusSoftStart + 1;

                if (rarity == SpecCardRarity.Rare)
                    weight += misses * _settings.RareBonusPerMiss;

                if (rarity == SpecCardRarity.Unique)
                    weight += misses * _settings.UniqueBonusFromRarePity;

                if (rarity == SpecCardRarity.Legendary &&
                    currentWave >= _settings.LegendaryMinWave)
                    weight += misses * _settings.LegendaryBonusFromRarePity;
            }

            // Unique+ soft pity.
            if (currentWave >= _settings.UniquePlusMinWave &&
                _pityState.OffersSinceUniquePlus >= _settings.UniquePlusSoftStart)
            {
                int misses = _pityState.OffersSinceUniquePlus - _settings.UniquePlusSoftStart + 1;

                if (rarity == SpecCardRarity.Unique)
                    weight += misses * _settings.UniqueBonusPerMiss;

                if (rarity == SpecCardRarity.Legendary &&
                    currentWave >= _settings.LegendaryMinWave)
                    weight += misses * _settings.LegendaryBonusFromUniquePity;
            }

            // Legendary soft pity.
            if (currentWave >= _settings.LegendaryMinWave &&
                _pityState.OffersSinceLegendary >= _settings.LegendarySoftStart)
            {
                int misses = _pityState.OffersSinceLegendary - _settings.LegendarySoftStart + 1;

                if (rarity == SpecCardRarity.Legendary)
                    weight += misses * _settings.LegendaryBonusPerMiss;
            }

            return Mathf.Max(0f, weight);
        }

        private void ApplyHardPityIfNeeded(
            List<SpecCardData> availableCards,
            int currentWave,
            float gradeBonus,
            System.Random rng)
        {
            // Priority order:
            // Legendary > Unique+ > Rare+

            if (currentWave >= _settings.LegendaryMinWave &&
                _pityState.OffersSinceLegendary >= _settings.LegendaryHardPity &&
                !OfferContains(SpecCardRarity.Legendary))
            {
                ForceCard(availableCards, SpecCardRarity.Legendary, currentWave, rng);
                return;
            }

            if (currentWave >= _settings.UniquePlusMinWave &&
                _pityState.OffersSinceUniquePlus >= _settings.UniquePlusHardPity &&
                !OfferContainsUniquePlus())
            {
                ForceCard(availableCards, SpecCardRarity.Unique, currentWave, rng);
                return;
            }

            if (currentWave >= _settings.RarePlusMinWave &&
                _pityState.OffersSinceRarePlus >= _settings.RarePlusHardPity &&
                !OfferContainsRarePlus())
            {
                ForceCard(availableCards, SpecCardRarity.Rare, currentWave, rng);
            }
        }

        private void ForceCard(
            List<SpecCardData> availableCards,
            SpecCardRarity minimumRarity,
            int currentWave,
            System.Random rng)
        {
            _candidates.Clear();

            for (int i = 0; i < availableCards.Count; i++)
            {
                SpecCardData card = availableCards[i];

                if (card == null)
                    continue;

                if (_settings.PreventDuplicateCards && _offer.Contains(card))
                    continue;

                if (card.Rarity < minimumRarity)
                    continue;

                if (!IsRarityAllowedForHardPity(card.Rarity, currentWave))
                    continue;

                _candidates.Add(card);
            }

            if (_candidates.Count == 0)
                return;

            SpecCardData forcedCard = _candidates[rng.Next(_candidates.Count)];
            ReplaceLowestRarityCard(forcedCard);
        }

        private bool IsRarityAllowedForHardPity(SpecCardRarity rarity, int currentWave)
        {
            if (rarity == SpecCardRarity.Legendary &&
                _settings.BlockLegendaryBeforeMinWave &&
                currentWave < _settings.LegendaryMinWave)
            {
                return false;
            }

            return true;
        }

        private void ReplaceLowestRarityCard(SpecCardData forcedCard)
        {
            if (forcedCard == null)
                return;

            if (_offer.Count == 0)
            {
                _offer.Add(forcedCard);
                return;
            }

            int lowestIndex = 0;
            SpecCardRarity lowestRarity = _offer[0] != null
                ? _offer[0].Rarity
                : SpecCardRarity.Legendary;

            for (int i = 1; i < _offer.Count; i++)
            {
                if (_offer[i] == null)
                {
                    lowestIndex = i;
                    lowestRarity = SpecCardRarity.Common;
                    continue;
                }

                if (_offer[i].Rarity < lowestRarity)
                {
                    lowestRarity = _offer[i].Rarity;
                    lowestIndex = i;
                }
            }

            _offer[lowestIndex] = forcedCard;
        }

        private SpecCardData RollAnyAvailableCard(
            List<SpecCardData> availableCards,
            System.Random rng)
        {
            _candidates.Clear();

            for (int i = 0; i < availableCards.Count; i++)
            {
                SpecCardData card = availableCards[i];

                if (card == null)
                    continue;

                if (_settings.PreventDuplicateCards && _offer.Contains(card))
                    continue;

                _candidates.Add(card);
            }

            if (_candidates.Count == 0)
                return null;

            return _candidates[rng.Next(_candidates.Count)];
        }

        private bool OfferContainsRarePlus()
        {
            for (int i = 0; i < _offer.Count; i++)
            {
                if (_offer[i] != null && _offer[i].Rarity >= SpecCardRarity.Rare)
                    return true;
            }

            return false;
        }

        private bool OfferContainsUniquePlus()
        {
            for (int i = 0; i < _offer.Count; i++)
            {
                if (_offer[i] != null && _offer[i].Rarity >= SpecCardRarity.Unique)
                    return true;
            }

            return false;
        }

        private bool OfferContains(SpecCardRarity rarity)
        {
            for (int i = 0; i < _offer.Count; i++)
            {
                if (_offer[i] != null && _offer[i].Rarity == rarity)
                    return true;
            }

            return false;
        }

        private void LogOffer(int currentWave)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append($"[SpecCardPity] Wave {currentWave} | ");
            sb.Append($"Rare+ Miss: {_pityState.OffersSinceRarePlus}, ");
            sb.Append($"Unique+ Miss: {_pityState.OffersSinceUniquePlus}, ");
            sb.Append($"Legendary Miss: {_pityState.OffersSinceLegendary} | ");

            for (int i = 0; i < _offer.Count; i++)
            {
                if (_offer[i] == null)
                    continue;

                sb.Append(_offer[i].name);
                sb.Append(" (");
                sb.Append(_offer[i].Rarity);
                sb.Append(")");

                if (i < _offer.Count - 1)
                    sb.Append(", ");
            }

            Debug.Log(sb.ToString());
        }
    }
}