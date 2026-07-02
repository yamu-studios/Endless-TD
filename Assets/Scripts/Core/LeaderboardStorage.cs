using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ETD.Core
{
    //[Serializable]
    //public class LeaderboardEntry
    //{
    //    public int SurvivedWave;
    //    public float RunDurationSeconds;
    //    public int EarnedCrystals;
    //    public string DateIso;

    //    public DateTime Date
    //    {
    //        get
    //        {
    //            if (DateTime.TryParse(DateIso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var date))
    //                return date;

    //            return DateTime.Now;
    //        }
    //    }
    //}

    [Serializable]
    internal class LeaderboardSaveData
    {
        public List<LeaderboardEntry> Entries = new();
    }

    public static class LeaderboardStorage
    {
        private const string PlayerPrefsKey = "ETD_LOCAL_LEADERBOARD";
        private const int MaxEntries = 10;

        public static IReadOnlyList<LeaderboardEntry> GetTopEntries()
        {
            LeaderboardSaveData data = Load();

           // SortAndTrim(data);

            return data.Entries;
        }

        //public static void AddRun(int survivedWave, float runDurationSeconds = 0f, int earnedCrystals = 0)
        //{
        //    if (survivedWave <= 0)
        //        return;

        //    LeaderboardSaveData data = Load();

        //    data.Entries.Add(new LeaderboardEntry
        //    {
        //        SurvivedWave = survivedWave,
        //        RunDurationSeconds = Mathf.Max(0f, runDurationSeconds),
        //        EarnedCrystals = Mathf.Max(0, earnedCrystals),
        //        DateIso = DateTime.Now.ToString("O")
        //    });

        //    SortAndTrim(data);
        //    Save(data);
        //}

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        private static LeaderboardSaveData Load()
        {
            string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);

            if (string.IsNullOrWhiteSpace(json))
                return new LeaderboardSaveData();

            try
            {
                LeaderboardSaveData data = JsonUtility.FromJson<LeaderboardSaveData>(json);
                return data ?? new LeaderboardSaveData();
            }
            catch
            {
                return new LeaderboardSaveData();
            }
        }

        private static void Save(LeaderboardSaveData data)
        {
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
        }

        //private static void SortAndTrim(LeaderboardSaveData data)
        //{
        //    data.Entries = data.Entries
        //        .Where(e => e != null && e.SurvivedWave > 0)
        //        .OrderByDescending(e => e.SurvivedWave)
        //        .ThenByDescending(e => e.RunDurationSeconds)
        //        .ThenByDescending(e => e.Date)
        //        .Take(MaxEntries)
        //        .ToList();
        //}
    }
}