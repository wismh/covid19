using UnityEngine;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>
    /// Fresh interface for this phase - deliberately NOT a fork/reuse of GMTK.VirusBehaviors.IBehavior
    /// (Assets/GMTK/Scripts/Behaviors/MacroVirus/Behaviors/Behavior.cs). Unlike GMTK.IDamageable (which old
    /// Bullet.cs interop-checks via `TryGetComponent&lt;IDamageable&gt;()`, so Phase 2's PlayerHealth correctly
    /// kept reusing it), nothing outside the old macro-virus AI depends on GMTK.IBehavior via
    /// reflection/interop - it's purely an internal strategy-pattern seam, so there's no interop reason to keep
    /// forking it. Shape mirrors the old interface exactly (same two members), since that shape itself is a good
    /// pattern per REFACTORING_PLAN.md section 5 ("IBehavior/VirusBehavior... Добрий патерн - залишити
    /// концептуально. Перенести стратегії, ін'єктувати залежності через конструктор").
    /// </summary>
    public interface IMacroVirusBehavior
    {
        void Init(Transform transform);
        void FixedUpdate();
    }
}
