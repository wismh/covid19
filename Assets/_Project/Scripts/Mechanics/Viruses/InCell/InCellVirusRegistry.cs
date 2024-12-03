using System.Collections.Generic;
using _Project.Mechanics.Shared;

namespace _Project.Mechanics.Viruses.InCell
{
    /// <summary>
    /// Phase 7 Stage 1 - replaces GMTK.MicroViruses.InCellVirusManager (Assets/GMTK/Scripts/Behaviors/MicroVirus/
    /// InCellViruses.cs's `public class InCellVirusManager : ObjectManager { };`). Thin pass-through wrapper
    /// around <see cref="EntityRegistry{T}"/>, same reasoning as
    /// <see cref="_Project.Mechanics.Viruses.Macro.MacroVirusRegistry"/> - see that class's doc comment for why a
    /// named wrapper exists instead of spelling out the generic type everywhere.
    ///
    /// --- Ownership: per-cell, NOT a shared singleton (unlike CellRegistry/MacroVirusRegistry) ---
    /// Old GMTK.MicroViruses.InCellVirusManager was never a ServiceLocator-registered global - each InCellViruses
    /// component (one per Cell prefab instance) did `gameObject.AddComponent&lt;InCellVirusManager&gt;()` itself,
    /// owning a private pool of just that one cell's in-cell viruses. Nothing outside a given cell ever reaches
    /// into another cell's in-cell-virus pool. <see cref="InCellVirusSpawner"/> reproduces that by `new`-ing its
    /// own private instance of this class rather than receiving one via Construct - the one deliberate ownership
    /// difference from MacroVirusRegistry/CellRegistry (both single shared instances a future composition root
    /// constructs and hands out).
    /// </summary>
    public sealed class InCellVirusRegistry
    {
        private readonly EntityRegistry<InCellVirusController> _viruses = new();

        public int Count => _viruses.Count;
        public IReadOnlyList<InCellVirusController> Viruses => _viruses.Entities;

        public void Register(InCellVirusController virus) => _viruses.Register(virus);
        public void Unregister(InCellVirusController virus) => _viruses.Unregister(virus);
    }
}
