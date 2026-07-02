using System;
using System.Collections.Generic;

namespace ETD.Core
{
    public sealed class LocalLeaderboardService : ILeaderboardService
    {
        public bool IsAvailable => true;

        public void SubmitScore(LeaderboardResult result)
        {
            SaveSystem.UpdateLeaderboard(result.Wave > 0 ? result.Wave : result.Score);
        }

        public void RequestHubLeaderboard(Action<IReadOnlyList<LeaderboardEntry>> onComplete)
        {
            var save = SaveSystem.Load();
            var entries = new List<LeaderboardEntry>();

            int[] localWaves = save.TopWaves != null && save.TopWaves.Length > 0
                ? save.TopWaves
                : save.TopScores;

            // Local fallback is only a fallback. It should show the saved local top 10
            // and must not duplicate the current player again on row 10.
            if (localWaves != null)
            {
                for (int i = 0; i < localWaves.Length && entries.Count < 10; i++)
                {
                    int score = localWaves[i];
                    if (score <= 0)
                        continue;

                    entries.Add(new LeaderboardEntry
                    {
                        Rank = entries.Count + 1,
                        PlayerName = entries.Count == 0
                            ? LocalizationManager.Get("leaderboard_you", "YOU")
                            : LocalizationManager.Get("leaderboard_local", "Local"),
                        Score = score,
                        Wave = score,
                        SurvivedWave = score,
                        IsLocalPlayer = entries.Count == 0
                    });
                }
            }

            onComplete?.Invoke(entries);
        }
    }
}
