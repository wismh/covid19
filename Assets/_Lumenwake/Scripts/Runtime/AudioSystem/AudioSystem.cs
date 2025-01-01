using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Lumenwake;
using UnityEngine;
using UnityEngine.Audio;

namespace Project.Core.AudioSystem
{
    [DisallowMultipleComponent]
    public sealed class AudioSystem : MonoBehaviour, IAudioSystem
    {
        private const float MinLinearVolume = 0.0001f;
        private const float SilentDecibels = -80f;

        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private AudioMixerGroup _sfxGroup;
        [SerializeField] private AudioMixerGroup _musicGroup;
        [SerializeField] private AudioMixerVolumeParameters _volumeParameters = new();
        [SerializeField] private int _sfxPoolSize = 12;
        [SerializeField] private float _defaultMusicCrossfade = 1f;
        [SerializeField] private float _defaultLoopingSfxFade = 0.1f;

        private readonly AudioSource[] _musicSources = new AudioSource[2];

        private SfxAudioSourcePool _sfxPool;
        private LoopingSfxRegistry _loopingSfx;
        private Transform _sfxRoot;
        private Transform _loopingSfxRoot;
        private Transform _musicRoot;
        private int _activeMusicIndex;
        private SoundData _currentMusic;
        private float _masterVolume = 1f;
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;
        private CancellationTokenSource _lifetimeCts;
        private bool _isInitialized;

        public bool IsMusicPlaying =>
            _isInitialized && _musicSources[_activeMusicIndex].isPlaying;
        public SoundData CurrentMusic => _currentMusic;
        public float MasterVolume => _masterVolume;
        public float MusicVolume => _musicVolume;
        public float SfxVolume => _sfxVolume;

        private void Awake() =>
            EnsureInitialized();

        private void LateUpdate()
        {
            if (!_isInitialized || _loopingSfx == null)
                return;

            foreach (LoopingSfxChannel channel in _loopingSfx.AllChannels)
                TickLoopingPitchRandomization(channel);
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            _lifetimeCts = new CancellationTokenSource();
            _sfxRoot = CreateChildRoot("SfxPool");
            _loopingSfxRoot = CreateChildRoot("LoopingSfx");
            _musicRoot = CreateChildRoot("Music");
            _sfxPool = new SfxAudioSourcePool(_sfxRoot, _sfxGroup, _sfxPoolSize);
            _loopingSfx = new LoopingSfxRegistry(_loopingSfxRoot, _sfxGroup);

            for (int i = 0; i < _musicSources.Length; i++)
                _musicSources[i] = CreateMusicSource(i);

            ApplyMixerVolume(_volumeParameters.MasterVolume, _masterVolume);
            ApplyMixerVolume(_volumeParameters.MusicVolume, _musicVolume);
            ApplyMixerVolume(_volumeParameters.SfxVolume, _sfxVolume);
        }

        private void OnDestroy()
        {
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();

            for (int i = 0; i < _musicSources.Length; i++)
                DOTween.Kill(_musicSources[i]);

            if (_loopingSfx != null)
            {
                foreach (LoopingSfxChannel channel in _loopingSfx.AllChannels)
                    DOTween.Kill(channel.Source);

                _loopingSfx.DisposeAll();
            }

            DOTween.Kill(this);
        }

        public void PlaySfx(SoundData sound, float volumeScale = 1f)
        {
            EnsureInitialized();

            if (sound == null || sound.Clip == null)
                return;

            PlaySfxInternal(sound.Clip, sound.Volume * volumeScale, sound.PitchRange);
        }

        public void PlaySfx(AudioClip clip, float volume = 1f, Vector2 pitchRange = default)
        {
            EnsureInitialized();

            if (clip == null)
                return;

            Vector2 resolvedPitchRange = pitchRange == default ? Vector2.one : pitchRange;
            PlaySfxInternal(clip, volume, resolvedPitchRange);
        }

        public LoopingSfxHandle CreateLoopingSfxHandle()
        {
            EnsureInitialized();
            return _loopingSfx.Allocate();
        }

        public void ReleaseLoopingSfxHandle(LoopingSfxHandle handle, float? fadeOutDuration = null)
        {
            EnsureInitialized();

            if (!_loopingSfx.TryGet(handle, out LoopingSfxChannel channel))
                return;

            float fade = ResolveLoopingFadeOut(fadeOutDuration);

            if (fade <= 0f)
            {
                StopLoopingSource(channel, 0f);
                _loopingSfx.Remove(handle);
                return;
            }

            StopLoopingSource(channel, fade, () => _loopingSfx.Remove(handle));
        }

