#if !DISABLESTEAMWORKS

using System;
using System.Collections.Generic;
using ETD.Core;
using Steamworks;
using UnityEngine;

namespace ETD.Steam
{
    public sealed class SteamLeaderboardService : MonoBehaviour, ILeaderboardService
    {
        private const string LeaderboardName = "waves_reached_v1";
        private const int DetailCount = 8;
        private const int GlobalRowsToFetch = 10;
        private const int VisibleTopRows = 9;

        private readonly LocalLeaderboardService _localFallback = new LocalLeaderboardService();

        private SteamLeaderboard_t _leaderboard;
        private bool _leaderboardReady;
        private bool _findRequested;

        [SerializeField] private float _serviceRepairInterval = 0.25f;
        private float _nextServiceRepairTime;

        private readonly List<ETD.Core.LeaderboardEntry> _topGlobalEntries = new List<ETD.Core.LeaderboardEntry>();
        private ETD.Core.LeaderboardEntry _playerEntry;
        private Action<IReadOnlyList<ETD.Core.LeaderboardEntry>> _pendingHubCallback;

        private CallResult<LeaderboardFindResult_t> _findResult;
        private CallResult<LeaderboardScoreUploaded_t> _uploadResult;
        private CallResult<LeaderboardScoresDownloaded_t> _topDownloadResult;
        private CallResult<LeaderboardScoresDownloaded_t> _playerDownloadResult;

        public bool IsAvailable
        {
            get
            {
                TryStartFindLeaderboard();
                return _leaderboardReady;
            }
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            _findResult = CallResult<LeaderboardFindResult_t>.Create(OnFindLeaderboard);
            _uploadResult = CallResult<LeaderboardScoreUploaded_t>.Create(OnScoreUploaded);
            _topDownloadResult = CallResult<LeaderboardScoresDownloaded_t>.Create(OnTopScoresDownloaded);
            _playerDownloadResult = CallResult<LeaderboardScoresDownloaded_t>.Create(OnPlayerScoreDownloaded);

            RegisterServiceIfNeeded();
        }

        private void OnEnable()
        {
            RegisterServiceIfNeeded();
        }

        private void Start()
        {
            RegisterServiceIfNeeded();
            TryStartFindLeaderboard();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextServiceRepairTime)
            {
                _nextServiceRepairTime = Time.unscaledTime + Mathf.Max(0.05f, _serviceRepairInterval);
                RegisterServiceIfNeeded();
            }

            TryStartFindLeaderboard();
        }

        private void RegisterServiceIfNeeded()
        {
            if (ServiceLocator.TryGet<ILeaderboardService>(out ILeaderboardService existing))
            {
                if (ReferenceEquals(existing, this))
                    return;

                // If the newer persistent runtime exists, it should win over this legacy service.
                if (existing != null && existing.GetType().Name == "SteamLeaderboardRuntime")
                    return;
            }

            ServiceLocator.Register<ILeaderboardService>(this);
        }

