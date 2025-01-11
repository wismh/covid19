using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Viruses.Macro;

namespace _Project.Mechanics.Composition
{
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
