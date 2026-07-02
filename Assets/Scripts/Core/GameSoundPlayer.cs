// ============================================================================
// ETD.Core - GameSoundPlayer.cs  [NEW]
// Listens to EventBus for all game events and plays sounds accordingly.
// Add to [Managers] in Game scene. Add a separate instance to Hub scene.
// ============================================================================
using UnityEngine;

namespace ETD.Core
{
    public class GameSoundPlayer : MonoBehaviour
    {
        [SerializeField] private GameSoundConfig _config;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogWarning("[GameSoundPlayer] No GameSoundConfig assigned.");
                return;
            }

            SubscribeEvents();
        }

        private void Start()
        {
            if (_config == null) return;
            PreloadConfigClips();
            AudioManager.Instance?.PlayMusicPlaylist(_config.GameMusicPlaylist, _config.GameMusic);
        }

        private void PreloadConfigClips()
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null || _config == null)
                return;

            audio.PreloadClips(_config.EnemyDeath);
            audio.PreloadClips(_config.GameMusicPlaylist);
            audio.PreloadClips(_config.HubMusicPlaylist);
            audio.PreloadClips(_config.BossMusicPlaylist);
            audio.PreloadClips(_config.GameOverMusicPlaylist);
            audio.PreloadClip(_config.ButtonClick);
            audio.PreloadClip(_config.ButtonHover);
            audio.PreloadClip(_config.WindowOpen);
            audio.PreloadClip(_config.WindowClose);
            audio.PreloadClip(_config.TabSwitch);
            audio.PreloadClip(_config.TraitToggle);
            audio.PreloadClip(_config.PurchaseSuccess);
            audio.PreloadClip(_config.PurchaseFail);
            audio.PreloadClip(_config.ChallengeComplete);
            audio.PreloadClip(_config.HubMusic);
            audio.PreloadClip(_config.GameMusic);
            audio.PreloadClip(_config.BossMusic);
            audio.PreloadClip(_config.GameOverMusic);
            audio.PreloadClip(_config.WaveStart);
            audio.PreloadClip(_config.WaveWarning);
            audio.PreloadClip(_config.WaveComplete);
            audio.PreloadClip(_config.LevelUp);
            audio.PreloadClip(_config.SpecCardSelect);
            audio.PreloadClip(_config.GameOver);
            audio.PreloadClip(_config.Pause);
            audio.PreloadClip(_config.Unpause);
            audio.PreloadClip(_config.EnemyReachedEnd);
            audio.PreloadClip(_config.UnlockNew);
        }
        private void SubscribeEvents()
        {
            // Game events
            EventBus.Subscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<SpecCardChosenEvent>(OnSpecCardChosen);
            EventBus.Subscribe<GameOverEvent>(OnGameOver);
            EventBus.Subscribe<GamePausedEvent>(OnGamePaused);
            EventBus.Subscribe<EnemyReachedEndEvent>(OnEnemyReachedEnd);
            EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Subscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Subscribe<TurretSoldEvent>(OnTurretSold);
            EventBus.Subscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Subscribe<TurretEvolvedEvent>(OnTurretEvolved);
            EventBus.Subscribe<UnlockNotificationEvent>(OnUnlockNotification);
        }

        // =================================================================
        // WAVE
        // =================================================================

        private void OnWaveStarted(WaveStartedEvent evt)
        {
            if (_config == null) return;

            // Boss waves get warning sound
            //if (evt.WaveNumber % 25 == 0 && _config.WaveWarning != null)
            //    AudioManager.Instance?.PlaySFX(_config.WaveWarning, SoundCategory.WaveStart, 1.2f);
            //else
            //    AudioManager.Instance?.PlaySFX(_config.WaveStart, SoundCategory.WaveStart);

            // Switch to boss music on boss waves
            //if (evt.WaveNumber % 25 == 0 && _config.BossMusic != null)
            //    AudioManager.Instance?.PlayMusic(_config.BossMusic);
        }

        private void OnWaveCompleted(WaveCompletedEvent evt)
        {
           // AudioManager.Instance?.PlaySFX(_config?.WaveComplete, SoundCategory.WaveComplete);

            // Return to normal game music after boss
            //if (evt.WaveNumber % 25 == 0 && _config?.GameMusic != null)
            //    AudioManager.Instance?.PlayMusic(_config.GameMusic);
        }

        // =================================================================
        // LEVEL UP / SPEC CARD
        // =================================================================

        private void OnLevelUp(LevelUpEvent evt)
        {
            AudioManager.Instance?.PlaySFX(_config?.LevelUp, SoundCategory.LevelUp, 1.1f);
        }

        private void OnSpecCardChosen(SpecCardChosenEvent evt)
        {
            AudioManager.Instance?.PlaySFX(_config?.SpecCardSelect, SoundCategory.SpecCard);
        }

        // =================================================================
        // GAME STATE
        // =================================================================

        private void OnGameOver(GameOverEvent evt)
        {
            AudioManager.Instance?.PlaySFX(_config?.GameOver, SoundCategory.UI, 1f);
            //if (_config?.GameOverMusic != null)
            //    AudioManager.Instance?.PlayMusic(_config.GameOverMusic, false);
        }

        private void OnGamePaused(GamePausedEvent evt)
        {
            bool isPaused = GameManager.Instance?.CurrentState == GameState.Paused;
            if (isPaused)
                AudioManager.Instance?.PlaySFX(_config?.Pause, SoundCategory.UI, 0.7f);
            else
                AudioManager.Instance?.PlaySFX(_config?.Unpause, SoundCategory.UI, 0.7f);
        }

        // =================================================================
        // ENEMY
        // =================================================================

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            // Death sound — position would need to come from event
            // For now play as 2D (world position not in event)
            if (_config?.EnemyDeath != null && _config.EnemyDeath.Length > 0)
            {
                var clip = _config.EnemyDeath[Random.Range(0, _config.EnemyDeath.Length)];
                AudioManager.Instance?.PlaySFX(clip, SoundCategory.EnemyDeath,
                    0.7f, Random.Range(0.9f, 1.1f));
            }
        }

        private void OnEnemyReachedEnd(EnemyReachedEndEvent evt)
        {
            AudioManager.Instance?.PlaySFX(_config?.EnemyReachedEnd, SoundCategory.UI, 0.9f);
        }

        // =================================================================
        // TURRET MANAGEMENT
        // =================================================================

        private void OnTurretPlaced(TurretPlacedEvent evt)
        {
            // Turret prefab's TurretSoundConfig handles this per-type.
            // This is just a fallback in case the prefab has no sound config.
        }

        private void OnTurretSold(TurretSoldEvent evt) { }

        private void OnTurretUpgraded(TurretUpgradedEvent evt) { }

        private void OnTurretEvolved(TurretEvolvedEvent evt) { }

        // =================================================================
        // UNLOCKS
        // =================================================================

        private void OnUnlockNotification(UnlockNotificationEvent evt)
        {
            AudioManager.Instance?.PlaySFX(_config?.UnlockNew, SoundCategory.UI, 1f);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<SpecCardChosenEvent>(OnSpecCardChosen);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Unsubscribe<GamePausedEvent>(OnGamePaused);
            EventBus.Unsubscribe<EnemyReachedEndEvent>(OnEnemyReachedEnd);
            EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Unsubscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Unsubscribe<TurretSoldEvent>(OnTurretSold);
            EventBus.Unsubscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Unsubscribe<TurretEvolvedEvent>(OnTurretEvolved);
            EventBus.Unsubscribe<UnlockNotificationEvent>(OnUnlockNotification);
        }
    }
}