        public void UpdateLoopingSfx(
            LoopingSfxHandle handle,
            bool shouldPlay,
            SoundData sound,
            Vector2? pitchRandomRangeOverride = null,
            float? fadeDuration = null)
        {
            EnsureInitialized();

            if (!handle.IsValid)
                return;

            if (!shouldPlay)
            {
                StopLoopingSfx(handle, fadeDuration);
                return;
            }

            if (sound == null || sound.Clip == null)
                return;

            if (!_loopingSfx.TryGet(handle, out _))
                return;

            Vector2 range = pitchRandomRangeOverride ?? sound.PitchRange;
            ConfigureAndPlayLooping(handle, sound.Clip, sound.Volume, range, randomizePitchEachLoop: true, fadeDuration);
        }

        public void PlayLoopingSfx(LoopingSfxHandle handle, SoundData sound, float? fadeInDuration = null)
        {
            if (sound == null || sound.Clip == null || !handle.IsValid)
                return;

            UpdateLoopingSfx(handle, true, sound, fadeDuration: fadeInDuration);
        }

        public void PlayLoopingSfx(LoopingSfxHandle handle, AudioClip clip, float volume = 1f, float pitch = 1f, float? fadeInDuration = null)
        {
            EnsureInitialized();

            if (clip == null || !handle.IsValid)
                return;

            if (!_loopingSfx.TryGet(handle, out LoopingSfxChannel channel))
                return;

            float fadeIn = ResolveLoopingFadeIn(fadeInDuration);
            AudioSource source = channel.Source;

            channel.TargetVolume = volume;
            channel.RandomizePitchEachLoop = false;
            channel.ManualPitchOverride = false;
            channel.PitchRandomRange = Vector2.one;

            bool sameClipPlaying = source.isPlaying && source.clip == clip;

            if (sameClipPlaying && Mathf.Approximately(source.pitch, pitch))
            {
                ApplyLoopingVolume(source, volume, fadeIn);
                return;
            }

            DOTween.Kill(source);

            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.loop = true;
            channel.ResetLoopTimeTracking();
            source.volume = fadeIn > 0f ? 0f : volume;
            source.Play();
            channel.LastPlaybackTime = source.time;

            ApplyLoopingVolume(source, volume, fadeIn);
        }

        public void PlayLoopingSfx(
            LoopingSfxHandle handle,
            AudioClip clip,
            float volume,
            Vector2 pitchRandomRange,
            float? fadeInDuration = null)
        {
            EnsureInitialized();

            if (clip == null || !handle.IsValid)
                return;

            if (!_loopingSfx.TryGet(handle, out _))
                return;

            ConfigureAndPlayLooping(handle, clip, volume, pitchRandomRange, randomizePitchEachLoop: true, fadeInDuration);
        }

        public void StopLoopingSfx(LoopingSfxHandle handle, float? fadeOutDuration = null)
        {
            EnsureInitialized();

            if (!_loopingSfx.TryGet(handle, out LoopingSfxChannel channel))
                return;

            StopLoopingSource(channel, ResolveLoopingFadeOut(fadeOutDuration));
        }

        public void StopAllLoopingSfx(float? fadeOutDuration = null)
        {
            EnsureInitialized();

            float fadeOut = ResolveLoopingFadeOut(fadeOutDuration);

            foreach (LoopingSfxChannel channel in _loopingSfx.AllChannels)
                StopLoopingSource(channel, fadeOut);
        }

        public bool IsLoopingSfxPlaying(LoopingSfxHandle handle) =>
            handle.IsValid &&
            _isInitialized &&
            _loopingSfx != null &&
            _loopingSfx.TryGet(handle, out LoopingSfxChannel channel) &&
            channel.Source.isPlaying;

        public void SetLoopingSfxVolume(LoopingSfxHandle handle, float volume, float? fadeDuration = null)
        {
            EnsureInitialized();

            if (!_loopingSfx.TryGet(handle, out LoopingSfxChannel channel))
                return;

            channel.TargetVolume = volume;
            ApplyLoopingVolume(channel.Source, volume, fadeDuration ?? 0f);
        }

