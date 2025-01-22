using System;
using Project.Core.AudioSystem;
using Zenject;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Composition
{
    public sealed class WorldScaleWhooshSfxDriver : IInitializable, IDisposable
    {
        private readonly WorldScaleController _worldScaleController;
        private readonly IAudioSystem _audioSystem;
        private readonly SoundData _enterMicroSound;
        private readonly SoundData _exitMicroSound;

        public WorldScaleWhooshSfxDriver(
            WorldScaleController worldScaleController,
            IAudioSystem audioSystem,
            SoundData enterMicroSound,
            SoundData exitMicroSound)
        {
            _worldScaleController = worldScaleController;
            _audioSystem = audioSystem;
            _enterMicroSound = enterMicroSound;
            _exitMicroSound = exitMicroSound;
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

        private void HandleEnteringMicroWorld() => _audioSystem.PlaySfx(_enterMicroSound);

        private void HandleEnteringMacroWorld() => _audioSystem.PlaySfx(_exitMicroSound);
    }
}
