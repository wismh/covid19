using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    [CreateAssetMenu(
        menuName = "Configurations/Viruses/" + nameof(OrangeVirusConfig),
        fileName = nameof(OrangeVirusConfig) + "_Default")]
    public sealed class OrangeVirusConfig : MicroVirusConfig
    {
        [Header("Continuous homing force")]
        [SerializeField] private float _homingForce = 0.1f;

        public float HomingForce => _homingForce;
    }
}
