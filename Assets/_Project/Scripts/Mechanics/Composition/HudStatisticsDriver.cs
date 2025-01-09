using Zenject;
using _Project.Mechanics.Cells;
using _Project.UI.Hud;

namespace _Project.Mechanics.Composition
{
    /// <summary>
    /// Split out of MainSceneComposition.WireCrossSystemEvents (see MacroVirusReplacementDriver's doc comment for
    /// the "MainCompositionRoot split" context). Bound by <see cref="GameplayEventWiringInstaller"/>.
    ///
    /// <see cref="CellRegistry.OnStatisticsChanged"/> is built to match <see cref="HudScreen.SetStatistics"/>'s
    /// parameter list byte-for-byte (see CellRegistry's own "Statistics event shape" doc comment) specifically so
    /// this wiring needs no adapter.
    /// </summary>
    public sealed class HudStatisticsDriver : IInitializable, System.IDisposable
    {
        private readonly CellRegistry _cellRegistry;
        private readonly HudScreen _hudScreen;

        public HudStatisticsDriver(CellRegistry cellRegistry, HudScreen hudScreen)
        {
            _cellRegistry = cellRegistry;
            _hudScreen = hudScreen;
        }

        public void Initialize()
        {
            _cellRegistry.OnStatisticsChanged += _hudScreen.SetStatistics;
        }

        public void Dispose()
        {
            _cellRegistry.OnStatisticsChanged -= _hudScreen.SetStatistics;
        }
    }
}
