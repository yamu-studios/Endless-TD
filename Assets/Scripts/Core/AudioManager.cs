// ============================================================================
// ETD.Core - AudioManager.cs  [UPDATED - split SFX into GameSounds + UI]
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace ETD.Core
{
    public enum SoundCategory
    {
        Music,
        UI,            // button clicks, level up — uses _uiVolume
        TurretShoot,   // uses _gameSoundsVolume
        TurretPlace,
        TurretUpgrade,
        TurretSell,
        TurretEvolve,
        ProjectileHit,
        EnemyDeath,
        EnemyStatus,
        WaveStart,
        WaveComplete,
        LevelUp,
        SpecCard,
        ChainLightning,
        Ambient,
        LoopingSFX
    }

    [System.Serializable]
    public class SoundCategorySettings
    {
        public SoundCategory Category;
        public int MaxSimultaneous = 4;
        public float Cooldown = 0f;
        [HideInInspector] public float LastPlayTime = -99f;
    }

    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Music")]
        [SerializeField] private AudioSource _musicSource;

        [Header("Music Playlist")]
        [SerializeField] private bool _avoidImmediateMusicRepeat = true;

        private AudioClip[] _musicPlaylist;
        private AudioClip _musicPlaylistFallback;
        private AudioClip _lastMusicClip;
        private bool _musicPlaylistActive;
        private bool _musicPlaylistRandom = true;
        private bool _musicPlaylistStarted;

        [Header("SFX Pool")]
        [SerializeField] private int _sfxPoolSize = 24;

        [Tooltip("Maximum AudioSource.Play calls allowed in one rendered frame. Extra low-priority requests are skipped to avoid SoundManager.LoadFMODSound / audio-thread spikes.")]
        [SerializeField, Min(1)] private int _maxSfxStartsPerFrame = 10;

        [Header("Category Limits")]
        [SerializeField]
        private SoundCategorySettings[] _categorySettings = new SoundCategorySettings[]
        {
            new() { Category = SoundCategory.UI,            MaxSimultaneous = 8,  Cooldown = 0f    },
            new() { Category = SoundCategory.TurretShoot,   MaxSimultaneous = 4,  Cooldown = 0.08f },
            new() { Category = SoundCategory.TurretPlace,   MaxSimultaneous = 2,  Cooldown = 0f    },
            new() { Category = SoundCategory.TurretUpgrade, MaxSimultaneous = 2,  Cooldown = 0f    },
            new() { Category = SoundCategory.TurretSell,    MaxSimultaneous = 2,  Cooldown = 0f    },
            new() { Category = SoundCategory.TurretEvolve,  MaxSimultaneous = 1,  Cooldown = 0f    },
            new() { Category = SoundCategory.ProjectileHit, MaxSimultaneous = 6,  Cooldown = 0.05f },
            new() { Category = SoundCategory.EnemyDeath,    MaxSimultaneous = 4,  Cooldown = 0.08f },
            new() { Category = SoundCategory.EnemyStatus,   MaxSimultaneous = 3,  Cooldown = 0.1f  },
            new() { Category = SoundCategory.WaveStart,     MaxSimultaneous = 1,  Cooldown = 0f    },
            new() { Category = SoundCategory.WaveComplete,  MaxSimultaneous = 1,  Cooldown = 0f    },
            new() { Category = SoundCategory.LevelUp,       MaxSimultaneous = 1,  Cooldown = 0f    },
            new() { Category = SoundCategory.SpecCard,      MaxSimultaneous = 2,  Cooldown = 0f    },
            new() { Category = SoundCategory.ChainLightning,MaxSimultaneous = 2,  Cooldown = 0.15f },
            new() { Category = SoundCategory.Ambient,       MaxSimultaneous = 2,  Cooldown = 0f    },
        };

        // SFX pool
        private AudioSource[] _sfxPool;
        private SfxPlaybackState[] _sfxStates;
        private int _sfxIndex;
        private int _sfxStartFrame = -1;
        private int _sfxStartsThisFrame;
        private readonly HashSet<int> _preparedClipIds = new();

        private struct SfxPlaybackState
        {
            public SoundCategory Category;
            public float VolumeScale;

            // FIX (profiler-confirmed O(turrets x poolSize) scan eliminated):
            // AudioSource.isPlaying is a C#-to-native bridge call, not a free field
            // read. With 78 turret ticks/frame (confirmed profiler, wave 55) and a
            // 24-slot pool, CountPlayingCategory was issuing 78x24=1,872 native
            // isPlaying queries per frame for CanPlay checks alone — entirely
            // invisible in the profiler since it had no BeginSample wrapper and
            // shows as self-time inside AimAndAttack.
            // Fix: EstimatedEndTime is set to (Time.unscaledTime + clip.length) at
            // play-time. CountPlayingCategory compares floats instead — zero native
            // calls, O(poolSize) not O(turrets x poolSize).
            public float EstimatedEndTime;
        }

        private sealed class LoopingSfxState
        {
            public AudioSource Source;
            public SoundCategory Category;
            public float VolumeScale = 1f;
        }

        // Looping SFX (laser/flame)
        private readonly Dictionary<int, LoopingSfxState> _loopingSources = new();

        // Volumes
        private float _masterVolume = 1f;
        private float _gameSoundsVolume = 0.8f;
        private float _uiVolume = 0.8f;
        private float _musicVolume = 0.7f;

        private readonly Dictionary<SoundCategory, SoundCategorySettings> _categoryMap = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitPool();
            BuildCategoryMap();
            LoadVolumeSettings();
        }

        private void Update()
        {
            UpdateMusicPlaylist();
        }

        private void InitPool()
        {
            _sfxPool = new AudioSource[_sfxPoolSize];
            _sfxStates = new SfxPlaybackState[_sfxPoolSize];
            for (int i = 0; i < _sfxPoolSize; i++)
            {
                var go = new GameObject($"SFX_{i}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _sfxPool[i] = src;
                _sfxStates[i] = new SfxPlaybackState
                {
                    Category = SoundCategory.UI,
                    VolumeScale = 1f
                };
            }
        }

        private void BuildCategoryMap()
        {
            foreach (var s in _categorySettings)
                _categoryMap[s.Category] = s;
        }

        // =================================================================
        // PLAY 2D
        // =================================================================

        public void PlaySFX(AudioClip clip, SoundCategory category = SoundCategory.UI,
            float volumeScale = 1f, float pitch = 1f)
        {
            if (clip == null || !CanPlayCategory(category)) return;
            EnsureClipLoaded(clip);
            int sourceIndex = GetFreeSourceIndex();
            if (sourceIndex < 0) return;
            var src = _sfxPool[sourceIndex];
            ConfigSource(sourceIndex, src, clip, Vector3.zero, false, volumeScale, category, pitch);
            src.Play();
            MarkPlayed(category);
        }

        // =================================================================
        // PLAY 3D
        // =================================================================

        public void PlaySFX3D(AudioClip clip, Vector3 worldPos,
            SoundCategory category = SoundCategory.ProjectileHit,
            float volumeScale = 1f, float pitch = 1f,
            float minDist = 3f, float maxDist = 20f)
        {
            if (clip == null || !CanPlayCategory(category)) return;
            EnsureClipLoaded(clip);
            int sourceIndex = GetFreeSourceIndex();
            if (sourceIndex < 0) return;
            var src = _sfxPool[sourceIndex];
            src.transform.position = worldPos;
            src.minDistance = minDist;
            src.maxDistance = maxDist;
            src.rolloffMode = AudioRolloffMode.Linear;
            ConfigSource(sourceIndex, src, clip, worldPos, true, volumeScale, category, pitch);
            src.Play();
            MarkPlayed(category);
        }

        public void PlaySFXRandom(AudioClip[] clips, Vector3 worldPos,
            SoundCategory category, float volumeScale = 1f,
            float pitchMin = 0.92f, float pitchMax = 1.08f)
        {
            if (clips == null || clips.Length == 0 || !CanPlayCategory(category)) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
                return;

            EnsureClipLoaded(clip);
            int sourceIndex = GetFreeSourceIndex();
            if (sourceIndex < 0) return;

            var src = _sfxPool[sourceIndex];
            src.transform.position = worldPos;
            src.minDistance = 3f;
            src.maxDistance = 20f;
            src.rolloffMode = AudioRolloffMode.Linear;
            ConfigSource(sourceIndex, src, clip, worldPos, true, volumeScale, category,
                Random.Range(pitchMin, pitchMax));
            src.Play();
            MarkPlayed(category);
        }

        // =================================================================
        // LOOPING
        // =================================================================

        public void StartLoop(AudioClip clip, GameObject owner, float volumeScale = 1f, float pitch = 1f)
        {
            StartLoop(clip, owner, volumeScale, pitch, SoundCategory.LoopingSFX);
        }

        public void StartLoop(AudioClip clip, GameObject owner, float volumeScale, float pitch, SoundCategory category)
        {
            if (clip == null || owner == null || GetCategoryVolume(category) <= 0.001f) return;
            EnsureClipLoaded(clip);
            int id = owner.GetInstanceID();

            LoopingSfxState state;
            AudioSource src;
            if (_loopingSources.TryGetValue(id, out state) && state != null && state.Source != null)
            {
                src = state.Source;
                if (src.clip == clip && src.isPlaying)
                {
                    state.Category = category;
                    state.VolumeScale = volumeScale;
                    src.volume = GetCategoryVolume(category) * volumeScale;
                    src.pitch = pitch;
                    return;
                }

                src.Stop();
            }
            else
            {
                var go = new GameObject($"Loop_{owner.name}");
                go.transform.SetParent(owner.transform);
                go.transform.localPosition = Vector3.zero;
                src = go.AddComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.minDistance = 2f;
                src.maxDistance = 15f;
                src.rolloffMode = AudioRolloffMode.Linear;

                state = new LoopingSfxState { Source = src };
                _loopingSources[id] = state;
            }

            state.Category = category;
            state.VolumeScale = volumeScale;

            src.clip = clip;
            src.loop = true;
            src.volume = GetCategoryVolume(category) * volumeScale;
            src.pitch = pitch;
            src.Play();
        }

        public void StopLoop(GameObject owner)
        {
            if (owner == null) return;
            if (_loopingSources.TryGetValue(owner.GetInstanceID(), out var state) && state?.Source != null)
            {
                state.Source.Stop();
                state.Source.clip = null;
            }
        }

        // =================================================================
        // MUSIC
        // =================================================================

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            _musicPlaylistActive = false;
            _musicPlaylist = null;
            _musicPlaylistFallback = null;
            _musicPlaylistStarted = false;

            if (_musicSource == null || clip == null) return;
            _lastMusicClip = clip;
            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.volume = _musicVolume * _masterVolume;
            _musicSource.Play();
        }

        public void PlayMusicPlaylist(AudioClip[] clips, AudioClip fallback = null, bool random = true)
        {
            if (_musicSource == null) return;

            bool hasPlaylist = HasAnyClip(clips);
            if (!hasPlaylist && fallback == null)
            {
                StopMusic();
                return;
            }

            if (_musicPlaylistActive
                && _musicPlaylist == clips
                && _musicPlaylistFallback == fallback
                && _musicPlaylistRandom == random
                && _musicSource.isPlaying)
            {
                return;
            }

            _musicPlaylist = clips;
            _musicPlaylistFallback = fallback;
            _musicPlaylistRandom = random;
            _musicPlaylistActive = true;
            _musicPlaylistStarted = true;

            PlayNextMusicPlaylistClip(forceDifferentFromCurrent: false);
        }

        public void StopMusic()
        {
            _musicPlaylistActive = false;
            _musicPlaylist = null;
            _musicPlaylistFallback = null;
            _musicPlaylistStarted = false;
            _musicSource?.Stop();
        }

        private void UpdateMusicPlaylist()
        {
            if (!_musicPlaylistActive || _musicSource == null || !_musicPlaylistStarted)
                return;

            // Single-track playlists loop through AudioSource.loop. Multi-track playlists do not loop;
            // when a clip ends, the next random/non-repeating clip starts here.
            if (_musicSource.loop)
                return;

            if (_musicSource.clip == null)
            {
                PlayNextMusicPlaylistClip(forceDifferentFromCurrent: false);
                return;
            }

            if (!_musicSource.isPlaying)
                PlayNextMusicPlaylistClip(forceDifferentFromCurrent: true);
        }

        private void PlayNextMusicPlaylistClip(bool forceDifferentFromCurrent)
        {
            AudioClip next = SelectNextPlaylistClip(forceDifferentFromCurrent);
            if (next == null)
                return;

            int count = CountPlaylistClips(_musicPlaylist, _musicPlaylistFallback);
            _lastMusicClip = next;
            _musicSource.clip = next;
            _musicSource.loop = count <= 1;
            _musicSource.volume = _musicVolume * _masterVolume;
            _musicSource.Play();
        }

        private AudioClip SelectNextPlaylistClip(bool forceDifferentFromCurrent)
        {
            int count = CountPlaylistClips(_musicPlaylist, _musicPlaylistFallback);
            if (count <= 0)
                return null;

            if (count == 1)
                return GetPlaylistClipByPackedIndex(0, _musicPlaylist, _musicPlaylistFallback);

            if (!_musicPlaylistRandom)
            {
                int currentPacked = GetPackedIndexOfClip(_lastMusicClip, _musicPlaylist, _musicPlaylistFallback);
                int nextPacked = currentPacked < 0 ? 0 : (currentPacked + 1) % count;
                return GetPlaylistClipByPackedIndex(nextPacked, _musicPlaylist, _musicPlaylistFallback);
            }

            int index = Random.Range(0, count);
            AudioClip selected = GetPlaylistClipByPackedIndex(index, _musicPlaylist, _musicPlaylistFallback);

            if (_avoidImmediateMusicRepeat && forceDifferentFromCurrent && count > 1 && selected == _lastMusicClip)
            {
                index = (index + 1) % count;
                selected = GetPlaylistClipByPackedIndex(index, _musicPlaylist, _musicPlaylistFallback);
            }

            return selected;
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

        private static int CountPlaylistClips(AudioClip[] clips, AudioClip fallback)
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

        private static AudioClip GetPlaylistClipByPackedIndex(int packedIndex, AudioClip[] clips, AudioClip fallback)
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

        private static int GetPackedIndexOfClip(AudioClip clip, AudioClip[] clips, AudioClip fallback)
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
        // VOLUME SETTERS (called by SettingsWindowUI)
        // =================================================================

        public void SetMasterVolume(float v)
        {
            _masterVolume = Mathf.Clamp01(v);
            ApplyMusicVolume();
            ApplyActiveSfxVolumes();
            ApplyLoopVolumes();
            SaveVolumes();
            MusicManager.Instance?.RefreshVolume();
        }

        public void SetGameSoundsVolume(float v)
        {
            _gameSoundsVolume = Mathf.Clamp01(v);
            ApplyActiveSfxVolumes();
            ApplyLoopVolumes();
            SaveVolumes();
        }

        public void SetUIVolume(float v)
        {
            _uiVolume = Mathf.Clamp01(v);
            ApplyActiveSfxVolumes();
            ApplyLoopVolumes();
            SaveVolumes();
        }

        public void SetMusicVolume(float v)
        {
            _musicVolume = Mathf.Clamp01(v);
            ApplyMusicVolume();
            ApplyActiveSfxVolumes();
            ApplyLoopVolumes();
            SaveVolumes();
            MusicManager.Instance?.RefreshVolume();
        }

        // Keep old SFXVolume setter for backward compat — routes to GameSounds
        public void SetSFXVolume(float v) => SetGameSoundsVolume(v);

        public float MasterVolume => _masterVolume;
        public float GameSoundsVolume => _gameSoundsVolume;
        public float UIVolume => _uiVolume;
        public float MusicVolume => _musicVolume;

        // =================================================================
        // PRELOAD / HOT-PATH GATING
        // =================================================================

        public bool CanPlayCategory(SoundCategory category)
        {
            return CanPlay(category);
        }

        public void PreloadClip(AudioClip clip)
        {
            EnsureClipLoaded(clip);
        }

        public void PreloadClips(AudioClip[] clips)
        {
            if (clips == null)
                return;

            for (int i = 0; i < clips.Length; i++)
                EnsureClipLoaded(clips[i]);
        }

        private void EnsureClipLoaded(AudioClip clip)
        {
            if (clip == null)
                return;

            int id = clip.GetInstanceID();
            if (_preparedClipIds.Contains(id))
                return;

            // This moves Unity's native SoundManager.LoadFMODSound work out of the
            // attack frame when TurretSoundConfig/GameSoundPlayer preloads clips.
            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();

            _preparedClipIds.Add(id);
        }

        // =================================================================
        // INTERNALS
        // =================================================================

        private float GetCategoryVolume(SoundCategory cat)
        {
            if (cat == SoundCategory.UI)
                return _uiVolume * _masterVolume;

            if (cat == SoundCategory.Music)
                return _musicVolume * _masterVolume;

            return _gameSoundsVolume * _masterVolume;
        }

        private void ConfigSource(int sourceIndex, AudioSource src, AudioClip clip, Vector3 pos,
            bool is3D, float volumeScale, SoundCategory category, float pitch)
        {
            if (_sfxStates != null && sourceIndex >= 0 && sourceIndex < _sfxStates.Length)
            {
                // FIX: Record EstimatedEndTime so CountPlayingCategory and
                // GetFreeSourceIndex can determine slot occupancy without calling
                // AudioSource.isPlaying (a native bridge call) on every query.
                float duration = (clip != null && clip.length > 0f) ? clip.length : 0.5f;
                _sfxStates[sourceIndex] = new SfxPlaybackState
                {
                    Category        = category,
                    VolumeScale     = volumeScale,
                    EstimatedEndTime = Time.unscaledTime + duration / Mathf.Max(0.01f, pitch)
                };
            }

            src.transform.position = pos;
            src.spatialBlend = is3D ? 1f : 0f;
            src.clip = clip;
            src.volume = GetCategoryVolume(category) * volumeScale;
            src.pitch = pitch;
            src.loop = false;
        }

        private bool CanPlay(SoundCategory cat)
        {
            if (GetCategoryVolume(cat) <= 0.001f)
                return false;

            int frame = Time.frameCount;
            if (_sfxStartFrame != frame)
            {
                _sfxStartFrame = frame;
                _sfxStartsThisFrame = 0;
            }

            if (_maxSfxStartsPerFrame > 0 && _sfxStartsThisFrame >= _maxSfxStartsPerFrame)
                return false;

            if (!_categoryMap.TryGetValue(cat, out var s))
                return true;

            if (s.Cooldown > 0f && (Time.unscaledTime - s.LastPlayTime) < s.Cooldown)
                return false;

            if (s.MaxSimultaneous > 0 && CountPlayingCategory(cat) >= s.MaxSimultaneous)
                return false;

            return true;
        }

        private int CountPlayingCategory(SoundCategory cat)
        {
            if (_sfxStates == null)
                return 0;

            // FIX: Compare EstimatedEndTime (set at play-time) instead of calling
            // src.isPlaying, which crosses the C#-to-native bridge on every call.
            // Pure float arithmetic — no AudioSource access needed.
            int count = 0;
            float now = Time.unscaledTime;
            int length = _sfxStates.Length;
            for (int i = 0; i < length; i++)
            {
                if (_sfxStates[i].Category == cat && _sfxStates[i].EstimatedEndTime > now)
                    count++;
            }
            return count;
        }

        private void MarkPlayed(SoundCategory cat)
        {
            if (_categoryMap.TryGetValue(cat, out var s)) s.LastPlayTime = Time.unscaledTime;
            if (_sfxStartFrame != Time.frameCount)
            {
                _sfxStartFrame = Time.frameCount;
                _sfxStartsThisFrame = 0;
            }
            _sfxStartsThisFrame++;
        }

        private int GetFreeSourceIndex()
        {
            if (_sfxPool == null || _sfxPool.Length == 0)
                return -1;

            // FIX: Use EstimatedEndTime instead of isPlaying to find a free slot.
            // Avoids a native bridge call per pool slot during the linear scan.
            float now = Time.unscaledTime;
            for (int i = 0; i < _sfxPool.Length; i++)
            {
                int idx = (_sfxIndex + i) % _sfxPool.Length;
                if (_sfxStates[idx].EstimatedEndTime <= now)
                {
                    _sfxIndex = (idx + 1) % _sfxPool.Length;
                    return idx;
                }
            }

            // All slots occupied — steal the oldest (least time remaining).
            int stolen = _sfxIndex;
            float leastRemaining = float.MaxValue;
            for (int i = 0; i < _sfxPool.Length; i++)
            {
                float remaining = _sfxStates[i].EstimatedEndTime - now;
                if (remaining < leastRemaining)
                {
                    leastRemaining = remaining;
                    stolen = i;
                }
            }
            _sfxPool[stolen].Stop();
            _sfxStates[stolen].EstimatedEndTime = 0f;
            _sfxIndex = (stolen + 1) % _sfxPool.Length;
            return stolen;
        }

        private void ApplyMusicVolume()
        {
            if (_musicSource != null)
                _musicSource.volume = _musicVolume * _masterVolume;
        }

        private void ApplyActiveSfxVolumes()
        {
            if (_sfxPool == null || _sfxStates == null)
                return;

            int count = Mathf.Min(_sfxPool.Length, _sfxStates.Length);
            for (int i = 0; i < count; i++)
            {
                var src = _sfxPool[i];
                if (src == null || !src.isPlaying)
                    continue;

                var state = _sfxStates[i];
                src.volume = GetCategoryVolume(state.Category) * state.VolumeScale;
            }
        }

        private void ApplyLoopVolumes()
        {
            if (_loopingSources == null || _loopingSources.Count == 0)
                return;

            foreach (var kvp in _loopingSources)
            {
                var state = kvp.Value;
                if (state?.Source == null)
                    continue;

                state.Source.volume = GetCategoryVolume(state.Category) * state.VolumeScale;
            }
        }

        private void SaveVolumes()
        {
            var save = SaveSystem.Load();
            save.MasterVolume = _masterVolume;
            save.GameSoundsVolume = _gameSoundsVolume;
            save.UIVolume = _uiVolume;
            save.MusicVolume = _musicVolume;
            SaveSystem.Save(save);
        }

        private void LoadVolumeSettings()
        {
            var save = SaveSystem.Load();
            _masterVolume = Mathf.Clamp01(save.MasterVolume);
            _gameSoundsVolume = Mathf.Clamp01(save.GameSoundsVolume);
            _uiVolume = Mathf.Clamp01(save.UIVolume);
            _musicVolume = Mathf.Clamp01(save.MusicVolume);
            ApplyMusicVolume();
            ApplyActiveSfxVolumes();
            ApplyLoopVolumes();
        }

        public void StopAllGameSounds()
        {
            if (_sfxPool != null)
            {
                foreach (var src in _sfxPool)
                {
                    if (src != null)
                        src.Stop();
                }
            }

            foreach (var kvp in _loopingSources)
            {
                var state = kvp.Value;
                if (state?.Source != null)
                {
                    state.Source.Stop();
                    state.Source.clip = null;
                }
            }

            _loopingSources.Clear();
        }
    }
}