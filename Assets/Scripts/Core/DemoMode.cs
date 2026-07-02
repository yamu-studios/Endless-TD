using UnityEngine;

namespace ETD.Core
{
    /// <summary>
    /// Central switch for the Steam demo build.
    /// Full builds stay uncapped. Demo builds are enabled by ETD_DEMO_BUILD.
    /// In the Unity Editor, demo mode can also be enabled from ETD/Demo/Enable Demo Mode In Editor.
    /// </summary>
    public static class DemoMode
    {
        public const int MaxPlayableWave = 25;
        public const string SaveFolderSuffix = "_Demo";
        public const string LeaderboardSuffix = "_demo";

        // Replace this with your full game App ID when your Steam page is ready.
        // If empty, the Wishlist button falls back to a Steam search page for Endless Defense.
        public const string FullGameSteamAppId = "";
        public const string FallbackSteamSearchUrl = "https://store.steampowered.com/search/?term=Endless%20Defense";

        public const string DemoCompletedTitleKey = "demo_complete_title";
        public const string DemoCompletedBodyKey = "demo_complete_body";
        public const string DemoCompletedReachedWaveKey = "demo_complete_reached_wave";
        public const string WishlistButtonKey = "demo_complete_wishlist_button";
        public const string ReturnMenuButtonKey = "demo_complete_return_menu_button";

        public const string DemoCompletedTitleFallback = "Demo Complete";
        public const string DemoCompletedBodyFallback = "Thank you for playing the Endless Defense Demo. Want to discover more turrets, traits, enemies, and endless waves? Visit the Steam page and wishlist the full game.";
        public const string DemoCompletedReachedWaveFallback = "Reached Wave {0}";
        public const string WishlistButtonFallback = "Wishlist on Steam";
        public const string ReturnMenuButtonFallback = "Return to Menu";

        public const string EditorDemoPlayerPrefsKey = "ETD_DEMO_EDITOR_TEST";

        public static bool DemoCompletedThisSession { get; private set; }
        public static int CompletedAtWave { get; private set; }

        // GameOverUI should be suppressed only for the exact GameOver sequence
        // that was created by demo completion. It must not suppress later
        // deaths after Retry.
        public static int DemoCompletedGameOverSequence { get; private set; } = -1;

        public static bool IsDemo
        {
            get
            {
#if ETD_DEMO_BUILD
                return true;
#elif UNITY_EDITOR
                return PlayerPrefs.GetInt(EditorDemoPlayerPrefsKey, 0) == 1;
#else
                return false;
#endif
            }
        }

        public static string BuildLabel => IsDemo ? "Steam Demo" : "Full Game";

        public static string FullGameStoreUrl
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(FullGameSteamAppId))
                    return $"https://store.steampowered.com/app/{FullGameSteamAppId}/";

                return FallbackSteamSearchUrl;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetSessionState()
        {
            ResetCompletionForNewRun();

            if (IsDemo)
                Debug.Log($"[ETD Demo] Demo mode active. Max playable wave: {MaxPlayableWave}");
        }

        public static void ResetCompletionForNewRun()
        {
            DemoCompletedThisSession = false;
            CompletedAtWave = 0;
            DemoCompletedGameOverSequence = -1;
        }

        public static bool ShouldSuppressGameOverUIForCurrentSequence(GameManager manager)
        {
            return IsDemo
                && DemoCompletedThisSession
                && manager != null
                && DemoCompletedGameOverSequence >= 0
                && manager.LastGameOverSequence == DemoCompletedGameOverSequence;
        }

        public static bool ShouldBlockStartingNextWave(int currentWave)
        {
            return IsDemo && currentWave >= MaxPlayableWave;
        }

        public static void CompleteDemo(int reachedWave)
        {
            if (!IsDemo || DemoCompletedThisSession)
                return;

            CompletedAtWave = Mathf.Max(MaxPlayableWave, reachedWave);
            DemoCompletedThisSession = true;

            Debug.Log($"[ETD Demo] Demo completed at wave {CompletedAtWave}.");

            var manager = GameManager.Instance;
            if (manager != null)
            {
                if (manager.CurrentState != GameState.GameOver)
                {
                    // TriggerGameOver increments the sequence before publishing.
                    // Store the expected sequence first so GameOverUI can suppress
                    // only this specific demo-complete GameOver.
                    DemoCompletedGameOverSequence = manager.LastGameOverSequence + 1;
                    manager.TriggerGameOver(CompletedAtWave, CompletedAtWave);
                }
                else
                {
                    DemoCompletedGameOverSequence = manager.LastGameOverSequence;
                }
            }

            EventBus.Publish(new DemoCompletedEvent { ReachedWave = CompletedAtWave });
        }
    }
}
