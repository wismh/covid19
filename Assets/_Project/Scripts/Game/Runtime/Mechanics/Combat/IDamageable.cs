using UnityEngine;

namespace _Project.Mechanics.Combat
{
    public interface IDamageable
    {
        void Damage(int damage, Vector3 attackDirection);
    }
}
