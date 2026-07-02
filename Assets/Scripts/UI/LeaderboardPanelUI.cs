using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ETD.Core;

namespace ETD.UI
{
    public class LeaderboardPanelUI : MonoBehaviour
    {
        [Header("Rows (assign 10 rows in order)")]
        [SerializeField] private List<LeaderboardRowUI> _rows = new();

        [Header("State UI")]
        [SerializeField] private GameObject _loadingIndicator;
        [SerializeField] private GameObject _notSignedInMessage;
        [SerializeField] private Button _refreshButton;

        [Header("Steam Service Wait")]
        [Tooltip("When returning from a run, GameManager clears ServiceLocator. Waiting briefly prevents the Hub panel from locking to Local fallback before Steam runtime re-registers.")]
        [SerializeField] private float _waitForSteamServiceSeconds = 0.35f;

        private Coroutine _refreshRoutine;
        private int _requestVersion;

        private void Awake()
        {
            _refreshButton?.onClick.AddListener(RequestRefresh);
        }

        private void OnEnable()
        {
            RequestRefresh();
        }

        private void OnDisable()
        {
            _requestVersion++;

            if (_refreshRoutine != null)
            {
                StopCoroutine(_refreshRoutine);
                _refreshRoutine = null;
            }

            SetLoading(false);
        }

        private void RequestRefresh()
        {
            _requestVersion++;

            if (!isActiveAndEnabled)
                return;

            if (_refreshRoutine != null)
                StopCoroutine(_refreshRoutine);

            _refreshRoutine = StartCoroutine(RequestRefreshRoutine(_requestVersion));
        }

        private IEnumerator RequestRefreshRoutine(int version)
        {
            SetLoading(true);

            // Wait one frame so persistent SteamLeaderboardRuntime can repair the
            // ServiceLocator after returning from the Game scene.
            yield return null;

            ILeaderboardService service = WaitAndGetBestService(version);

            float deadline = Time.unscaledTime + Mathf.Max(0f, _waitForSteamServiceSeconds);
            while (IsRequestStillValid(version) && IsLocalFallback(service) && Time.unscaledTime < deadline)
            {
                yield return null;
                service = WaitAndGetBestService(version);
            }

            if (!IsRequestStillValid(version))
                yield break;

            if (service == null)
            {
                LeaderboardBootstrap.RegisterFallback();
                ServiceLocator.TryGet<ILeaderboardService>(out service);
            }

            if (service == null)
            {
                ShowEmptyFallback();
                yield break;
            }

            service.RequestHubLeaderboard(entries =>
            {
                if (!IsRequestStillValid(version))
                    return;

                OnLeaderboardLoaded(entries);
            });
        }

        private ILeaderboardService WaitAndGetBestService(int version)
        {
            if (!IsRequestStillValid(version))
                return null;

            ServiceLocator.TryGet<ILeaderboardService>(out ILeaderboardService service);
            return service;
        }

        private bool IsRequestStillValid(int version)
        {
            return this != null && isActiveAndEnabled && version == _requestVersion;
        }

        private static bool IsLocalFallback(ILeaderboardService service)
        {
            return service is LocalLeaderboardService;
        }

        private void OnLeaderboardLoaded(IReadOnlyList<LeaderboardEntry> entries)
        {
            SetLoading(false);

            if (_notSignedInMessage != null)
                _notSignedInMessage.SetActive(entries == null || entries.Count == 0);

            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] == null)
                    continue;

                if (entries != null && i < entries.Count && entries[i] != null && entries[i].Score > 0)
                    _rows[i].SetEntry(entries[i], isPlayerRow: i == 9 && entries[i].IsLocalPlayer);
                else
                    _rows[i].SetEmpty(i + 1);
            }
        }

        private void ShowEmptyFallback()
        {
            SetLoading(false);
            if (_notSignedInMessage != null) _notSignedInMessage.SetActive(true);

            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null)
                    _rows[i].SetEmpty(i + 1);
            }
        }

        private void SetLoading(bool loading)
        {
            if (_loadingIndicator != null) _loadingIndicator.SetActive(loading);
            if (_notSignedInMessage != null && loading) _notSignedInMessage.SetActive(false);
        }
    }
}
