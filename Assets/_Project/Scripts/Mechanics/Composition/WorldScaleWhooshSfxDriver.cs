using System;
using Project.Core.AudioSystem;
using UnityEngine;
using Zenject;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Composition
{
    public sealed class WorldScaleWhooshSfxDriver : IInitializable, IDisposable
    {
        private readonly WorldScaleController _worldScaleController;
        private readonly IAudioSystem _audioSystem;
        private readonly AudioClip _enterMicroClip;
        private readonly AudioClip _exitMicroClip;

        public WorldScaleWhooshSfxDriver(
            WorldScaleController worldScaleController,
            IAudioSystem audioSystem,
            AudioClip enterMicroClip,
            AudioClip exitMicroClip)
        {
            _worldScaleController = worldScaleController;
            _audioSystem = audioSystem;
            _enterMicroClip = enterMicroClip;
            _exitMicroClip = exitMicroClip;
        }

        public void Initialize()
        {
            _worldScaleController.OnEnteringMicroWorld += HandleEnteringMicroWorld;
            _worldScaleController.OnEnteringMacroWorld += HandleEnteringMacroWorld;
        }

        public void Dispose()
        {
            _worldScaleController.OnEnteringMicroWorld -= HandleEnteringMicroWorld;
            _worldScaleController.OnEnteringMacroWorld -= HandleEnteringMacroWorld;
        }

        private void HandleEnteringMicroWorld() => _audioSystem.PlaySfx(_enterMicroClip);

        private void HandleEnteringMacroWorld() => _audioSystem.PlaySfx(_exitMicroClip);
    }
}