        public void SetLoopingSfxPitch(LoopingSfxHandle handle, float pitch)
        {
            EnsureInitialized();

            if (!_loopingSfx.TryGet(handle, out LoopingSfxChannel channel))
                return;

            channel.ManualPitchOverride = true;
            channel.RandomizePitchEachLoop = false;
            channel.Source.pitch = pitch;
        }

        public void PlayMusic(SoundData sound, bool loop = true) =>
            PlayMusic(sound, _defaultMusicCrossfade, loop);

        public void PlayMusic(SoundData sound, float crossfadeDuration, bool loop = true)
        {
            EnsureInitialized();

            if (sound == null || sound.Clip == null)
            {
                return;
            }

            if (!IsMusicPlaying || crossfadeDuration <= 0f)
            {
                // Nothing to crossfade FROM - fade up from silence instead, using the same duration, rather
                // than discarding it and snapping straight to full volume.
                PlayMusicImmediate(sound, loop, crossfadeDuration);
                return;
            }

            CrossfadeMusic(sound, crossfadeDuration, loop);
        }

        public void CrossfadeMusic(SoundData sound, float duration, bool loop = true)
        {
            EnsureInitialized();

            if (sound == null || sound.Clip == null)
                return;

            if (duration <= 0f || !IsMusicPlaying)
            {
                PlayMusicImmediate(sound, loop, duration);
                return;
            }

            int nextIndex = 1 - _activeMusicIndex;
            AudioSource current = _musicSources[_activeMusicIndex];
            AudioSource next = _musicSources[nextIndex];
            float targetVolume = sound.Volume;

            DOTween.Kill(current);
            DOTween.Kill(next);

            ConfigureMusicSource(next, sound, loop);
            next.volume = 0f;
            next.Play();

            current.DOFade(0f, duration).SetUpdate(true).OnComplete(() => current.Stop());
            next.DOFade(targetVolume, duration).SetUpdate(true);

            _activeMusicIndex = nextIndex;
            _currentMusic = sound;
        }

        public void StopMusic(float fadeOutDuration = 0f)
        {
            EnsureInitialized();

            AudioSource active = _musicSources[_activeMusicIndex];
            DOTween.Kill(active);

            if (fadeOutDuration <= 0f)
            {
                active.Stop();
                _currentMusic = null;
                return;
            }

            active
                .DOFade(0f, fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    active.Stop();
                    _currentMusic = null;
                });
        }

