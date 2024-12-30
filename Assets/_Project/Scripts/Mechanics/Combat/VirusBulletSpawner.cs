using UnityEngine;
using Zenject;
using _Project.Mechanics.Viruses.Micro;

namespace _Project.Mechanics.Combat
{
    /// <summary>
    /// Spawns VirusBullet instances for CucumberVirusBehavior.
    /// Replaces the temporary GmtkVirusBulletPrefabSpawner bridge.
    /// Implements IVirusBulletSpawner, parenting bullets to scaleReference to preserve in-cell world scaling.
    /// Pooled via <see cref="VirusBullet.Factory"/> (bound by MicroVirusesInstaller) instead of a
    /// [SerializeField] prefab + Instantiate - see VirusBullet's own class doc comment.
    /// </summary>
    public sealed class VirusBulletSpawner : MonoBehaviour, IVirusBulletSpawner
    {
        private VirusBullet.Factory _bulletFactory;

        [Inject]
        public void Construct(VirusBullet.Factory bulletFactory)
        {
            _bulletFactory = bulletFactory;
        }

        public VirusBullet SpawnBullet(Vector3 position, Vector3 direction, Transform scaleReference)
        {
            return _bulletFactory.Create(position, direction, scaleReference);
        }
    }
}
