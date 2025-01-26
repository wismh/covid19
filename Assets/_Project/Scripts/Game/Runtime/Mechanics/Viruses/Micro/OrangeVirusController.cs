using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    public sealed class OrangeVirusController : MicroVirusController
    {
        [SerializeField] private OrangeVirusConfig _config;

        protected override MicroVirusConfig Config => _config;

        protected override IMicroVirusBehavior BuildBehavior(Transform target)
            => new OrangeVirusBehavior(_config, target, Rigidbody2D);
    }
}
