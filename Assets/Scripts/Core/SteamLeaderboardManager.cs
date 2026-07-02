using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if STEAMWORKS_NET
using Steamworks;
#endif

namespace ETD.Core
{
    public class SteamLeaderboardEntry
    {
        public int Rank;
        public string PlayerName;
        public int Score;
        public bool IsLocalPlayer;
        public Texture2D Avatar;
        public bool AvatarLoaded;
    }

    public class SteamLeaderboardManager : MonoBehaviour
    {
        public static SteamLeaderboardManager Instance { get; private set; }

        [Header("Leaderboard Settings")]
        [SerializeField] private string _leaderboardName = "waves_reached_v1";
        [SerializeField] private int _topEntriesToFetch = 9;

        // Cached results
        public List<SteamLeaderboardEntry> TopEntries { get; private set; } = new();
        public SteamLeaderboardEntry PlayerEntry { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsInitialized { get; private set; }

        public event Action OnLeaderboardUpdated;

#if STEAMWORKS_NET
        private SteamLeaderboard_t _leaderboard;
        private bool _leaderboardFound;

        private CallResult<LeaderboardFindResult_t> _findResult;
        private CallResult<LeaderboardScoreUploaded_t> _uploadResult;
        private CallResult<LeaderboardScoresDownloaded_t> _downloadGlobalResult;
        private CallResult<LeaderboardScoresDownloaded_t> _downloadPlayerResult;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Legacy wrapper only. Score submission now goes through ILeaderboardService in RunManager.

#if STEAMWORKS_NET
            if (!SteamManager.Initialized) return;

            _findResult = CallResult<LeaderboardFindResult_t>.Create(OnLeaderboardFound);
            _uploadResult = CallResult<LeaderboardScoreUploaded_t>.Create(OnScoreUploaded);
            _downloadGlobalResult = CallResult<LeaderboardScoresDownloaded_t>.Create(OnGlobalDownloaded);
            _downloadPlayerResult = CallResult<LeaderboardScoresDownloaded_t>.Create(OnPlayerDownloaded);

            var handle = SteamUserStats.FindOrCreateLeaderboard(
                _leaderboardName,
                ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
                ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric);
            _findResult.Set(handle);
#endif
        }

        // =================================================================
        // GAME OVER  SUBMIT SCORE
        // =================================================================

        private void OnGameOver(GameOverEvent evt)
        {
#if STEAMWORKS_NET
            if (!_leaderboardFound || !SteamManager.Initialized) return;

            var handle = SteamUserStats.UploadLeaderboardScore(
                _leaderboard,
                ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
                evt.Score,
                null, 0);
            _uploadResult.Set(handle);
#endif
        }

        // =================================================================
        // FETCH  called by Hub when leaderboard panel opens
        // =================================================================

        public void FetchLeaderboard()
        {
#if STEAMWORKS_NET
            if (!_leaderboardFound || !SteamManager.Initialized || IsLoading) return;
            IsLoading = true;
            TopEntries.Clear();
            PlayerEntry = null;

            // Fetch top N globally (0-indexed: 0 to _topEntriesToFetch-1)
            var handle = SteamUserStats.DownloadLeaderboardEntries(
                _leaderboard,
                ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal,
                1, _topEntriesToFetch);
            _downloadGlobalResult.Set(handle);
#else
            IsLoading = false;
            IsInitialized = true;
            OnLeaderboardUpdated?.Invoke();
#endif
        }

        // =================================================================
        // CALLBACKS
        // =================================================================

#if STEAMWORKS_NET
        private void OnLeaderboardFound(LeaderboardFindResult_t result, bool failure)
        {
            if (failure || result.m_bLeaderboardFound == 0)
            {
                Debug.LogWarning("[SteamLeaderboard] Leaderboard not found: " + _leaderboardName);
                return;
            }
            _leaderboard = result.m_hSteamLeaderboard;
            _leaderboardFound = true;
            IsInitialized = true;
            FetchLeaderboard();
        }

        private void OnScoreUploaded(LeaderboardScoreUploaded_t result, bool failure)
        {
            if (failure) { Debug.LogWarning("[SteamLeaderboard] Score upload failed."); return; }
            Debug.Log($"[SteamLeaderboard] Score uploaded. New rank: {result.m_nGlobalRankNew}");
            FetchLeaderboard(); // Refresh after upload
        }