        private bool TryStartFindLeaderboard()
        {
            if (_leaderboardReady || _findRequested)
                return _leaderboardReady;

            if (!SteamAPI.IsSteamRunning())
                return false;

            try
            {
                SteamAPICall_t call = SteamUserStats.FindOrCreateLeaderboard(
                    LeaderboardName,
                    ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
                    ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric
                );

                _findResult.Set(call);
                _findRequested = true;

                Debug.Log("[SteamLeaderboard] Finding leaderboard: " + LeaderboardName);
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SteamLeaderboard] Could not start Steam leaderboard request. " + ex.Message);
                return false;
            }
        }

        private void OnFindLeaderboard(LeaderboardFindResult_t result, bool ioFailure)
        {
            _findRequested = false;

            if (ioFailure || result.m_bLeaderboardFound == 0)
            {
                Debug.LogWarning("[SteamLeaderboard] Failed to find/create leaderboard. Using local fallback.");
                _leaderboardReady = false;
                return;
            }

            _leaderboard = result.m_hSteamLeaderboard;
            _leaderboardReady = true;

            Debug.Log("[SteamLeaderboard] Leaderboard ready: " + LeaderboardName);
            RequestHubLeaderboard(null);
        }

        public void SubmitScore(LeaderboardResult result)
        {
            if (!TryStartFindLeaderboard() && !_leaderboardReady)
            {
                _localFallback.SubmitScore(result);
                Debug.Log("[SteamLeaderboard] Steam not ready. Saved score locally.");
                return;
            }

            if (!_leaderboardReady)
            {
                _localFallback.SubmitScore(result);
                return;
            }

            int leaderboardScore = result.Wave > 0 ? result.Wave : result.Score;

            int[] details =
            {
                leaderboardScore,
                result.DurationSeconds,
                result.EnemiesKilled,
                result.BossesDefeated,
                result.TotalGoldEarned,
                result.EarnedCrystals,
                result.TurretsEvolved,
                result.LivesRemaining
            };

            try
            {
                SteamAPICall_t call = SteamUserStats.UploadLeaderboardScore(
                    _leaderboard,
                    ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
                    leaderboardScore,
                    details,
                    details.Length
                );

                _uploadResult.Set(call);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SteamLeaderboard] Upload failed. Saved locally. " + ex.Message);
                _localFallback.SubmitScore(result);
            }
        }

        private void OnScoreUploaded(LeaderboardScoreUploaded_t result, bool ioFailure)
        {
            if (ioFailure || result.m_bSuccess == 0)
            {
                Debug.LogWarning("[SteamLeaderboard] Score upload failed.");
                return;
            }

            Debug.Log(
                "[SteamLeaderboard] Score uploaded. " +
                "Changed=" + result.m_bScoreChanged +
                ", NewRank=" + result.m_nGlobalRankNew +
                ", OldRank=" + result.m_nGlobalRankPrevious
            );

            RequestHubLeaderboard(null);
        }

        public void RequestHubLeaderboard(Action<IReadOnlyList<ETD.Core.LeaderboardEntry>> onComplete)
        {
            if (onComplete != null)
                _pendingHubCallback = onComplete;

            if (!TryStartFindLeaderboard() && !_leaderboardReady)
            {
                if (onComplete != null)
                    _localFallback.RequestHubLeaderboard(onComplete);
                return;
            }

            if (!_leaderboardReady)
            {
                if (onComplete != null)
                    _localFallback.RequestHubLeaderboard(onComplete);
                return;
            }

            _topGlobalEntries.Clear();
            _playerEntry = null;

            try
            {
                SteamAPICall_t call = SteamUserStats.DownloadLeaderboardEntries(
                    _leaderboard,
                    ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal,
                    1,
                    GlobalRowsToFetch
                );

                _topDownloadResult.Set(call);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SteamLeaderboard] Top 10 download failed. Using local fallback. " + ex.Message);

                if (onComplete != null)
                    _localFallback.RequestHubLeaderboard(onComplete);
            }
        }

        private void OnTopScoresDownloaded(LeaderboardScoresDownloaded_t result, bool ioFailure)
        {
            if (ioFailure)
            {
                CompleteHubRequest();
                return;
            }

            _topGlobalEntries.Clear();

            bool localPlayerInTopNine = false;
            bool localPlayerInTopTen = false;
            ulong localUserId = SteamUser.GetSteamID().m_SteamID;

            for (int i = 0; i < result.m_cEntryCount; i++)
            {
                if (!TryReadEntry(result.m_hSteamLeaderboardEntries, i, out ETD.Core.LeaderboardEntry entry))
                    continue;

                _topGlobalEntries.Add(entry);

                if (entry.UserId == localUserId)
                {
                    if (entry.Rank <= VisibleTopRows)
                        localPlayerInTopNine = true;

                    if (entry.Rank <= GlobalRowsToFetch)
                        localPlayerInTopTen = true;
                }
            }

            if (localPlayerInTopNine || localPlayerInTopTen)
            {
                CompleteHubRequest();
                return;
            }

            try
            {
                SteamAPICall_t call = SteamUserStats.DownloadLeaderboardEntries(
                    _leaderboard,
                    ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser,
                    0,
                    0
                );

                _playerDownloadResult.Set(call);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SteamLeaderboard] Player rank request failed. " + ex.Message);
                CompleteHubRequest();
            }
        }

        private void OnPlayerScoreDownloaded(LeaderboardScoresDownloaded_t result, bool ioFailure)
        {
            if (!ioFailure && result.m_cEntryCount > 0)
            {
                if (TryReadEntry(result.m_hSteamLeaderboardEntries, 0, out ETD.Core.LeaderboardEntry entry))
                {
                    entry.PlayerName = LocalizationManager.Get("leaderboard_you", "YOU");
                    entry.IsLocalPlayer = true;
                    _playerEntry = entry;
                }
            }

            CompleteHubRequest();
        }

        private void CompleteHubRequest()
        {
            List<ETD.Core.LeaderboardEntry> final = new List<ETD.Core.LeaderboardEntry>();

            bool localPlayerInFirstNine = false;
            ulong localUserId = 0;

            try
            {
                localUserId = SteamUser.GetSteamID().m_SteamID;
            }
            catch
            {
                localUserId = 0;
            }

            for (int i = 0; i < _topGlobalEntries.Count && final.Count < VisibleTopRows; i++)
            {
                ETD.Core.LeaderboardEntry entry = _topGlobalEntries[i];
                final.Add(entry);

                if (entry.IsLocalPlayer || (localUserId != 0 && entry.UserId == localUserId))
                    localPlayerInFirstNine = true;
            }

            if (localPlayerInFirstNine)
            {
                if (_topGlobalEntries.Count >= GlobalRowsToFetch)
                    final.Add(_topGlobalEntries[GlobalRowsToFetch - 1]);
            }
            else if (_playerEntry != null && !ContainsUser(final, _playerEntry.UserId))
            {
                final.Add(_playerEntry);
            }
            else if (_topGlobalEntries.Count >= GlobalRowsToFetch)
            {
                final.Add(_topGlobalEntries[GlobalRowsToFetch - 1]);
            }

            Action<IReadOnlyList<ETD.Core.LeaderboardEntry>> callback = _pendingHubCallback;
            _pendingHubCallback = null;

            callback?.Invoke(final);
        }

        private static bool ContainsUser(List<ETD.Core.LeaderboardEntry> entries, ulong userId)
        {
            if (userId == 0)
                return false;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].UserId == userId)
                    return true;
            }

            return false;
        }

        private bool TryReadEntry(
            SteamLeaderboardEntries_t entriesHandle,
            int index,
            out ETD.Core.LeaderboardEntry entry)
        {
            entry = null;

            int[] details = new int[DetailCount];

            bool ok = SteamUserStats.GetDownloadedLeaderboardEntry(
                entriesHandle,
                index,
                out LeaderboardEntry_t steamEntry,
                details,
                details.Length
            );

            if (!ok)
                return false;

            CSteamID localUser = SteamUser.GetSteamID();
            bool isLocal = steamEntry.m_steamIDUser == localUser;

            string playerName = isLocal
                ? LocalizationManager.Get("leaderboard_you", "YOU")
                : SteamFriends.GetFriendPersonaName(steamEntry.m_steamIDUser);

            if (string.IsNullOrWhiteSpace(playerName) || playerName == "[unknown]")
                playerName = "Steam Player";

            entry = new ETD.Core.LeaderboardEntry
            {
                Rank = steamEntry.m_nGlobalRank,
                PlayerName = playerName,
                Score = steamEntry.m_nScore,
                Wave = details.Length > 0 && details[0] > 0 ? details[0] : steamEntry.m_nScore,
                SurvivedWave = details.Length > 0 && details[0] > 0 ? details[0] : steamEntry.m_nScore,

                UserId = steamEntry.m_steamIDUser.m_SteamID,
                AvatarTexture = GetSteamAvatarTexture(steamEntry.m_steamIDUser),

                DurationSeconds = details.Length > 1 ? details[1] : 0,
                EnemiesKilled = details.Length > 2 ? details[2] : 0,
                BossesDefeated = details.Length > 3 ? details[3] : 0,
                TotalGoldEarned = details.Length > 4 ? details[4] : 0,
                EarnedCrystals = details.Length > 5 ? details[5] : 0,
                TurretsEvolved = details.Length > 6 ? details[6] : 0,
                LivesRemaining = details.Length > 7 ? details[7] : 0,

                IsLocalPlayer = isLocal
            };

            return true;
        }

        private Texture2D GetSteamAvatarTexture(CSteamID steamId)
        {
            int avatarHandle = SteamFriends.GetMediumFriendAvatar(steamId);

            if (avatarHandle <= 0)
                return null;

            if (!SteamUtils.GetImageSize(avatarHandle, out uint width, out uint height))
                return null;

            if (width == 0 || height == 0)
                return null;

            byte[] image = new byte[width * height * 4];

            if (!SteamUtils.GetImageRGBA(avatarHandle, image, image.Length))
                return null;

            FlipImageVertically(image, (int)width, (int)height);

            Texture2D texture = new Texture2D(
                (int)width,
                (int)height,
                TextureFormat.RGBA32,
                false,
                true
            );

            texture.LoadRawTextureData(image);
            texture.Apply();

            return texture;
        }

        private static void FlipImageVertically(byte[] data, int width, int height)
        {
            int stride = width * 4;
            byte[] row = new byte[stride];

            for (int y = 0; y < height / 2; y++)
            {
                int top = y * stride;
                int bottom = (height - 1 - y) * stride;

                Buffer.BlockCopy(data, top, row, 0, stride);
                Buffer.BlockCopy(data, bottom, data, top, stride);
                Buffer.BlockCopy(row, 0, data, bottom, stride);
            }
        }
    }
}

#endif
