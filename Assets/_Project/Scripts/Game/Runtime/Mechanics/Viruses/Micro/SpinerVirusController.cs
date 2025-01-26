using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    public sealed class SpinerVirusController : MicroVirusController
    {
        [SerializeField] private SpinerVirusConfig _config;

        protected override MicroVirusConfig Config => _config;

        protected override IMicroVirusBehavior BuildBehavior(Transform target)
            => new SpinerVirusBehavior(_config, target, Rigidbody2D, Animator);
    }
}
