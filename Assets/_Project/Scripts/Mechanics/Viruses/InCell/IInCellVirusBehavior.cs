using UnityEngine;

namespace _Project.Mechanics.Viruses.InCell
{
    /// <summary>
    /// Fresh interface for this phase - deliberately NOT a fork/reuse of the abstract
    /// GMTK.MicroViruses.VirusBehavior base class (Assets/GMTK/Scripts/Behaviors/MicroVirus/Behaviors/
    /// VirusBehavior.cs), same reasoning as IMacroVirusBehavior (no external interop depends on the old type).
    ///
    /// The old abstract class exposed six virtual no-op hooks (Init/Update/OnEnable/FixedUpdate/OnDestroy/
    /// OnDamage), but none of the three concrete behaviors (OrangeVirus/CucumberVirus/SpinerVirus) ever override
    /// OnDestroy or OnDamage - so this interface only declares the four hooks actually used, rather than
    /// reproducing two permanently-unused ones.
    /// </summary>
    public interface IInCellVirusBehavior
    {
        void Init(Transform transform);
        void OnEnable();
        void Update();
        void FixedUpdate();
    }
}
