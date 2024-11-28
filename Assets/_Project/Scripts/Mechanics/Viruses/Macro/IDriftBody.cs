using UnityEngine;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>
    /// Movement surface each <see cref="IMacroVirusBehavior"/> drives. Fresh port of the Move/Rotate/Impulse
    /// trio from GMTK.DriftMovableObject (Assets/GMTK/Scripts/Behaviors/DriftMovableObject.cs) - same approach
    /// _Project.Mechanics.Player.PlayerMovement took in Phase 2 (re-implementing the primitive rather than
    /// reusing the old MonoBehaviour). <see cref="MacroVirusController"/> implements this interface directly
    /// instead of composing a separate movement component: unlike Player (where PlayerHealth also needed to
    /// drive PlayerMovement for on-hit knockback, justifying a standalone component), nothing else needs to
    /// drive a macro virus's body, so the interface exists only to give the three behavior strategies a
    /// constructor-injectable seam instead of an implicit `GetComponent&lt;DriftMovableObject&gt;()` call.
    /// </summary>
    public interface IDriftBody
    {
        /// <summary>Old: DriftMovableObject.Move(float).</summary>
        void Move(float direction);

        /// <summary>Old: DriftMovableObject.Rotate(float).</summary>
        void Rotate(float direction);

        /// <summary>Old: DriftMovableObject.Impulse(Vector3).</summary>
        void Impulse(Vector3 impulse);
    }
}
