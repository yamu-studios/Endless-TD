// ETD.Core - MusicManager.cs
// Manages automatic music track switching based on game state.
// Crossfades smoothly between tracks using two AudioSource ping-pong.
// Tracks: Menu, Preparation, Wave, Boss Wave, Game Over.
// Lives on [Persistent] in Boot scene (DontDestroyOnLoad).
// ============================================================================
using System.Collections;
using UnityEngine;

namespace ETD.Core
{
    public enum MusicTrack
    {
        None,
        Menu,
        Preparation,
        Wave,
        BossWave,
        GameOver
    }

    public class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }

        [Header("Tracks - Legacy Single Clips")]
        [SerializeField] private AudioClip _menuTrack;
        [SerializeField] private AudioClip _preparationTrack;
        [SerializeField] private AudioClip _waveTrack;
        [SerializeField] private AudioClip _bossWaveTrack;
        [SerializeField] private AudioClip _gameOverTrack;

        [Header("Tracks - Playlists")]
        [SerializeField] private AudioClip[] _menuTracks;
        [Tooltip("Optional separate preparation playlist. Leave empty to use Game Tracks.")]
        [SerializeField] private AudioClip[] _preparationTracks;
        [Tooltip("Normal in-run playlist. Used for Wave, and also Preparation if Preparation Tracks is empty.")]
        [SerializeField] private AudioClip[] _gameTracks;
        [SerializeField] private AudioClip[] _bossWaveTracks;
        [SerializeField] private AudioClip[] _gameOverTracks;

        [Header("Playlist Behaviour")]
        [SerializeField] private bool _randomizePlaylistOrder = true;
        [SerializeField] private bool _avoidImmediateRepeat = true;

        [Header("Crossfade")]
        [SerializeField] private float _crossfadeDuration = 1.5f;
        [SerializeField] private float _bossWarningDelay = 0.5f; // delay before boss music kicks in

        [Header("Volume")]
        [SerializeField][Range(0f, 1f)] private float _trackVolume = 1f;

        // Two sources for ping-pong crossfade
        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private bool _aIsActive = true;

        // Logical fade weights are kept separate from final AudioSource.volume.
        // Final volume is always: fadeWeight * _trackVolume * master * music.
        // This prevents crossfade coroutines from overriding SettingsWindowUI slider changes.
        private float _sourceAFadeWeight;
        private float _sourceBFadeWeight;

        private MusicTrack _currentTrack = MusicTrack.None;
        private AudioClip _lastMenuClip;
        private AudioClip _lastPreparationClip;
        private AudioClip _lastWaveClip;
        private AudioClip _lastBossWaveClip;
        private AudioClip _lastGameOverClip;
        private Coroutine _fadeCoroutine;
        private Coroutine _bossDelayCoroutine;
        private int _currentWave;

        private bool _wasPausedByGame;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            CreateSources();
        }

        private void Start()
        {
            SubscribeEvents();
            // Start with menu music
            Play(MusicTrack.Menu, instant: true);
        }

        private void CreateSources()
        {
            var goA = new GameObject("MusicSource_A");
            var goB = new GameObject("MusicSource_B");
            goA.transform.SetParent(transform);
            goB.transform.SetParent(transform);

            _sourceA = goA.AddComponent<AudioSource>();
            _sourceB = goB.AddComponent<AudioSource>();

            foreach (var src in new[] { _sourceA, _sourceB })
            {
                src.loop = true;
                src.spatialBlend = 0f;
                src.playOnAwake = false;
                src.volume = 0f;
            }
        }

        // =================================================================
        // PLAY / SWITCH
        // =================================================================

        public void Play(MusicTrack track, bool instant = false)
        {
            Play(track, instant, forceNextClip: false);
        }

        private void Play(MusicTrack track, bool instant, bool forceNextClip)
        {
            if (_currentTrack == track && !forceNextClip) return;

            bool shouldLoop;
            var clip = GetNextClip(track, forceNextClip, out shouldLoop);
            if (clip == null && track != MusicTrack.None) return;

            _currentTrack = track;

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(CrossfadeTo(clip, shouldLoop, instant ? 0f : _crossfadeDuration));
        }

        private AudioClip GetNextClip(MusicTrack track, bool forceDifferentFromLast, out bool shouldLoop)
        {
            AudioClip[] playlist = GetPlaylist(track);
            AudioClip fallback = GetFallbackClip(track);
            int count = CountClips(playlist, fallback);
            shouldLoop = count <= 1;

            if (count <= 0)
                return null;

            if (count == 1)
            {
                AudioClip only = GetPackedClip(0, playlist, fallback);
                SetLastClip(track, only);
                return only;
            }

            AudioClip last = GetLastClip(track);
            AudioClip selected;

            if (_randomizePlaylistOrder)
            {
                int index = Random.Range(0, count);
                selected = GetPackedClip(index, playlist, fallback);

                if (_avoidImmediateRepeat && forceDifferentFromLast && selected == last)
                {
                    index = (index + 1) % count;
                    selected = GetPackedClip(index, playlist, fallback);
                }
            }
            else
            {
                int currentIndex = GetPackedIndex(last, playlist, fallback);
                selected = GetPackedClip(currentIndex < 0 ? 0 : (currentIndex + 1) % count, playlist, fallback);
            }

            SetLastClip(track, selected);
            return selected;
        }

        private AudioClip[] GetPlaylist(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Menu:
                    return _menuTracks;
                case MusicTrack.Preparation:
                    return _preparationTracks;
                case MusicTrack.Wave:
                    return _gameTracks;
                case MusicTrack.BossWave:
                    return HasAnyClip(_bossWaveTracks) ? _bossWaveTracks : _gameTracks;
                case MusicTrack.GameOver:
                    return _gameOverTracks;
                default:
                    return null;
            }
        }

        private AudioClip GetFallbackClip(MusicTrack track) => track switch
        {
            MusicTrack.Menu => _menuTrack,
            MusicTrack.Preparation => _preparationTrack,
            MusicTrack.Wave => _waveTrack,
            MusicTrack.BossWave => _bossWaveTrack != null ? _bossWaveTrack : _waveTrack,
            MusicTrack.GameOver => _gameOverTrack,
            _ => null
        };

        // =================================================================
        // CROSSFADE COROUTINE
        // =================================================================

        private IEnumerator CrossfadeTo(AudioClip newClip, bool loop, float duration)
        {
            AudioSource incoming = _aIsActive ? _sourceB : _sourceA;
            AudioSource outgoing = _aIsActive ? _sourceA : _sourceB;
            _aIsActive = !_aIsActive;

            bool incomingIsA = incoming == _sourceA;
            bool outgoingIsA = outgoing == _sourceA;

            // Start new clip
            if (newClip != null)
            {
                incoming.clip = newClip;
                incoming.loop = loop;
                SetFadeWeight(incomingIsA, 0f);
                incoming.Play();
            }
            else
            {
                incoming.Stop();
                incoming.clip = null;
                SetFadeWeight(incomingIsA, 0f);
            }

            if (duration <= 0f)
            {
                SetFadeWeight(outgoingIsA, 0f);
                outgoing.Stop();
                outgoing.clip = null;

                SetFadeWeight(incomingIsA, newClip != null ? 1f : 0f);
                ApplySourceVolumes();
                _fadeCoroutine = null;
                yield break;
            }

            float elapsed = 0f;
            float startOutWeight = GetFadeWeight(outgoingIsA);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                SetFadeWeight(outgoingIsA, Mathf.Lerp(startOutWeight, 0f, t));
                if (newClip != null)
                    SetFadeWeight(incomingIsA, t);

                ApplySourceVolumes();
                yield return null;
            }

            SetFadeWeight(outgoingIsA, 0f);
            outgoing.Stop();
            outgoing.clip = null;

            if (newClip != null)
                SetFadeWeight(incomingIsA, 1f);

            ApplySourceVolumes();
            _fadeCoroutine = null;
        }


        private bool CurrentTrackCanAutoAdvance()
        {
            if (_currentTrack == MusicTrack.None)
                return false;

            AudioClip[] playlist = GetPlaylist(_currentTrack);
            AudioClip fallback = GetFallbackClip(_currentTrack);
            return CountClips(playlist, fallback) > 1;
        }

        private void TryAdvancePlaylistIfFinished()
        {
            if (_fadeCoroutine != null || _wasPausedByGame || !CurrentTrackCanAutoAdvance())
                return;

            AudioSource active = ActiveSource();
            if (active == null || active.clip == null || active.loop || active.isPlaying)
                return;

            Play(_currentTrack, instant: false, forceNextClip: true);
        }

        private AudioClip GetLastClip(MusicTrack track) => track switch
        {
            MusicTrack.Menu => _lastMenuClip,
            MusicTrack.Preparation => _lastPreparationClip,
            MusicTrack.Wave => _lastWaveClip,
            MusicTrack.BossWave => _lastBossWaveClip,
            MusicTrack.GameOver => _lastGameOverClip,
            _ => null
        };

        private void SetLastClip(MusicTrack track, AudioClip clip)
        {
            switch (track)
            {
                case MusicTrack.Menu:
                    _lastMenuClip = clip;
                    break;
                case MusicTrack.Preparation:
                    _lastPreparationClip = clip;
                    break;
                case MusicTrack.Wave:
                    _lastWaveClip = clip;
                    break;
                case MusicTrack.BossWave:
                    _lastBossWaveClip = clip;
                    break;
                case MusicTrack.GameOver:
                    _lastGameOverClip = clip;
                    break;
            }
        }

        private static bool HasAnyClip(AudioClip[] clips)
        {
            if (clips == null) return false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null) return true;
            }
            return false;
        }

        private static int CountClips(AudioClip[] clips, AudioClip fallback)
        {
            int count = 0;
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] != null) count++;
                }
            }

            if (count == 0 && fallback != null)
                count = 1;

            return count;
        }

        private static AudioClip GetPackedClip(int packedIndex, AudioClip[] clips, AudioClip fallback)
        {
            if (clips != null)
            {
                int packed = 0;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] == null) continue;
                    if (packed == packedIndex) return clips[i];
                    packed++;
                }
            }

            return fallback;
        }

        private static int GetPackedIndex(AudioClip clip, AudioClip[] clips, AudioClip fallback)
        {
            if (clip == null) return -1;

            if (clips != null)
            {
                int packed = 0;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] == null) continue;
                    if (clips[i] == clip) return packed;
                    packed++;
                }
            }

            return fallback == clip ? 0 : -1;
        }

        // =================================================================
        // VOLUME
        // =================================================================

        private float GetTargetVolume()
        {
            if (AudioManager.Instance == null)
                return _trackVolume;

            return _trackVolume
                * AudioManager.Instance.MasterVolume
                * AudioManager.Instance.MusicVolume;
        }

        private float GetFadeWeight(bool sourceA)
        {
            return sourceA ? _sourceAFadeWeight : _sourceBFadeWeight;
        }

        private void SetFadeWeight(bool sourceA, float weight)
        {
            weight = Mathf.Clamp01(weight);
            if (sourceA)
                _sourceAFadeWeight = weight;
            else
                _sourceBFadeWeight = weight;
        }

        private void ApplySourceVolumes()
        {
            float targetVolume = GetTargetVolume();

            if (_sourceA != null)
                _sourceA.volume = _sourceAFadeWeight * targetVolume;

            if (_sourceB != null)
                _sourceB.volume = _sourceBFadeWeight * targetVolume;
        }

        /// <summary>
        /// Called by AudioManager when master/music volume changes.
        /// This must not change fade weights; otherwise moving the slider during a crossfade
        /// makes the outgoing track jump back to full volume.
        /// </summary>
        public void RefreshVolume()
        {
            ApplySourceVolumes();
        }

        private AudioSource ActiveSource() => _aIsActive ? _sourceA : _sourceB;

        // =================================================================
        // EVENT SUBSCRIPTIONS
        // =================================================================

        private void SubscribeEvents()
        {
            EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
            EventBus.Subscribe<GameOverEvent>(OnGameOver);
        }

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            var state = (GameState)evt.NewState;

            switch (state)
            {
                case GameState.Hub:
                    CancelBossDelay();
                    Play(MusicTrack.Menu);
                    break;

                case GameState.Preparation:
                    // Only switch if coming from hub or game over
                    if (_currentTrack == MusicTrack.Menu
                     || _currentTrack == MusicTrack.GameOver
                     || _currentTrack == MusicTrack.None)
                        Play(MusicTrack.Preparation);
                    break;

                case GameState.Paused:
                    // Pause music (optional: lower volume instead of stopping)
                    PauseMusic(true);
                    break;
            }
        }

        private void OnWaveStarted(WaveStartedEvent evt)
        {
            _currentWave = evt.WaveNumber;
            CancelBossDelay();

            if (evt.WaveNumber % 25 == 0)
            {
                // Boss wave — delay slightly for dramatic effect
                _bossDelayCoroutine = StartCoroutine(DelayedPlay(MusicTrack.BossWave, _bossWarningDelay, evt.WaveNumber));
            }
            else
            {
                Play(MusicTrack.Wave);
            }
        }

        private void OnWaveCompleted(WaveCompletedEvent evt)
        {
            CancelBossDelay();
            // Return to prep music after wave ends
            Play(MusicTrack.Preparation);
        }

        private void OnGameOver(GameOverEvent evt)
        {
            CancelBossDelay();
            Play(MusicTrack.GameOver);
        }

        private IEnumerator DelayedPlay(MusicTrack track, float delay, int waveNumberAtRequest)
        {
            yield return new WaitForSecondsRealtime(delay);

            // Do not let a delayed boss cue override a state that has already advanced.
            if (_currentWave == waveNumberAtRequest
                && GameManager.Instance != null
                && GameManager.Instance.CurrentState == GameState.WaveActive)
            {
                Play(track);
            }

            _bossDelayCoroutine = null;
        }

        private void CancelBossDelay()
        {
            if (_bossDelayCoroutine != null)
            {
                StopCoroutine(_bossDelayCoroutine);
                _bossDelayCoroutine = null;
            }
        }

        // =================================================================
        // PAUSE / RESUME
        // =================================================================

        public void PauseMusic(bool paused)
        {
            if (paused)
            {
                _wasPausedByGame = true;
                if (_sourceA != null && _sourceA.isPlaying) _sourceA.Pause();
                if (_sourceB != null && _sourceB.isPlaying) _sourceB.Pause();
            }
            else if (_wasPausedByGame)
            {
                _wasPausedByGame = false;
                if (_sourceA != null) _sourceA.UnPause();
                if (_sourceB != null) _sourceB.UnPause();
            }
        }

        // Also unpause when game resumes and advance playlists when a non-looping clip ends.
        private void Update()
        {
            if (_wasPausedByGame
                && GameManager.Instance != null
                && GameManager.Instance.CurrentState != GameState.Paused)
            {
                PauseMusic(false);
            }

            TryAdvancePlaylistIfFinished();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
        }
    }
}
