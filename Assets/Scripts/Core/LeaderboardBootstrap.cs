using UnityEngine;

namespace ETD.Core
{
    public sealed class LeaderboardBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            RegisterFallback();
        }

        public static void RegisterFallback()
        {
            ILeaderboardService existing;

            // Do not overwrite Steam service if it already exists.
            if (ServiceLocator.TryGet(out existing) && existing != null)
                return;

            ServiceLocator.Register<ILeaderboardService>(
                new LocalLeaderboardService()
            );

            Debug.Log("[LeaderboardBootstrap] Using local leaderboard service.");
        }
    }
}
