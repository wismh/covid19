using UnityEngine;

namespace Project.Core.AudioSystem
{
    public interface IAudioSystem
    {
        void PlaySfx(SoundData sound, float volumeScale = 1f);

        void PlaySfx(AudioClip clip, float volume = 1f, Vector2 pitchRange = default);

        LoopingSfxHandle CreateLoopingSfxHandle();

        void ReleaseLoopingSfxHandle(LoopingSfxHandle handle, float? fadeOutDuration = null);

        void UpdateLoopingSfx(
            LoopingSfxHandle handle,
            bool shouldPlay,
            SoundData sound,
            Vector2? pitchRandomRangeOverride = null,
            float? fadeDuration = null);

        void PlayLoopingSfx(LoopingSfxHandle handle, SoundData sound, float? fadeInDuration = null);

        void PlayLoopingSfx(LoopingSfxHandle handle, AudioClip clip, float volume = 1f, float pitch = 1f, float? fadeInDuration = null);

        void PlayLoopingSfx(
            LoopingSfxHandle handle,
            AudioClip clip,
            float volume,
            Vector2 pitchRandomRange,
            float? fadeInDuration = null);

        void StopLoopingSfx(LoopingSfxHandle handle, float? fadeOutDuration = null);

        void StopAllLoopingSfx(float? fadeOutDuration = null);

        bool IsLoopingSfxPlaying(LoopingSfxHandle handle);

        void SetLoopingSfxVolume(LoopingSfxHandle handle, float volume, float? fadeDuration = null);

        void SetLoopingSfxPitch(LoopingSfxHandle handle, float pitch);

        void PlayMusic(SoundData sound, bool loop = true);

        void PlayMusic(SoundData sound, float crossfadeDuration, bool loop = true);

        void CrossfadeMusic(SoundData sound, float duration, bool loop = true);

        void StopMusic(float fadeOutDuration = 0f);

        void SetMusicLoop(bool loop);

        bool IsMusicPlaying { get; }

        SoundData CurrentMusic { get; }

        float MasterVolume { get; }

        float MusicVolume { get; }

        float SfxVolume { get; }

        void SetMasterVolume(float volume);

        void SetMusicVolume(float volume);

        void SetSfxVolume(float volume);
    }
}
