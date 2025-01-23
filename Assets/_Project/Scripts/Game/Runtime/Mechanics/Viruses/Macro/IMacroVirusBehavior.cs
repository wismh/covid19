using UnityEngine;

namespace _Project.Mechanics.Viruses.Macro
{
    public interface IMacroVirusBehavior
    {
        void Init(Transform transform);
        void FixedUpdate();
    }
}
