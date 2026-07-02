// ============================================================================
// ETD.Core - GameSoundConfig.cs  [NEW ScriptableObject]
// Holds all global AudioClips for UI and game events.
// Assign to GameSoundPlayer in the Game scene.
// ============================================================================
using UnityEngine;

namespace ETD.Core
{
    [CreateAssetMenu(fileName = "GameSoundConfig", menuName = "ETD/Game Sound Config")]
    public class GameSoundConfig : ScriptableObject
    {
        // Static instance so any component can access sounds without a reference
        public static GameSoundConfig Instance { get; private set; }

        private void OnEnable()
        {
            Instance = this;
        }
        
        [Header("UI")]
        public AudioClip ButtonClick;
        public AudioClip ButtonHover;
        public AudioClip WindowOpen;
        public AudioClip WindowClose;
        public AudioClip TabSwitch;
        public AudioClip TraitToggle;
        public AudioClip PurchaseSuccess;
        public AudioClip PurchaseFail;

        [Header("Game Events")]
        public AudioClip WaveStart;
        public AudioClip WaveComplete;
        public AudioClip WaveWarning;      // boss/elite wave incoming
        public AudioClip LevelUp;
        public AudioClip SpecCardSelect;
        public AudioClip ChallengeComplete;
        public AudioClip UnlockNew;

        [Header("Game State")]
        public AudioClip GameOver;
        public AudioClip Pause;
        public AudioClip Unpause;

        [Header("Enemy")]
        public AudioClip[] EnemyDeath;
        public AudioClip EnemyReachedEnd;  // when enemy leaks

        [Header("Music - Legacy Single Clips")]
        public AudioClip HubMusic;
        public AudioClip GameMusic;
        public AudioClip BossMusic;
        public AudioClip GameOverMusic;

        [Header("Music - Playlists")]
        [Tooltip("Menu / Hub tracks. If more than one clip is assigned, AudioManager picks a random non-repeating next track when the current one ends.")]
        public AudioClip[] HubMusicPlaylist;

        [Tooltip("Normal run tracks used during gameplay. If more than one clip is assigned, AudioManager picks a random non-repeating next track when the current one ends.")]
        public AudioClip[] GameMusicPlaylist;

        [Tooltip("Optional boss tracks. Keep empty to use BossMusic or the normal game playlist.")]
        public AudioClip[] BossMusicPlaylist;

        [Tooltip("Optional game-over tracks. Keep empty to use GameOverMusic.")]
        public AudioClip[] GameOverMusicPlaylist;
    }
}
