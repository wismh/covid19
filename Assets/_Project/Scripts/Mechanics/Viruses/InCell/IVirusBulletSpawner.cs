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
    ///
    /// --- <paramref name="scaleReference"/> (Phase 8 testing fix, 2026-09-11) ---
    /// Old code's `Instantiate(_bulletPrefab, _bulletSpawnPoint)` used the 2-arg overload that instantiates the
    /// bullet AS A CHILD of `_bulletSpawnPoint` - old CucumberBullet.prefab's own localScale was authored for a
    /// MACRO-world 1x parent, so being parented deep inside the tiny in-cell hierarchy (Cell.prefab's 3x root x
    /// the "Viruses" container's compensating 0.15x, net ~0.45x world scale - see InCellVirusSpawner._
    /// virusContainer's doc comment) is what actually shrank the bullet to the right size; old code never scaled
    /// it explicitly. User-reported symptom this fixes: "у cucumber завеликі снаряди" - this bridge's original
    /// `Instantiate(_bulletPrefab, position, Quaternion.identity)` (no parent at all) skipped that inherited
    /// scale entirely, spawning bullets at their prefab's raw macro-sized scale inside the micro world. Passing
    /// the same <see cref="_Project.Mechanics.Viruses.InCell.InCellVirusController"/>-owned bulletSpawnPoint
    /// Transform back through here (rather than reading it off the config) lets the implementation reproduce
    /// that exact parenting.
    /// </summary>
    public interface IVirusBulletSpawner
    {
        void SpawnBullet(Vector3 position, Vector3 direction, Transform scaleReference);
    }
}
