using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Project.Mechanics.Shared
{
    /// <summary>
    /// Phase 7 Stage 1 ("реєстр + спавнери") - generic replacement for GMTK.ObjectManager (Assets/GMTK/Scripts/
    /// Behaviors/ObjectManager.cs), the base class old CellManager/VirusCellManager/VirusManager/MedicalManager/
    /// InCellVirusManager all trivially subclassed with an empty body (`public class CellManager : ObjectManager
    /// { };`) purely to get a distinct MonoBehaviour type ServiceLocator could key registrations on
    /// (REFACTORING_PLAN.md section 5 calls this out by name: "ObjectManager (насправді List&lt;Transform&gt;, не
    /// пул) - Назва вводить в оману").
    ///
    /// Two deliberate changes from the old shape:
    /// 1. Generic composition instead of inheritance-for-a-type-tag. The five old subclasses added no members and
    ///    existed only so ServiceLocator.Get&lt;CellManager&gt;() vs. Get&lt;VirusManager&gt;() could resolve to
    ///    different lists. A generic `EntityRegistry&lt;T&gt;` gives every feature-specific registry
    ///    (CellRegistry/MacroVirusRegistry/MicroVirusRegistry, one per Phase-3 component type) the same
    ///    behavior with zero duplicated code and no empty marker classes - each feature registry composes an
    ///    `EntityRegistry&lt;T&gt;` internally instead of inheriting from a shared MonoBehaviour base.
    /// 2. Stores the typed component (`T`, constrained to `Component` so `.transform` is available for distance
    ///    checks) instead of a bare `Transform`. Old ObjectManager.FindNearToPoint returned a raw Transform, so
    ///    every caller that needed the actual behavior component had to `GetComponent&lt;X&gt;()` on it again
    ///    (e.g. old CellController.Die(): `var nearestCell = _cellManager.FindNearToPoint(player.position); if
    ///    (nearestCell == transform)`). Returning `T` directly removes that redundant lookup at every call site.
    ///
    /// Plain C# (not a MonoBehaviour, not IService/ServiceLocator-registered) per REFACTORING_PLAN.md section 4 -
    /// Mechanics/* gameplay systems are hand-composed (`new CellRegistry(...)`), matching every other Phase 3/4
    /// POCO (WorldScaleController, PlayerStatsService). A future composition root owns the single shared instance
    /// of each feature registry and passes it to whichever spawner/consumer needs it via a `Construct(...)`
    /// method, the same seam Phase 2/3 already established for MacroVirusConfig/ICellTargetProvider etc.
    /// </summary>
    public sealed class EntityRegistry<T> where T : Component
    {
        private readonly List<T> _entities = new();

        public IReadOnlyList<T> Entities => _entities;
        public int Count => _entities.Count;

        /// <summary>Raised after an entity is added. Old: GMTK.ObjectManager had no equivalent "added" event -
        /// AddObject was a silent no-op returning void - added here since feature registries (CellRegistry) need
        /// to react to registration (subscribe to the entity's own death/infection events) the same way old
        /// per-feature code (CellController, wired by hand per-instance) used to.</summary>
        public event Action<T> OnRegistered;

        /// <summary>Old: GMTK.ObjectManager.OnObjectRemoved (`Action`, no payload) - carries the removed entity
        /// here instead, since callers with more than one registered entity need to know which one left.</summary>
        public event Action<T> OnUnregistered;

        /// <summary>Old: GMTK.ObjectManager.AddObject(Transform) - the `if (_objects.Contains(_object)) return;`
        /// duplicate guard is preserved exactly.</summary>
        public void Register(T entity)
        {
            if (entity == null || _entities.Contains(entity))
                return;

            _entities.Add(entity);
            OnRegistered?.Invoke(entity);
        }

        /// <summary>Old: GMTK.ObjectManager.RemoveObject(Transform).</summary>
        public void Unregister(T entity)
        {
            if (entity == null || !_entities.Remove(entity))
                return;

            OnUnregistered?.Invoke(entity);
        }

        /// <summary>Old: GMTK.ObjectManager.FindNearToPoint(Vector3) (single-return overload).</summary>
        public T FindNearest(Vector3 point)
        {
            return FindNearest(point, out _);
        }

        /// <summary>Old: GMTK.ObjectManager.FindNearToPoint(Vector3, out float sqrDistance).</summary>
        public T FindNearest(Vector3 point, out float sqrDistance)
        {
            T nearest = null;
            float minSqrDistance = Mathf.Infinity;

            for (int i = 0; i < _entities.Count; i++)
            {
                if (_entities[i] == null)
                    continue;

                float candidateSqrDistance = (_entities[i].transform.position - point).sqrMagnitude;
                if (candidateSqrDistance < minSqrDistance)
                {
                    minSqrDistance = candidateSqrDistance;
                    nearest = _entities[i];
                }
            }

            sqrDistance = minSqrDistance;
            return nearest;
        }
    }
}
