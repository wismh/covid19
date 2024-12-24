using System;
using UnityEngine;

namespace _Project.Mechanics.Cells
{
    /// <summary>
    /// Tracks a cell's in-cell-virus count only - deliberately nothing else. Ports the counting half of
    /// GMTK.CellController.OnAddVirus()/OnDestroyVirus() (Assets/GMTK/Scripts/Behaviors/Cell/CellController.cs):
    ///
    ///   old OnAddVirus:    `if (_virusCount == 0) { _gameStatistics.CellSick(); _virusCellManager.AddObject(transform); } _virusCount += 1;`
    ///   old OnDestroyVirus: `_virusCount -= 1; if (_virusCount == 0) { _health = 100; _virusCellManager.RemoveObject(transform); _gameStatistics.CellRecovered(); }`
    ///
    /// The old methods conflated three separate concerns in one place: (1) the count itself, (2) a health-reset
    /// side effect on cure, and (3) registering/deregistering the cell with GMTK.VirusCellManager and
    /// GMTK.GameStatistics for the "sick cells" list / stats HUD. REFACTORING_PLAN.md section 3.4 calls out
    /// exactly this kind of monolith as the thing to split.
    ///
    /// This class keeps only concern (1) and raises events for (2)/(3) to react to:
    /// - <see cref="OnInfectionStarted"/> (0 -> 1 transition) and <see cref="OnInfectionCleared"/> (1 -> 0)
    ///   mirror the two `if (_virusCount == 0)` branches above, without deciding what happens as a result.
    /// - <see cref="CellHealth"/> is the composing class for concern (2): it holds a direct [SerializeField]
    ///   reference to this component (matching the PlayerHealth/PlayerMovement sibling-reference pattern from
    ///   Phase 2, rather than inventing a new orchestrator class) and subscribes to OnInfectionCleared to reset
    ///   health back to max.
    /// - Concern (3) - GMTK.VirusCellManager registration and GMTK.GameStatistics sick/recovered counters - is
    ///   deliberately NOT reproduced here. Phase 3 is scoped to pure C# + config assets with no scene/prefab
    ///   wiring, and a "living/sick cells" registry class does not exist yet in the new architecture (only the
    ///   shared placement utility is in scope per REFACTORING_PLAN.md Phase 3 item 5, not a CellManager port).
    ///   A future composition root subscribes to OnInfectionStarted/OnInfectionCleared to update whatever
    ///   registry/stats system exists by then - the same seam pattern as MacroVirusController.OnKilled /
    ///   MicroVirusController.OnKilled deferring XP-granting to a composition root instead of reaching into
    ///   GMTK.Services.PlayerStats directly.
    /// </summary>
    public sealed class CellInfectionState : MonoBehaviour
    {
        private int _virusCount;

        public int VirusCount => _virusCount;
        public bool IsInfected => _virusCount > 0;

        /// <summary>Raised on the 0 -> 1 transition only (old: the `if (_virusCount == 0)` branch in OnAddVirus).</summary>
        public event Action OnInfectionStarted;

        /// <summary>Raised on the 1 -> 0 transition only (old: the `if (_virusCount == 0)` branch in OnDestroyVirus).</summary>
        public event Action OnInfectionCleared;

        /// <summary>Raised on every add/remove with the new count - a generic hook for anything that wants the
        /// live count without caring about the start/clear edges specifically (e.g. a future in-cell-virus spawn
        /// count, mirroring old CellController.SetMicroSprite() -> `_inCellVirusManager.SetVirusesCount(_virusCount)`,
        /// which is itself out of scope for this phase - see CellVisuals doc comment).</summary>
        public event Action<int> OnVirusCountChanged;

        /// <summary>Old: CellController.OnAddVirus(), called by PenetrationIntoCellBehavior when a macro virus
        /// finishes penetrating a cell (see PenetrationIntoCellBehavior.OnAnimationEnd).</summary>
        public void AddVirus()
        {
            _virusCount += 1;

            if (_virusCount == 1)
                OnInfectionStarted?.Invoke();

            OnVirusCountChanged?.Invoke(_virusCount);
        }

        /// <summary>
        /// Old: CellController.OnDestroyVirus(), called when an in-cell virus dies (old code wired this via
        /// `_inCellVirusManager.GetComponent&lt;InCellVirusManager&gt;().OnObjectRemoved += OnDestroyVirus;` in
        /// CellController.Start() - a future composition root wires MicroVirusController.OnKilled to this method
        /// the same way once a per-cell virus registry exists).
        ///
        /// Clamped at 0 as a defensive guard the old code didn't have (`_virusCount -= 1` could theoretically go
        /// negative if called more times than AddVirus - never happened in practice since calls were 1:1 paired
        /// through the manager's remove event, but the guard costs nothing and removes the possibility here).
        /// </summary>
        public void RemoveVirus()
        {
            _virusCount = Mathf.Max(0, _virusCount - 1);

            if (_virusCount == 0)
                OnInfectionCleared?.Invoke();

            OnVirusCountChanged?.Invoke(_virusCount);
        }
    }
}
