using UnityEngine;
using _Project.Mechanics.Viruses.InCell;

namespace _Project.Mechanics.Combat
{
    /// <summary>
    /// Spawns VirusBullet instances for CucumberVirusBehavior.
    /// Replaces the temporary GmtkVirusBulletPrefabSpawner bridge.
    /// Implements IVirusBulletSpawner, parenting bullets to scaleReference to preserve in-cell world scaling.
    /// </summary>
    public sealed class VirusBulletSpawner : MonoBehaviour, IVirusBulletSpawner
    {
        [SerializeField] private VirusBullet _bulletPrefab;

        public void SpawnBullet(Vector3 position, Vector3 direction, Transform scaleReference)
        {
            if (_bulletPrefab == null)
                return;

            VirusBullet bullet = scaleReference != null
                ? Instantiate(_bulletPrefab, scaleReference)
                : Instantiate(_bulletPrefab);

            Transform bulletTransform = bullet.transform;
            bulletTransform.position = position;
            bulletTransform.right = direction;
        }
    }
}
