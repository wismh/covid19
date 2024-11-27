using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Project.Core.AudioSystem
{
    internal sealed class SfxAudioSourcePool
    {
        private readonly Stack<AudioSource> _available = new();
        private readonly List<AudioSource> _all = new();

        public SfxAudioSourcePool(Transform parent, AudioMixerGroup outputGroup, int size)
        {
            for (int i = 0; i < size; i++)
            {
                AudioSource source = CreateSource(parent, outputGroup, i);
                _all.Add(source);
                _available.Push(source);
            }
        }

        public bool TryAcquire(out AudioSource source)
        {
            if (_available.Count > 0)
            {
                source = _available.Pop();
                return true;
            }

            for (int i = 0; i < _all.Count; i++)
            {
                AudioSource candidate = _all[i];
                if (!candidate.isPlaying)
                {
                    source = candidate;
                    return true;
                }
            }

            source = null;
            return false;
        }

        public void Release(AudioSource source)
        {
            source.Stop();
            source.clip = null;
            _available.Push(source);
        }

        private static AudioSource CreateSource(Transform parent, AudioMixerGroup outputGroup, int index)
        {
            var sourceObject = new GameObject($"SfxSource_{index}");
            sourceObject.transform.SetParent(parent, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = outputGroup;
            return source;
        }
    }
}
