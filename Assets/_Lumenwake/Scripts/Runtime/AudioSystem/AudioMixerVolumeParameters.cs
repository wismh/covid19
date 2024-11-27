using System;
using UnityEngine;

namespace Project.Core.AudioSystem
{
    [Serializable]
    public sealed class AudioMixerVolumeParameters
    {
        [SerializeField] private string _masterVolume = "MasterVolume";
        [SerializeField] private string _musicVolume = "MusicVolume";
        [SerializeField] private string _sfxVolume = "SfxVolume";

        public string MasterVolume => _masterVolume;

        public string MusicVolume => _musicVolume;

        public string SfxVolume => _sfxVolume;
    }
}
