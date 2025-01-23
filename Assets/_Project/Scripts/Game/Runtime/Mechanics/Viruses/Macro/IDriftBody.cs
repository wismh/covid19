using UnityEngine;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>Movement surface each <see cref="IMacroVirusBehavior"/> drives. <see cref="MacroVirusController"/>
    /// implements this directly rather than composing a separate movement component, since nothing else needs to
    /// drive a macro virus's body.</summary>
    public interface IDriftBody
    {
        void Move(float direction);
        void Rotate(float direction);
        void Impulse(Vector3 impulse);
    }
}
