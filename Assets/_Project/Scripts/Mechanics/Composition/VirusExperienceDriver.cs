using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Macro;
using _Project.Mechanics.Viruses.Micro;

namespace _Project.Mechanics.Composition
{
    /// <summary>
    /// Split out of MainSceneComposition.WireCrossSystemEvents (see MacroVirusReplacementDriver's doc comment for
    /// the "MainCompositionRoot split" context). Bound by <see cref="GameplayEventWiringInstaller"/>.
    ///
    /// Grants XP on every virus kill, macro and micro alike - old VirusController.Damage()/InCellVirus.Damage()'s
    /// death branches both called `_playerStats.AddExperience()` directly.
    ///
    /// Macro: subscribes to the REGISTRY's <see cref="MacroVirusRegistry.OnRegistered"/> (not each spawner's
    /// per-instance OnKilled directly, which this class has no access to until a virus already exists) so every
    /// macro virus - spawned fresh, or as a cell-death replacement wave - gets exactly one XP-on-kill subscription.
    /// Deliberately only OnKilled, not OnPenetratedIntoCell - old code never granted XP for a virus successfully
    /// penetrating a cell, only for killing one. No matching per-virus unsubscribe on death: the subscribed virus
    /// instance is destroyed by MacroVirusSpawner immediately after, so the closure has nothing left to fire on -
    /// only the registry-level OnRegistered subscription itself needs symmetric cleanup (done in Dispose).
    ///
    /// Micro: <see cref="CellSpawner.OnMicroVirusSpawnerCreated"/> fires once per cell, the moment that cell's own
    /// MicroVirusSpawner is constructed (in-cell viruses have no shared registry to subscribe to, each cell owns a
    /// private MicroVirusRegistry) - so this subscribes the XP grant to every cell's spawner exactly once. Same
    /// "no per-instance unsubscribe needed" reasoning as the macro side.
    /// </summary>
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
