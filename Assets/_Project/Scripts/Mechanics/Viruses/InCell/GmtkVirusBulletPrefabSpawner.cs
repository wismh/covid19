using UnityEngine;

namespace _Project.Mechanics.Viruses.InCell
{
    /// <summary>
    /// TODO(virus-bullet migration): temporary bridge to the old GMTK.VirusBullet prefab (Assets/GMTK/Scripts/
    /// Behaviors/MicroVirus/Bullet/VirusBullet.cs, Assets/GMTK/Prefabs/CucumberBullet.prefab) - mirrors
    /// _Project.Mechanics.Player.GmtkBulletManagerProjectileSpawner's bridge role from Phase 2, but simpler:
    /// old CucumberVirus didn't pool its bullets at all (plain `Object.Instantiate`/implicit lifetime-managed
    /// `Destroy(gameObject, _bulletLifetime)` inside VirusBullet itself), so there's no existing pool to
    /// preserve here - this bridge just instantiates the prefab, matching old behavior exactly.
    /// </summary>
    public sealed class GmtkVirusBulletPrefabSpawner : MonoBehaviour, IVirusBulletSpawner
    {
        [SerializeField] private Transform _bulletPrefab;

        public void SpawnBullet(Vector3 position, Vector3 direction)
        {
            // Old: CucumberVirus.FixedUpdate() ->
            // `var bullet = Object.Instantiate(_bulletPrefab, _bulletSpawnPoint); bullet.transform.position = ...; bullet.transform.right = direction;`
            Transform bullet = Instantiate(_bulletPrefab, position, Quaternion.identity);
            bullet.right = direction;
        }
    }
}
