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

        public void SpawnBullet(Vector3 position, Vector3 direction, Transform scaleReference)
        {
            // Old: CucumberVirus.FixedUpdate() -> `var bullet = Object.Instantiate(_bulletPrefab,
            // _bulletSpawnPoint); bullet.transform.position = ...; bullet.transform.right = direction;` - the
            // 2-arg Instantiate overload parents the bullet under _bulletSpawnPoint, which is what actually gave
            // it the correct small in-cell-world scale (see IVirusBulletSpawner.SpawnBullet's doc comment) - not
            // ported until now, which is why bullets rendered at their raw macro-sized prefab scale instead.
            Transform bullet = scaleReference != null
                ? Instantiate(_bulletPrefab, scaleReference)
                : Instantiate(_bulletPrefab);

            bullet.position = position;
            bullet.right = direction;
        }
    }
}
