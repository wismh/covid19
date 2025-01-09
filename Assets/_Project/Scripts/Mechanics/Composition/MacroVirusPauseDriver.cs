using Zenject;
using _Project.Mechanics.Viruses.Macro;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Composition
{
    /// <summary>
    /// Split out of MainSceneComposition.WireCrossSystemEvents (see MacroVirusReplacementDriver's doc comment for
    /// the "MainCompositionRoot split" context). Bound by <see cref="GameplayEventWiringInstaller"/>.
    ///
    /// Old: `_virusManager.gameObject.SetActive(false/true)` from GamePlayState.State's setter - see
    /// <see cref="MacroVirusSpawner.SetPaused"/>'s own doc comment for the full history of what this pause
    /// actually needs to do (GameObject toggle + DOTween pause on already-penetrating viruses).
    /// </summary>
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
