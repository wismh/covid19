using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Viruses.Macro;

namespace _Project.Mechanics.Composition
{
    /// <summary>
    /// Split out of MainSceneComposition.WireCrossSystemEvents (2026-09-20 "MainCompositionRoot split into
    /// installers", following the space-bac/tactics-cards convention of promoting cross-system event glue to
    /// small IInitializable/IDisposable driver services bound via an installer, instead of one MonoBehaviour
    /// doing every subscription by hand - see e.g. tactics-cards' GameplayHapticFeedbackDriver). Bound by
    /// <see cref="GameplayEventWiringInstaller"/>.
    ///
    /// Old: CellController.Die()'s `else StartCoroutine(SpawnVirusesByDelay())` branch, reproduced via
    /// <see cref="CellRegistry.OnCellDied"/> -&gt; <see cref="MacroVirusSpawner.SpawnReplacementVirusesAsync"/>
    /// (see that method's own doc comment for the "Cross-system wiring left to Stage 2" history this fulfills).
    /// </summary>
    public sealed class MacroVirusReplacementDriver : IInitializable, System.IDisposable
    {
        private readonly CellRegistry _cellRegistry;
        private readonly MacroVirusSpawner _macroVirusSpawner;

        public MacroVirusReplacementDriver(CellRegistry cellRegistry, MacroVirusSpawner macroVirusSpawner)
        {
            _cellRegistry = cellRegistry;
            _macroVirusSpawner = macroVirusSpawner;
        }

        public void Initialize()
        {
            _cellRegistry.OnCellDied += HandleCellDied;
        }

        public void Dispose()
        {
            _cellRegistry.OnCellDied -= HandleCellDied;
        }

        private void HandleCellDied(Vector3 position, int virusCountAtDeath)
        {
            _macroVirusSpawner.SpawnReplacementVirusesAsync(position, virusCountAtDeath).Forget();
        }
    }
}
