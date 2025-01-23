using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    public interface IMicroVirusBehavior
    {
        void Init(Transform transform);
        void OnEnable();
        void Update();
        void FixedUpdate();
    }
}
