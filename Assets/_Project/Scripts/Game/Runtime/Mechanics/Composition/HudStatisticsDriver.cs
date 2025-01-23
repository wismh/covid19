using Zenject;
using _Project.Mechanics.Cells;
using _Project.UI.Hud;

namespace _Project.Mechanics.Composition
{
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
