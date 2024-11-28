using UnityEngine;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>
    /// Replaces `ServiceLocator.Instance.Get&lt;CellManager&gt;().FindNearToPoint(...)`
    /// (old GMTK.VirusBehaviors.MoveToCell.Init/FixedUpdate, Assets/GMTK/Scripts/Behaviors/MacroVirus/Behaviors/
    /// MoveToCell.cs) with a plain constructor-injected seam instead of a service-locator lookup.
    ///
    /// Deliberately NOT a dependency on a new CellManager/registry class: REFACTORING_PLAN.md's Phase 3 file
    /// list only asks for the shared rejection-sampling placement utility (item 5), not a living-cells registry
    /// port - building one is out of scope here. Whoever composes the gameplay scene later supplies an
    /// implementation once such a registry exists (old GMTK.CellManager during any transition period, or a new
    /// one), the same way IProjectileSpawner let Phase 2's PlayerTurret stay decoupled from the concrete bullet
    /// pool.
    /// </summary>
    public interface ICellTargetProvider
    {
        Transform FindNearestCell(Vector3 point);
    }
}
