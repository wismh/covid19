using UnityEngine;
using Zenject;
using _Project.Mechanics.Viruses.Micro;

namespace _Project.Mechanics.Combat
{
    /// <summary>Spawns VirusBullet instances for CucumberVirusBehavior, parenting bullets to scaleReference to
    /// preserve in-cell world scaling.</summary>
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