        private void OnGlobalDownloaded(LeaderboardScoresDownloaded_t result, bool failure)
        {
            if (failure) { IsLoading = false; OnLeaderboardUpdated?.Invoke(); return; }

            CSteamID localId = SteamUser.GetSteamID();
            bool playerInTop = false;

            for (int i = 0; i < result.m_cEntryCount; i++)
            {
                SteamUserStats.GetDownloadedLeaderboardEntry(
                    result.m_hSteamLeaderboardEntries, i,
                    out LeaderboardEntry_t entry, null, 0);

                var le = new SteamLeaderboardEntry
                {
                    Rank = entry.m_nGlobalRank,
                    Score = entry.m_nScore,
                    PlayerName = SteamFriends.GetFriendPersonaName(entry.m_steamIDUser),
                    IsLocalPlayer = entry.m_steamIDUser == localId
                };

                if (le.IsLocalPlayer) playerInTop = true;
                TopEntries.Add(le);
                StartCoroutine(LoadAvatar(entry.m_steamIDUser, le));
            }

            if (!playerInTop)
            {
                // Fetch player's own rank separately
                var handle = SteamUserStats.DownloadLeaderboardEntries(
                    _leaderboard,
                    ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser,
                    0, 0);
                _downloadPlayerResult.Set(handle);
            }
            else
            {
                // If player IS in top 9, also fetch rank 10
                if (TopEntries.Count >= _topEntriesToFetch)
                {
                    var handle = SteamUserStats.DownloadLeaderboardEntries(
                        _leaderboard,
                        ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal,
                        _topEntriesToFetch + 1, _topEntriesToFetch + 1);
                    _downloadPlayerResult.Set(handle);
                }
                else
                {
                    IsLoading = false;
                    OnLeaderboardUpdated?.Invoke();
                }
            }
        }

        private void OnPlayerDownloaded(LeaderboardScoresDownloaded_t result, bool failure)
        {
            IsLoading = false;

            if (!failure && result.m_cEntryCount > 0)
            {
                SteamUserStats.GetDownloadedLeaderboardEntry(
                    result.m_hSteamLeaderboardEntries, 0,
                    out LeaderboardEntry_t entry, null, 0);

                CSteamID localId = SteamUser.GetSteamID();
                PlayerEntry = new SteamLeaderboardEntry
                {
                    Rank = entry.m_nGlobalRank,
                    Score = entry.m_nScore,
                    PlayerName = SteamFriends.GetFriendPersonaName(entry.m_steamIDUser),
                    IsLocalPlayer = entry.m_steamIDUser == localId
                };
                StartCoroutine(LoadAvatar(entry.m_steamIDUser, PlayerEntry));
            }

            OnLeaderboardUpdated?.Invoke();
        }

        // =================================================================
        // AVATAR LOADING
        // =================================================================

        private IEnumerator LoadAvatar(CSteamID steamId, SteamLeaderboardEntry entry)
        {
            int handle = SteamFriends.GetLargeFriendAvatar(steamId);

            // Handle not ready yet  wait
            int attempts = 0;
            while (handle == -1 && attempts < 10)
            {
                yield return new WaitForSeconds(0.2f);
                handle = SteamFriends.GetLargeFriendAvatar(steamId);
                attempts++;
            }

            if (handle <= 0) { entry.AvatarLoaded = true; yield break; }

            bool ok = SteamUtils.GetImageSize(handle, out uint w, out uint h);
            if (!ok || w == 0 || h == 0) { entry.AvatarLoaded = true; yield break; }

            byte[] imageData = new byte[w * h * 4];
            SteamUtils.GetImageRGBA(handle, imageData, (int)(w * h * 4));

            var tex = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false);

            // Steam returns RGBA top-to-bottom; Unity expects bottom-to-top  flip
            byte[] flipped = new byte[imageData.Length];
            int stride = (int)w * 4;
            for (int row = 0; row < h; row++)
                Array.Copy(imageData, row * stride, flipped, ((int)h - 1 - row) * stride, stride);

            tex.LoadRawTextureData(flipped);
            tex.Apply();

            entry.Avatar = tex;
            entry.AvatarLoaded = true;
            OnLeaderboardUpdated?.Invoke(); // Refresh UI when avatar loads
        }
#endif

        private void OnDestroy()
        {
            // Legacy wrapper only. No direct GameOver subscription.
        }
    }
}