using ETD.Data;
using UnityEngine;

namespace ETD.Gameplay
{
    public static class ScoreCalculator
    {
        /// <summary>
        /// Endless Defense leaderboard score is intentionally the wave reached.
        ///
        /// Steam leaderboards sort by the integer score value, so using the wave number
        /// directly makes the leaderboard ranking match the player-facing goal:
        /// survive to the highest wave.
        /// </summary>
        public static int CalculateFinalScore(RunData data)
        {
            if (data == null)
                return 0;

            return Mathf.Max(0, data.CurrentWave);
        }
    }
}
