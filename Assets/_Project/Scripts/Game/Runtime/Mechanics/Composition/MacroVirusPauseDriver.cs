using Zenject;
using _Project.Mechanics.Viruses.Macro;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Composition
{
    public sealed class MacroVirusPauseDriver : IInitializable, System.IDisposable
    {
        private readonly WorldScaleController _worldScaleController;
        private readonly MacroVirusSpawner _macroVirusSpawner;

        public MacroVirusPauseDriver(WorldScaleController worldScaleController, MacroVirusSpawner macroVirusSpawner)
        {
            _worldScaleController = worldScaleController;
            _macroVirusSpawner = macroVirusSpawner;
        }

        public void Initialize()
        {
            _worldScaleController.OnEnteredMicroWorld += HandlePauseMacroSpawning;
            _worldScaleController.OnExitedMicroWorld += HandleResumeMacroSpawning;
        }

        public void Dispose()
        {
            _worldScaleController.OnEnteredMicroWorld -= HandlePauseMacroSpawning;
            _worldScaleController.OnExitedMicroWorld -= HandleResumeMacroSpawning;
        }

        private void HandlePauseMacroSpawning() => _macroVirusSpawner.SetPaused(true);

        private void HandleResumeMacroSpawning() => _macroVirusSpawner.SetPaused(false);
    }
}
