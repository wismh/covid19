using UnityEngine;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>Constructor-injected seam for finding the nearest cell, decoupling MoveToCellBehavior from a
    /// concrete cell registry.</summary>
    public interface ICellTargetProvider
    {
        Transform FindNearestCell(Vector3 point);
    }
}
