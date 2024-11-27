using System;
using UnityEngine;

namespace Project.Core.AudioSystem
{
    [Serializable]
    public sealed class UiSoundsConfig
    {
        [SerializeField] private SoundData _buttonClick;

        public SoundData ButtonClick => _buttonClick;
    }
}
