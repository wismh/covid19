using UnityEngine;

namespace _Project.Mechanics.Player
{
    /// <summary>Base-tunable numbers for player movement, health/armor, combat, and progression.</summary>
    [CreateAssetMenu(
        menuName = "Configurations/Player/" + nameof(PlayerConfig),
        fileName = nameof(PlayerConfig) + "_Default")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField] private float _baseMoveSpeed = 3f;
        [SerializeField] private float _acceleration = 1f;
        [SerializeField] private float _friction = 0.02f;
        [SerializeField] private float _baseMobility = 80f;
        [SerializeField] private float _angularAcceleration = 5f;
        [SerializeField] private float _angularFriction = 0.8f;
        [SerializeField] private float _backwardMovementMultiplier = 0.05f;

        [Header("Health & armor")]
        [SerializeField] private int _startingHealth = 3;

        // Distinct from StartingHealth: the ceiling the HP upgrade grows current health toward. Keep these two
        // fields separate - collapsing them into one silently caps the HP upgrade at the starting value.
        [SerializeField] private int _baseMaxHealth = 8;

        [SerializeField] private int _baseArmor = 0;
        [SerializeField] private int _maxArmor = 8;
        [SerializeField] private float _armorIgnoreChanceDivisor = 6.0f;

        // Proc-roll range for the armor-ignore chance - deliberately NOT [0,1): a roll can never land below 0.1.
        [SerializeField] private float _armorIgnoreRollMin = 0.1f;
        [SerializeField] private float _armorIgnoreRollMax = 1.0f;

        [SerializeField] private float _knockbackImpulseStrength = 0.3f;

        [Header("Damage/death presentation timing")]
        [SerializeField] private float _damageFlashDurationSeconds = 0.1f;
        [SerializeField] private float _deathAnimationDelaySeconds = 0.2f;
        [SerializeField] private float _deathToGameOverDelaySeconds = 1.8f;

        [Header("Combat")]
        [SerializeField] private int _baseDamage = 1;
        [SerializeField] private float _baseReloadTime = 1.75f;
        [SerializeField] private float _reloadTimeReductionPerUpgradeLevel = 0.25f;
        [SerializeField] private float _minReloadTime = 0.05f;
        [SerializeField] private float _bulletSpeed = 10f;
        [SerializeField] private float _bulletLifetimeSeconds = 2f;

        [Header("Progression")]
        [SerializeField] private int _startingMaxExperience = 3;
        [SerializeField] private float _experienceGrowthMultiplierPerLevel = 1.5f;

        // Applies on every upgrade pick (not just Health) whenever the player's overall level is even.
        [SerializeField] private int _bonusHealthPerEvenLevel = 1;

        [SerializeField] private int _upgradeChoicesPerLevelUp = 3;

        public float BaseMoveSpeed => _baseMoveSpeed;
        public float Acceleration => _acceleration;
        public float Friction => _friction;
        public float BaseMobility => _baseMobility;
        public float AngularAcceleration => _angularAcceleration;
        public float AngularFriction => _angularFriction;
        public float BackwardMovementMultiplier => _backwardMovementMultiplier;

        public int StartingHealth => _startingHealth;
        public int BaseMaxHealth => _baseMaxHealth;
        public int BaseArmor => _baseArmor;
        public int MaxArmor => _maxArmor;
        public float ArmorIgnoreChanceDivisor => _armorIgnoreChanceDivisor;
        public float ArmorIgnoreRollMin => _armorIgnoreRollMin;
        public float ArmorIgnoreRollMax => _armorIgnoreRollMax;
        public float KnockbackImpulseStrength => _knockbackImpulseStrength;

        public float DamageFlashDurationSeconds => _damageFlashDurationSeconds;
        public float DeathAnimationDelaySeconds => _deathAnimationDelaySeconds;
        public float DeathToGameOverDelaySeconds => _deathToGameOverDelaySeconds;

        public int BaseDamage => _baseDamage;
        public float BaseReloadTime => _baseReloadTime;
        public float ReloadTimeReductionPerUpgradeLevel => _reloadTimeReductionPerUpgradeLevel;
        public float MinReloadTime => _minReloadTime;
        public float BulletSpeed => _bulletSpeed;
        public float BulletLifetimeSeconds => _bulletLifetimeSeconds;

        public int StartingMaxExperience => _startingMaxExperience;
        public float ExperienceGrowthMultiplierPerLevel => _experienceGrowthMultiplierPerLevel;
        public int BonusHealthPerEvenLevel => _bonusHealthPerEvenLevel;
        public int UpgradeChoicesPerLevelUp => _upgradeChoicesPerLevelUp;
    }
}
