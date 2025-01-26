using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    [CreateAssetMenu(
        menuName = "Configurations/Viruses/" + nameof(SpinerVirusConfig),
        fileName = nameof(SpinerVirusConfig) + "_Default")]
    public sealed class SpinerVirusConfig : MicroVirusConfig
    {
        [Header("Periodic impulse rush")]
        [SerializeField] private float _rushImpulse = 5f;
        [SerializeField] private float _rushCooldownSeconds = 4f;

        [Header("Attack timing")]
        // Randomizes each spawned virus's initial attack timer so a batch doesn't all attack in lockstep.
        [SerializeField] private float _initialCooldownJitterMultiplier = 1.5f;

        [SerializeField] private float _rushWindupLeadSeconds = 1f;

        public float RushImpulse => _rushImpulse;
        public float RushCooldownSeconds => _rushCooldownSeconds;

        public float InitialCooldownJitterMultiplier => _initialCooldownJitterMultiplier;
        public float RushWindupLeadSeconds => _rushWindupLeadSeconds;
    }
}
