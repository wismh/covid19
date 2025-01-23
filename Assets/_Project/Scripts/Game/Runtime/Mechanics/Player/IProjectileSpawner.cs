using UnityEngine;

namespace _Project.Mechanics.Player
{
    /// <summary>Seam between PlayerTurret and whatever actually spawns/pools projectiles, so PlayerTurret doesn't
    /// depend on a concrete pooling implementation.</summary>
    public interface IProjectileSpawner
    {
        void SpawnProjectile(Vector3 position, Vector3 direction);
    }
}
