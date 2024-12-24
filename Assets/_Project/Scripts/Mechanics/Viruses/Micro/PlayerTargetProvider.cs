using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>
    /// Phase 7 testing fix (2026-09-11): the first (and, per IPlayerTargetProvider's own doc comment, only)
    /// implementation of this interface - CellSpawner.SpawnCell() needed one to Construct() each cell's
    /// MicroVirusSpawner (see that class's doc comment for why no in-cell viruses existed at all before this).
    /// Trivial plain-C# wrapper around a Transform, matching the Mechanics/* manual-composition style (no
    /// MonoBehaviour, no Zenject binding) - a composition root constructs one with the player's transform once
    /// player references are available.
    /// </summary>
    public sealed class PlayerTargetProvider : IPlayerTargetProvider
    {
        public Transform PlayerTransform { get; }

        public PlayerTargetProvider(Transform playerTransform)
        {
            PlayerTransform = playerTransform;
        }
    }
}
