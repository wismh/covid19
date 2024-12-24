using System;
using System.Collections.Generic;
using UnityEngine;
using _Project.Mechanics.Shared;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>
    /// Phase 7 Stage 1 - replaces GMTK.VirusManager (Assets/GMTK/Scripts/Behaviors/MacroVirus/VirusSpawner.cs's
    /// `public class VirusManager : ObjectManager { };`). Thin pass-through wrapper around
    /// <see cref="EntityRegistry{T}"/> rather than a bare `EntityRegistry&lt;MacroVirusController&gt;` used
    /// directly, for two reasons:
    /// 1. Symmetry with <see cref="_Project.Mechanics.Cells.CellRegistry"/> and
    ///    <see cref="_Project.Mechanics.Viruses.Micro.MicroVirusRegistry"/> - every feature gets one discoverable,
    ///    named registry type in its own namespace rather than generic-type spelled out at every call site.
    /// 2. Room to grow without a breaking API change - e.g. a future balance pass wanting a normal-vs-glist
    ///    breakdown count (old VirusSpawner only ever checked `_virusManager.Pool.Count`, but a future HUD
    ///    "elite virus incoming" indicator would need the split) has a natural home here without CellRegistry-style
    ///    domain logic being needed for it today.
    ///
    /// Unlike CellRegistry (a single shared instance multiple systems reach into), this registry is constructed
    /// and owned entirely by one <see cref="MacroVirusSpawner"/> instance - nothing outside the macro-virus
    /// spawning system needs to enumerate/count live macro viruses today (old code's only other Pool reader,
    /// AdviceDialog/CellManager, reads the CELL pool, not the virus one). See MacroVirusSpawner's doc comment for
    /// why it still receives this via <see cref="_Project.Mechanics.Viruses.Macro.MacroVirusSpawner.Construct"/>
    /// rather than `new`-ing it privately in Awake().
    /// </summary>
    public sealed class MacroVirusRegistry
    {
        private readonly EntityRegistry<MacroVirusController> _viruses = new();

        public int Count => _viruses.Count;
        public IReadOnlyList<MacroVirusController> Viruses => _viruses.Entities;

        public event Action<MacroVirusController> OnRegistered
        {
            add => _viruses.OnRegistered += value;
            remove => _viruses.OnRegistered -= value;
        }

        public event Action<MacroVirusController> OnUnregistered
        {
            add => _viruses.OnUnregistered += value;
            remove => _viruses.OnUnregistered -= value;
        }

        public void Register(MacroVirusController virus) => _viruses.Register(virus);
        public void Unregister(MacroVirusController virus) => _viruses.Unregister(virus);

        public MacroVirusController FindNearestVirus(Vector3 point) => _viruses.FindNearest(point);
        public MacroVirusController FindNearestVirus(Vector3 point, out float sqrDistance) => _viruses.FindNearest(point, out sqrDistance);
        public UnityEngine.Transform FindNearestVirusTransform(Vector3 point, out float sqrDistance)
        {
            var virus = _viruses.FindNearest(point, out sqrDistance);
            return virus != null ? virus.transform : null;
        }
    }
}
