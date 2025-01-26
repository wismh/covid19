using UnityEngine;
using _Project.Mechanics.Combat;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>Seam between CucumberVirusBehavior and whatever actually spawns the bullet.</summary>
    public interface IVirusBulletSpawner
    {
        /// <param name="scaleReference">The bullet is parented under this transform, which is what gives it the
        /// correct (shrunk) scale inside a cell's micro-virus hierarchy - it must not be spawned unparented.
        /// </param>
        /// <returns>The spawned bullet instance (null if the spawner has no prefab assigned), so the caller can
        /// configure its speed/lifetime/damage from its own CucumberVirusConfig.</returns>
        VirusBullet SpawnBullet(Vector3 position, Vector3 direction, Transform scaleReference);
    }
}
