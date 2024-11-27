using UnityEngine;
using UnityEngine;
using UnityEngine.Audio;

namespace Project.Core.AudioSystem
{
    internal sealed class LoopingSfxChannel
    {
        public LoopingSfxChannel(Transform parent, AudioMixerGroup outputGroup, int slotId)
        {
            var sourceObject = new GameObject($"LoopingSfx_{slotId}");
            sourceObject.transform.SetParent(parent, false);

            Source = sourceObject.AddComponent<AudioSource>();
            Source.playOnAwake = false;
            Source.loop = true;
            Source.outputAudioMixerGroup = outputGroup;
        }

        public AudioSource Source { get; }

        public float TargetVolume { get; set; }

        public Vector2 PitchRandomRange { get; set; } = Vector2.one;

        public bool RandomizePitchEachLoop { get; set; }

        public bool ManualPitchOverride { get; set; }

        public float LastPlaybackTime { get; set; } = -1f;

        public void ApplyRandomPitchFromRange() =>
            Source.pitch = Random.Range(PitchRandomRange.x, PitchRandomRange.y);

        public void ResetLoopTimeTracking() =>
            LastPlaybackTime = -1f;

        public void DisposeSource()
        {
            if (Source != null)
                Object.Destroy(Source.gameObject);
        }
    }
}
