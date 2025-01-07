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
        // Intentional starting health value (design decision, not a bug) - matches what players actually
        // experienced in the shipped jam build. Old PlayerDamageable.Start() set this via a hardcoded literal
        // (`_health = 3;`) rather than reading it from a config/serialized field, which is a separate,
        // purely code-quality issue (magic number, not centrally tunable) - not a statement about the value
        // itself being wrong. Now a proper config value here instead of a literal buried in code.
        [SerializeField] private int _startingHealth = 3;

        // The OLD `_maxHealth` field, confirmed via git history (Player.prefab, right before the Phase 2 port)
        // to be 8 in the final shipped jam build - a distinct value from StartingHealth above, not the same
        // number and not interchangeable with it. It was the real, working ceiling for
        // `PlayerDamageable.OnUpgrade`'s `if (_upgrade == UpgradeType.PLAYER_HP && _health < _maxHealth)
        // _health += 1;` - i.e. players started at 3 HP and could grow to 8 by picking the HP upgrade card. A
        // prior round collapsed this and StartingHealth into one field (both set to 3), which silently also
        // capped the upgrade ceiling at 3 - the HP upgrade card became a no-op once at full health. Split back
        // into two fields so both original values are preserved simultaneously.
        [SerializeField] private int _baseMaxHealth = 8;

        [SerializeField] private int _baseArmor = 0;

        // Old: PlayerDamageable.OnUpgrade hardcoded `Armor < 8` inline; named here instead.
        [SerializeField] private int _maxArmor = 8;

        // Old: PlayerDamageable.Damage -> `float chanceIgnore = Armor / 6.0f;`.
        [SerializeField] private float _armorIgnoreChanceDivisor = 6.0f;

        // Old: PlayerDamageable.Damage -> `Random.Range(0.1f, 1.0f) < chanceIgnore` - the proc-roll range for
        // the armor-ignore chance (deliberately NOT [0,1): a roll can never actually land below 0.1).
        [SerializeField] private float _armorIgnoreRollMin = 0.1f;
        [SerializeField] private float _armorIgnoreRollMax = 1.0f;

        // Old: PlayerDamageable.AnimationOnDamage -> `_body.Impulse(attackDirection * 0.3f);`
        [SerializeField] private float _knockbackImpulseStrength = 0.3f;

        [Header("Damage/death presentation timing (values from PlayerDamageable.AnimationOnDamage()/AnimationOnDied())")]
        [SerializeField] private float _damageFlashDurationSeconds = 0.1f;
        [SerializeField] private float _deathAnimationDelaySeconds = 0.2f;
        [SerializeField] private float _deathToGameOverDelaySeconds = 1.8f;

        [Header("Combat (ported from GMTK.PlayerTurretController / GMTK.Bullet, values from prefabs)")]
        [SerializeField] private int _baseDamage = 1;
        [SerializeField] private float _baseReloadTime = 1.75f;

        // Historical mirror of the old PlayerTurretController._scaleByLevelUpgrade serialized field (0.25,
        // read from Player.prefab). The value actually consumed at runtime by PlayerStatsService.
        // EffectiveReloadTime is UpgradeDefinition_AttackSpeed.EffectPerLevel (single source of truth) - this
        // field exists so PlayerConfig still captures every original tunable, per the phase-2 spec, without the
        // two independently-authored assets needing to be kept in perfect sync to stay meaningful individually.
        [SerializeField] private float _reloadTimeReductionPerUpgradeLevel = 0.25f;

        [SerializeField] private float _minReloadTime = 0.05f;

        // Old: GMTK.Bullet's own [SerializeField] defaults (speed 10f, lifetime 2f) - previously a prefab-only
        // value with no config backing at all (2026-09-19 hardcode audit).
        [SerializeField] private float _bulletSpeed = 10f;
        [SerializeField] private float _bulletLifetimeSeconds = 2f;

        [Header("Progression (ported from GMTK.Services.PlayerStats)")]
        [SerializeField] private int _startingMaxExperience = 3;
        [SerializeField] private float _experienceGrowthMultiplierPerLevel = 1.5f;

        // Old: a silent side effect buried inside PlayerDamageable.OnUpgrade -
        // `if (_playerStats.Level % 2 == 0) _health += 1;` - ran for EVERY upgrade type, not just PLAYER_HP.
        // Named explicitly here; see PlayerStatsService.ApplyUpgrade / OnBonusHealthGranted for where it's now
        // an explicit, documented rule instead of a hidden side effect.
        [SerializeField] private int _bonusHealthPerEvenLevel = 1;

        // Old: UpgradesHUD.ShowUpgrades() -> offers 3 random upgrade cards (of 6 total types) per level-up.
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
