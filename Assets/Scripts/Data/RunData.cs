// ============================================================================
// ETD.Data - RunData.cs  [UPDATED - uses new SpecCardEffectType]
// ============================================================================
using System.Collections.Generic;

namespace ETD.Data
{
    public class RunData
    {
        // Player
        public int Lives;
        public int MaxLives;
        public int Gold;
        public int Score;
        public int Level = 1;
        public float CurrentXP;
        public float XPToNextLevel;

        // Waves
        public int CurrentWave;
        public float TotalTime;
        public float PrepTimeRemaining;

        // Selections from planning
        public List<string> ActiveTraitIds = new();
        public float[] ActivePermanentBonuses = new float[10];
        public int ActiveRerollTokens;

        // In-run acquired
        public Dictionary<SpecCardEffectType, float> SpecBonuses = new();
        public Dictionary<SpecCardEffectType, int> SpecStacks = new();

        // Meta
        public int MetaCurrencyEarned;
        public int EnemiesKilled;
        public int TurretsPlaced;
        public int TurretsEvolved;
        public int BossesDefeated;
        public int TotalGoldEarned;
        public int TotalGoldSpent;
        public float TotalBurnDamageDealt;
        public int TotalCriticalHits;
        public int TotalChainHits;
        public int TotalProjectilesFired;
        public int TotalFrozenEnemies;
        public int TotalSlowedSeconds; // cumulative
        public int TotalBurningEnemies;
        public float TotalSniperDamage;
        public float TotalPercentHPDamage;

        public int SpecCardsChosen = 0;

        public float GetXPRequired(int level)
        {
            return Core.GameConstants.BASE_XP_REQUIRED
                 * UnityEngine.Mathf.Pow(Core.GameConstants.XP_SCALING_FACTOR, level - 1);
        }

        public float GetSpecBonus(SpecCardEffectType type)
        {
            return SpecBonuses.TryGetValue(type, out float val) ? val : 0f;
        }

        public void AddSpecBonus(SpecCardEffectType type, float value)
        {
            if (SpecBonuses.ContainsKey(type))
                SpecBonuses[type] += value;
            else
                SpecBonuses[type] = value;

            if (SpecStacks.ContainsKey(type))
                SpecStacks[type]++;
            else
                SpecStacks[type] = 1;
        }
    }
}