        public void SetMusicLoop(bool loop)
        {
            EnsureInitialized();
            _musicSources[_activeMusicIndex].loop = loop;
        }

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
            ApplyMixerVolume(_volumeParameters.MasterVolume, _masterVolume);
        }

        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
            ApplyMixerVolume(_volumeParameters.MusicVolume, _musicVolume);
        }

        public void SetSfxVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
            ApplyMixerVolume(_volumeParameters.SfxVolume, _sfxVolume);
        }

        private float ResolveLoopingFadeIn(float? fadeDuration) =>
            fadeDuration ?? _defaultLoopingSfxFade;

        private float ResolveLoopingFadeOut(float? fadeDuration) =>
            fadeDuration ?? _defaultLoopingSfxFade;

        private void ConfigureAndPlayLooping(
            LoopingSfxHandle handle,
            AudioClip clip,
            float volume,
            Vector2 pitchRandomRange,
            bool randomizePitchEachLoop,
            float? fadeInDuration)
        {
            if (!_loopingSfx.TryGet(handle, out LoopingSfxChannel channel))
                return;

            float fadeIn = ResolveLoopingFadeIn(fadeInDuration);
            AudioSource source = channel.Source;

            channel.TargetVolume = volume;
            channel.PitchRandomRange = pitchRandomRange;
            channel.RandomizePitchEachLoop = randomizePitchEachLoop;
            channel.ManualPitchOverride = false;

            bool sameClipPlaying = source.isPlaying && source.clip == clip;

            if (sameClipPlaying)
            {
                channel.PitchRandomRange = pitchRandomRange;
                channel.RandomizePitchEachLoop = randomizePitchEachLoop;
                channel.ManualPitchOverride = false;
                ApplyLoopingVolume(source, volume, fadeIn);
                return;
            }

            DOTween.Kill(source);

            source.Stop();
            source.clip = clip;
            source.loop = true;
            channel.ApplyRandomPitchFromRange();
            channel.ResetLoopTimeTracking();
            source.volume = fadeIn > 0f ? 0f : volume;
            source.Play();
            channel.LastPlaybackTime = source.time;

            ApplyLoopingVolume(source, volume, fadeIn);
        }

        private static void TickLoopingPitchRandomization(LoopingSfxChannel channel)
        {
            if (channel.ManualPitchOverride || !channel.RandomizePitchEachLoop)
                return;

            AudioSource source = channel.Source;

            if (!source.isPlaying || source.clip == null)
            {
                channel.LastPlaybackTime = -1f;
                return;
            }

            float t = source.time;

            if (channel.LastPlaybackTime >= 0f && t < channel.LastPlaybackTime - 0.02f)
                channel.ApplyRandomPitchFromRange();

            channel.LastPlaybackTime = t;
        }

        private void StopLoopingSource(LoopingSfxChannel channel, float fadeOutDuration, System.Action onFullyStopped = null)
        {
            AudioSource source = channel.Source;
            DOTween.Kill(source);

            void FinishStop()
            {
                source.Stop();
                channel.ResetLoopTimeTracking();
                channel.RandomizePitchEachLoop = false;
                channel.ManualPitchOverride = false;
                onFullyStopped?.Invoke();
            }

            if (!source.isPlaying)
            {
                FinishStop();
                return;
            }

            if (fadeOutDuration <= 0f)
            {
                FinishStop();
                return;
            }

            source
                .DOFade(0f, fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(FinishStop);
        }

        private static void ApplyLoopingVolume(AudioSource source, float volume, float fadeDuration)
        {
            if (fadeDuration <= 0f)
            {
                source.volume = volume;
                return;
            }

            source.DOFade(volume, fadeDuration).SetUpdate(true);
        }

        private void PlaySfxInternal(AudioClip clip, float volume, Vector2 pitchRange)
        {
            if (!_sfxPool.TryAcquire(out AudioSource source))
            {
                LoggingSystem.LogWarning("SFX pool exhausted; skipping playback.", this);
                return;
            }

            source.clip = clip;
            source.volume = volume;
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.loop = false;
            source.Play();
            ReturnSfxSourceWhenFinished(source).Forget();
        }

        private void PlayMusicImmediate(SoundData sound, bool loop, float fadeInDuration = 0f)
        {
            AudioSource active = _musicSources[_activeMusicIndex];
            AudioSource inactive = _musicSources[1 - _activeMusicIndex];

            DOTween.Kill(active);
            DOTween.Kill(inactive);

            inactive.Stop();
            ConfigureMusicSource(active, sound, loop);
            active.Play();
            _currentMusic = sound;

            if (fadeInDuration > 0f)
            {
                active.volume = 0f;
                active.DOFade(sound.Volume, fadeInDuration).SetUpdate(true);
            }
            else
            {
                active.volume = sound.Volume;
            }
        }

        private void ConfigureMusicSource(AudioSource source, SoundData sound, bool loop)
        {
            source.clip = sound.Clip;
            source.loop = loop;
            source.pitch = Random.Range(sound.PitchRange.x, sound.PitchRange.y);
        }

        private AudioSource CreateMusicSource(int index)
        {
            var sourceObject = new GameObject($"MusicSource_{index}");
            sourceObject.transform.SetParent(_musicRoot, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = _musicGroup;
            return source;
        }

        private Transform CreateChildRoot(string name)
        {
            var rootObject = new GameObject(name);
            rootObject.transform.SetParent(transform, false);
            return rootObject.transform;
        }

        private void ApplyMixerVolume(string parameterName, float linearVolume)
        {
            if (string.IsNullOrEmpty(parameterName))
                return;

            _mixer.SetFloat(parameterName, LinearToDecibels(linearVolume));
        }

        private static float LinearToDecibels(float linearVolume) =>
            linearVolume > MinLinearVolume
                ? Mathf.Log10(linearVolume) * 20f
                : SilentDecibels;

        private async UniTaskVoid ReturnSfxSourceWhenFinished(AudioSource source)
        {
            CancellationToken token = _lifetimeCts.Token;

            try
            {
                await UniTask.WaitWhile(() => source.isPlaying, cancellationToken: token);
                _sfxPool.Release(source);
            }
            catch (System.OperationCanceledException)
            {
            }
        }
    }
}
