using Project.Core.AudioSystem;
using UnityEngine;
using _Project.Mechanics.Combat;

namespace _Project.Mechanics.Viruses.Micro
{
    public sealed class CucumberVirusController : MicroVirusController
    {
        [SerializeField] private CucumberVirusConfig _config;

        [SerializeField] private Transform _bulletSpawnPoint;
        [SerializeField] private SoundData _attackSound;
        [SerializeField] private VirusBulletSpawner _bulletSpawner;

        protected override MicroVirusConfig Config => _config;

        protected override IMicroVirusBehavior BuildBehavior(Transform target)
            => new CucumberVirusBehavior(_config, target, _bulletSpawnPoint, AudioSystem, _attackSound, _bulletSpawner, Rigidbody2D, Animator);
    }
}
