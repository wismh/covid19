using UnityEngine;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>Base-tunable numbers for macro-virus movement, behavior, and penetration into cells. Both
    /// variants (Virus/Glist) move identically - only health and penetration delay differ per-variant, kept on
    /// MacroVirusController's own per-instance fields instead of here.</summary>
    [CreateAssetMenu(
        menuName = "Configurations/Viruses/" + nameof(MacroVirusConfig),
        fileName = nameof(MacroVirusConfig) + "_Default")]
    public sealed class MacroVirusConfig : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField] private float _baseMoveSpeed = 2f;
        [SerializeField] private float _acceleration = 0.5f;
        [SerializeField] private float _friction = 0.02f;
        [SerializeField] private float _angularSpeed = 80f;
        [SerializeField] private float _angularAcceleration = 5f;
        [SerializeField] private float _angularFriction = 0.8f;

        [Header("Behavior throttles")]
        [SerializeField] private float _moveToCellThrottle = 1f;
        [SerializeField] private float _attackOrbitThrottle = 0.05f;

        [Header("Penetration")]
        [SerializeField] private float _penetrationDurationSeconds = 0.5f;
        [SerializeField] private float _penetrationScaleFactor = 0.5f;

        [Header("Damage/death presentation timing")]
        [SerializeField] private float _damageFlashDurationSeconds = 0.1f;
        [SerializeField] private float _deathAnimationDelaySeconds = 0.2f;
        [SerializeField] private float _deathToKillDelaySeconds = 0.8f;

        public float BaseMoveSpeed => _baseMoveSpeed;
        public float Acceleration => _acceleration;
        public float Friction => _friction;
        public float AngularSpeed => _angularSpeed;
        public float AngularAcceleration => _angularAcceleration;
        public float AngularFriction => _angularFriction;

        public float MoveToCellThrottle => _moveToCellThrottle;
        public float AttackOrbitThrottle => _attackOrbitThrottle;

        public float PenetrationDurationSeconds => _penetrationDurationSeconds;
        public float PenetrationScaleFactor => _penetrationScaleFactor;

        public float DamageFlashDurationSeconds => _damageFlashDurationSeconds;
        public float DeathAnimationDelaySeconds => _deathAnimationDelaySeconds;
        public float DeathToKillDelaySeconds => _deathToKillDelaySeconds;
    }
}
