using UnityEngine;

namespace Project.Core.DamagePopupModule
{
    public interface IDamagePopupSpawner
    {
        void Spawn(Vector3 worldPosition, DamagePopupInfo info);
        void Spawn(Transform target, Vector3 worldPosition, DamagePopupInfo info);
    }
}
