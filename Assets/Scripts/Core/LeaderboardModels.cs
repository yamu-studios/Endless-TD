using System;
using System.Collections.Generic;
using UnityEngine;

namespace ETD.Core
{
    [Serializable]
    public struct LeaderboardResult
    {
        // Current leaderboard score. This is the wave reached.
        public int Score;
        public int Wave;
        public int DurationSeconds;
        public int EnemiesKilled;
        public int BossesDefeated;
        public int TotalGoldEarned;
        public int EarnedCrystals;
        public int TurretsEvolved;
        public int LivesRemaining;
    }

    [Serializable]
    public class LeaderboardEntry
    {
        public int Rank;
        public string PlayerName;
        // Current leaderboard score. This is the wave reached.
        public int Score;
        public int SurvivedWave;

        // Steam user id as raw ulong.
        // Core stays independent from Steamworks.NET because it does not use CSteamID.
        public ulong UserId;

        // Steam avatar converted by SteamLeaderboardService.
        // UI can display this with a RawImage.
        public Texture2D AvatarTexture;

        public bool IsLocalPlayer;

        // Optional details. Keep these for future expanded leaderboard UI.
        public int Wave;
        public int DurationSeconds;
        public int EnemiesKilled;
        public int BossesDefeated;
        public int TotalGoldEarned;
        public int EarnedCrystals;
        public int TurretsEvolved;
        public int LivesRemaining;
    }

    public interface ILeaderboardService
    {
        bool IsAvailable { get; }

        void SubmitScore(LeaderboardResult result);

        // Returns:
        // rows 1-9 = global top 9
        // row 10   = global #10 when the local player is already in top 9,
        //            otherwise the local player's real global rank
        void RequestHubLeaderboard(
            Action<IReadOnlyList<LeaderboardEntry>> onComplete
        );
    }
}
