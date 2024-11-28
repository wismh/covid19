using UnityEngine;

namespace _Project.Mechanics.Player
{
    /// <summary>
    /// Every base-tunable number found while porting the old GMTK player systems (Assets/GMTK/Scripts/Behaviors/
    /// Player/*, Assets/GMTK/Scripts/Behaviors/DriftMovableObject.cs, Assets/GMTK/Scripts/Behaviors/Bullet/
    /// Bullet.cs) - see TECHNICAL_AUDIT.md section 2.2 ("Магічні числа") for why these used to be scattered
    /// literals instead of a single config asset. Concrete numbers below were read directly from
    /// Assets/GMTK/Prefabs/Player.prefab and Bullet.prefab's serialized component values, not guessed.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Configurations/Player/" + nameof(PlayerConfig),
        fileName = nameof(PlayerConfig) + "_Default")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("Movement (ported from GMTK.DriftMovableObject, values from Player.prefab)")]
        [SerializeField] private float _baseMoveSpeed = 3f;
        [SerializeField] private float _acceleration = 1f;
        [SerializeField] private float _friction = 0.02f;
        [SerializeField] private float _baseMobility = 80f;
        [SerializeField] private float _angularAcceleration = 5f;
        [SerializeField] private float _angularFriction = 0.8f;

        // Old: GMTK.PlayerBodyController.FixedUpdate -> `inputValue.y < 0 ? 0.05f : 1f`.
        [SerializeField] private float _backwardMovementMultiplier = 0.05f;

        [Header("Health & armor (ported from GMTK.PlayerDamageable)")]
        // Old PlayerDamageable.Start() hardcoded `_health = 3`, ignoring the serialized `_maxHealth` field
        // (which was only ever used as the upgrade ceiling) - a known bug, see TECHNICAL_AUDIT.md section 5.
        // This field is that same value (read from Player.prefab: _maxHealth: 8) but is now actually used as the
        // real starting health too (see PlayerStatsService.EffectiveMaxHealth / PlayerHealth.Start()).
        [SerializeField] private int _baseMaxHealth = 8;

        [SerializeField] private int _baseArmor = 0;

        // Old: PlayerDamageable.OnUpgrade hardcoded `Armor < 8` inline; named here instead.
        [SerializeField] private int _maxArmor = 8;

        // Old: PlayerDamageable.Damage -> `float chanceIgnore = Armor / 6.0f;`.
        [SerializeField] private float _armorIgnoreChanceDivisor = 6.0f;

        [Header("Combat (ported from GMTK.PlayerTurretController / GMTK.Bullet, values from prefabs)")]
        [SerializeField] private int _baseDamage = 1;
        [SerializeField] private float _baseReloadTime = 1.75f;

        // Historical mirror of the old PlayerTurretController._scaleByLevelUpgrade serialized field (0.25,
        // read from Player.prefab). The value actually consumed at runtime by PlayerStatsService.
        // EffectiveReloadTime is UpgradeDefinition_AttackSpeed.EffectPerLevel (single source of truth) - this
        // field exists so PlayerConfig still captures every original tunable, per the phase-2 spec, without the
        // two independently-authored assets needing to be kept in perfect sync to stay meaningful individually.
        [SerializeField] private float _reloadTimeReductionPerUpgradeLevel = 0.25f;

        [SerializeField] private float _minReloadTime = 0.25f;

        [Header("Progression (ported from GMTK.Services.PlayerStats)")]
        [SerializeField] private int _startingMaxExperience = 3;
        [SerializeField] private float _experienceGrowthMultiplierPerLevel = 1.5f;

        // Old: a silent side effect buried inside PlayerDamageable.OnUpgrade -
        // `if (_playerStats.Level % 2 == 0) _health += 1;` - ran for EVERY upgrade type, not just PLAYER_HP.
        // Named explicitly here; see PlayerStatsService.ApplyUpgrade / OnBonusHealthGranted for where it's now
        // an explicit, documented rule instead of a hidden side effect.
        [SerializeField] private int _bonusHealthPerEvenLevel = 1;

        public float BaseMoveSpeed => _baseMoveSpeed;
        public float Acceleration => _acceleration;
        public float Friction => _friction;
        public float BaseMobility => _baseMobility;
        public float AngularAcceleration => _angularAcceleration;
        public float AngularFriction => _angularFriction;
        public float BackwardMovementMultiplier => _backwardMovementMultiplier;

        public int BaseMaxHealth => _baseMaxHealth;
        public int BaseArmor => _baseArmor;
        public int MaxArmor => _maxArmor;
        public float ArmorIgnoreChanceDivisor => _armorIgnoreChanceDivisor;

        public int BaseDamage => _baseDamage;
        public float BaseReloadTime => _baseReloadTime;
        public float ReloadTimeReductionPerUpgradeLevel => _reloadTimeReductionPerUpgradeLevel;
        public float MinReloadTime => _minReloadTime;

        public int StartingMaxExperience => _startingMaxExperience;
        public float ExperienceGrowthMultiplierPerLevel => _experienceGrowthMultiplierPerLevel;
        public int BonusHealthPerEvenLevel => _bonusHealthPerEvenLevel;
    }
}
