#if !DISABLESTEAMWORKS

using ETD.Core;
using UnityEngine;

namespace ETD.Steam
{
    public sealed class SteamLeaderboardBootstrap : MonoBehaviour
    {
        private static SteamLeaderboardBootstrap _instance;

        private SteamLeaderboardRuntime _runtime;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            RegisterService();
        }

        private void OnEnable()
        {
            RegisterService();
        }

        private void RegisterService()
        {
            _runtime = SteamLeaderboardRuntime.Instance;

            if (_runtime == null)
            {
                _runtime = gameObject.GetComponent<SteamLeaderboardRuntime>();
                if (_runtime == null)
                    _runtime = gameObject.AddComponent<SteamLeaderboardRuntime>();
            }

            ServiceLocator.Register<ILeaderboardService>(_runtime);
            Debug.Log("[SteamLeaderboardBootstrap] Unified Steam leaderboard runtime registered.");
        }
    }
}

#endif
