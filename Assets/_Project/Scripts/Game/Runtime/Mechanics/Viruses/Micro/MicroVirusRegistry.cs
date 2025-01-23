using System.Collections.Generic;
using _Project.Mechanics.Shared;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>Thin pass-through wrapper around <see cref="EntityRegistry{T}"/>. Owned per-cell, not a shared
    /// singleton: <see cref="MicroVirusSpawner"/> `new`s its own private instance, since nothing outside a given
    /// cell ever reaches into another cell's in-cell-virus pool.</summary>
    public sealed class MicroVirusRegistry
    {
        private readonly EntityRegistry<MicroVirusController> _viruses = new();

        public int Count => _viruses.Count;
        public IReadOnlyList<MicroVirusController> Viruses => _viruses.Entities;

        public void Register(MicroVirusController virus) => _viruses.Register(virus);
        public void Unregister(MicroVirusController virus) => _viruses.Unregister(virus);
    }
}
