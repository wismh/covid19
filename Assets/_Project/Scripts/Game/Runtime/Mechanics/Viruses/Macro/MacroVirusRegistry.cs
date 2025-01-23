using System;
using System.Collections.Generic;
using UnityEngine;
using _Project.Mechanics.Shared;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>Thin pass-through wrapper around <see cref="EntityRegistry{T}"/>, owned entirely by one
    /// <see cref="MacroVirusSpawner"/> instance - nothing outside the macro-virus spawning system needs to
    /// enumerate/count live macro viruses today.</summary>
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
