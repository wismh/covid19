using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    [CreateAssetMenu(
        menuName = "Configurations/Viruses/" + nameof(CucumberVirusConfig),
        fileName = nameof(CucumberVirusConfig) + "_Default")]
    public sealed class CucumberVirusConfig : MicroVirusConfig
    {
        [Header("Homing + bullet volley")]
        // Direction is away from the player - Cucumber kites at range while shooting rather than closing to melee.
        [SerializeField] private float _kitingForce = 0.1f;

        // Bullet fire interval - independent from CollisionDamageCooldownSeconds even though both default to 2s.
        [SerializeField] private float _bulletFireCooldownSeconds = 2f;

        [SerializeField] private float _virusBulletSpeed = 4f;
        [SerializeField] private float _virusBulletLifetimeSeconds = 5f;
        [SerializeField] private int _virusBulletDamage = 1;

        [Header("Attack timing")]
        // Randomizes each spawned virus's initial attack timer so a batch doesn't all attack in lockstep.
        [SerializeField] private float _initialCooldownJitterMultiplier = 1.5f;

        [SerializeField] private float _bulletWindupLeadFraction = 0.5f;

        public float KitingForce => _kitingForce;
        public float BulletFireCooldownSeconds => _bulletFireCooldownSeconds;

        public float VirusBulletSpeed => _virusBulletSpeed;
        public float VirusBulletLifetimeSeconds => _virusBulletLifetimeSeconds;
        public int VirusBulletDamage => _virusBulletDamage;

        public float InitialCooldownJitterMultiplier => _initialCooldownJitterMultiplier;
        public float BulletWindupLeadFraction => _bulletWindupLeadFraction;
    }
}
