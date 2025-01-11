using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Macro;
using _Project.Mechanics.Viruses.Micro;

namespace _Project.Mechanics.Composition
{
    public sealed class VirusExperienceDriver : IInitializable, System.IDisposable
    {
        private readonly MacroVirusRegistry _macroVirusRegistry;
        private readonly CellSpawner _cellSpawner;
        private readonly PlayerStatsService _playerStats;

        public VirusExperienceDriver(MacroVirusRegistry macroVirusRegistry, CellSpawner cellSpawner, PlayerStatsService playerStats)
        {
            _macroVirusRegistry = macroVirusRegistry;
            _cellSpawner = cellSpawner;
            _playerStats = playerStats;
        }

        public void Initialize()
        {
            _macroVirusRegistry.OnRegistered += HandleMacroVirusRegistered;
            _cellSpawner.OnMicroVirusSpawnerCreated += HandleMicroVirusSpawnerCreated;
        }

        public void Dispose()
        {
            _macroVirusRegistry.OnRegistered -= HandleMacroVirusRegistered;
            _cellSpawner.OnMicroVirusSpawnerCreated -= HandleMicroVirusSpawnerCreated;
        }

        // Each spawned virus is destroyed right after firing OnKilled, so this per-instance subscription
        // never needs an explicit unsubscribe.
        private void HandleMacroVirusRegistered(MacroVirusController virus)
        {
            virus.OnKilled += () => _playerStats.AddExperience();
        }

        private void HandleMicroVirusSpawnerCreated(MicroVirusSpawner spawner)
        {
            spawner.OnVirusKilled += () => _playerStats.AddExperience();
        }
    }
}
