using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>One ScriptableObject class, instantiated three times (MicroVirusConfig_Orange/_Cucumber/_Spiner).
    /// </summary>
    [CreateAssetMenu(
        menuName = "Configurations/Viruses/" + nameof(MicroVirusConfig),
        fileName = nameof(MicroVirusConfig) + "_New")]
    public sealed class MicroVirusConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private MicroVirusVariant _variant;

        [Header("Health & melee")]
        [SerializeField] private int _maxHealth;
        [SerializeField] private int _meleeDamage;
        [SerializeField] private float _collisionDamageCooldownSeconds = 2f;
        [SerializeField] private float _knockbackForceOnHit = 0.8f;

        [Header("Death presentation timing")]
        // Damage flash / death-animation timing lives on the shared DamageDeathPresentationConfig instead.
        [SerializeField] private float _deathToKillDelaySeconds = 0.8f;

        [Header("Orange - continuous homing force")]
        [SerializeField] private float _homingForce = 0.1f;

        [Header("Cucumber - homing + bullet volley")]
        // Direction is away from the player - Cucumber kites at range while shooting rather than closing to melee.
        [SerializeField] private float _kitingForce = 0.1f;

        // Bullet fire interval - independent from _collisionDamageCooldownSeconds even though both default to 2s.
        [SerializeField] private float _bulletFireCooldownSeconds = 2f;

        [SerializeField] private float _virusBulletSpeed = 4f;
        [SerializeField] private float _virusBulletLifetimeSeconds = 5f;
        [SerializeField] private int _virusBulletDamage = 1;

        [Header("Spiner - periodic impulse rush")]
        [SerializeField] private float _rushImpulse = 5f;
        [SerializeField] private float _rushCooldownSeconds = 4f;

        [Header("Attack timing")]
        // Randomizes each spawned virus's initial attack timer so a batch doesn't all attack in lockstep.
        [SerializeField] private float _initialCooldownJitterMultiplier = 1.5f;

        [SerializeField] private float _bulletWindupLeadFraction = 0.5f;
        [SerializeField] private float _rushWindupLeadSeconds = 1f;

        public MicroVirusVariant Variant => _variant;
        public int MaxHealth => _maxHealth;
        public int MeleeDamage => _meleeDamage;
        public float CollisionDamageCooldownSeconds => _collisionDamageCooldownSeconds;
        public float KnockbackForceOnHit => _knockbackForceOnHit;

        public float DeathToKillDelaySeconds => _deathToKillDelaySeconds;

        public float HomingForce => _homingForce;

        public float KitingForce => _kitingForce;
        public float BulletFireCooldownSeconds => _bulletFireCooldownSeconds;

        public float VirusBulletSpeed => _virusBulletSpeed;
        public float VirusBulletLifetimeSeconds => _virusBulletLifetimeSeconds;
        public int VirusBulletDamage => _virusBulletDamage;

        public float RushImpulse => _rushImpulse;
        public float RushCooldownSeconds => _rushCooldownSeconds;

        public float InitialCooldownJitterMultiplier => _initialCooldownJitterMultiplier;
        public float BulletWindupLeadFraction => _bulletWindupLeadFraction;
        public float RushWindupLeadSeconds => _rushWindupLeadSeconds;
    }
}
