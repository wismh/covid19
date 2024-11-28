using UnityEngine;

namespace _Project.Mechanics.Viruses.InCell
{
    /// <summary>
    /// Small seam between CucumberVirusBehavior and whatever actually spawns the bullet, mirroring
    /// _Project.Mechanics.Player.IProjectileSpawner from Phase 2. Old GMTK.MicroViruses.CucumberVirus fired by
    /// directly doing `Object.Instantiate(_bulletPrefab, _bulletSpawnPoint)` with no pooling at all (Assets/
    /// GMTK/Scripts/Behaviors/MicroVirus/Behaviors/CucumberVirus.cs) - unlike the player's bullets there isn't
    /// even an existing pool to bridge to here, so this interface exists purely so CucumberVirusBehavior doesn't
    /// need a concrete spawner type, not to hide an existing pooling system.
    /// </summary>
    public interface IVirusBulletSpawner
    {
        void SpawnBullet(Vector3 position, Vector3 direction);
    }
}
