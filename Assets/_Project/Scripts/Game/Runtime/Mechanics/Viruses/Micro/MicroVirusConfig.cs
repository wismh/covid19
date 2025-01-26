using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>Base-tunable numbers shared by all micro virus variants. Each variant's own numbers live on its
    /// own subclass (<see cref="OrangeVirusConfig"/>/<see cref="CucumberVirusConfig"/>/<see cref="SpinerVirusConfig"/>)
    /// instead of being declared here and left unused by the other variants.</summary>
    public abstract class MicroVirusConfig : ScriptableObject
    {
        [Header("Health & melee")]
        [SerializeField] private int _maxHealth;
        [SerializeField] private int _meleeDamage;
        [SerializeField] private float _collisionDamageCooldownSeconds = 2f;
        [SerializeField] private float _knockbackForceOnHit = 0.8f;

        [Header("Death presentation timing")]
        // Damage flash / death-animation timing lives on the shared DamageDeathPresentationConfig instead.
        [SerializeField] private float _deathToKillDelaySeconds = 0.8f;

        public int MaxHealth => _maxHealth;
        public int MeleeDamage => _meleeDamage;
        public float CollisionDamageCooldownSeconds => _collisionDamageCooldownSeconds;
        public float KnockbackForceOnHit => _knockbackForceOnHit;

        public float DeathToKillDelaySeconds => _deathToKillDelaySeconds;
    }
}
