using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>
    /// One ScriptableObject class, instantiated three times (MicroVirusConfig_Orange/_Cucumber/_Spiner.asset) -
    /// same convention as Phase 2's six UpgradeDefinition_&lt;Stat&gt; assets (REFACTORING_PLAN.md section 3.1:
    /// "`_&lt;ОписоваНазва&gt;` для одного з багатьох (співпадає з enum/id, який представляє)"). Every number
    /// below was read directly from the matching prefab's serialized InCellVirus component (Assets/GMTK/
    /// Prefabs/Orange.prefab, Cucumber.prefab, Spiner.prefab) plus the shared per-behavior constants in
    /// Assets/GMTK/Scripts/Behaviors/MicroVirus/Behaviors/{OrangeVirus,CucumberVirus,SpinerVirus}.cs, not
    /// guessed.
    ///
    /// Note on the shared 2s collision-damage cooldown: old GMTK.MicroViruses.InCellVirus declares
    /// `k_attackCooldown = 2f` once, applied uniformly to melee contact damage against the player regardless of
    /// which VirusBehavior is active (Orange/Cucumber/Spiner all deal melee damage on contact - see
    /// MicroVirusController.OnCollisionStay2D). Since this project's convention authors each variant as an
    /// independent asset rather than one shared config, the same value (2s) is duplicated across all three
    /// MicroVirusConfig_* assets rather than factored into a fourth shared asset - not worth a whole extra
    /// ScriptableObject for one duplicated float given only 3 variants exist.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Configurations/Viruses/" + nameof(MicroVirusConfig),
        fileName = nameof(MicroVirusConfig) + "_New")]
    public sealed class MicroVirusConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private MicroVirusVariant _variant;

        [Header("Health & melee (values from Orange/Cucumber/Spiner.prefab InCellVirus component)")]
        [SerializeField] private int _maxHealth;

        // Old: InCellVirus._damage - melee contact damage dealt to the player via OnCollisionStay2D.
        [SerializeField] private int _meleeDamage;

        // Old: InCellVirus.k_attackCooldown (private const, 2f, shared by all three variants - see class doc).
        [SerializeField] private float _collisionDamageCooldownSeconds = 2f;

        // Old: InCellVirus.AnimationOnDamage() -> `rd.AddForce(attackDirection * 0.8f, ForceMode2D.Impulse);`
        [SerializeField] private float _knockbackForceOnHit = 0.8f;

        [Header("Damage/death presentation timing (values from InCellVirus.AnimationOnDamage()/AnimationOnDied())")]
        [SerializeField] private float _damageFlashDurationSeconds = 0.1f;
        [SerializeField] private float _deathAnimationDelaySeconds = 0.2f;
        [SerializeField] private float _deathToKillDelaySeconds = 0.8f;

        [Header("Orange - continuous homing force (Assets/GMTK/Scripts/.../OrangeVirus.cs)")]
        // Old: OrangeVirus.k_speed (private const, 0.1f) - ForceMode2D.Force magnitude, direction = toward player.
        [SerializeField] private float _homingForce = 0.1f;

        [Header("Cucumber - homing + bullet volley (Assets/GMTK/Scripts/.../CucumberVirus.cs)")]
        // Old: CucumberVirus.k_speed (private const, 0.1f) - ForceMode2D.Force magnitude. Direction is AWAY from
        // the player (`(_transform.position - _target.position).normalized`), the opposite sign convention from
        // Orange's chase-toward force - Cucumber kites at range while shooting rather than closing to melee.
        // Preserved exactly; see CucumberVirusBehavior's doc comment.
        [SerializeField] private float _kitingForce = 0.1f;

        // Old: CucumberVirus.k_attackCooldown (private const, 2f) - bullet fire interval. Numerically the same
        // as _collisionDamageCooldownSeconds above but a DIFFERENT old constant governing a different thing
        // (bullet fire rate, not melee contact damage) - kept as a separate field so changing one doesn't
        // silently change the other, exactly as they were two independent `const` declarations in old code.
        [SerializeField] private float _bulletFireCooldownSeconds = 2f;

        // Old: GMTK.VirusBullet's own [SerializeField] defaults (speed 4f, lifetime 5f, damage 1) - previously a
        // prefab-only value with no config backing at all (2026-09-19 hardcode audit).
        [SerializeField] private float _virusBulletSpeed = 4f;
        [SerializeField] private float _virusBulletLifetimeSeconds = 5f;
        [SerializeField] private int _virusBulletDamage = 1;

        [Header("Spiner - periodic impulse rush (Assets/GMTK/Scripts/.../SpinerVirus.cs)")]
        // Old: SpinerVirus.k_impulse (private const, 5f) - ForceMode2D.Impulse magnitude toward the player.
        [SerializeField] private float _rushImpulse = 5f;

        // Old: SpinerVirus.k_attackCooldown (private const, 4f) - rush interval, its own separate constant.
        [SerializeField] private float _rushCooldownSeconds = 4f;

        [Header("Attack timing (shared formula shape across Cucumber/Spiner's OnEnable/FixedUpdate - see " +
                "CucumberVirusBehavior/SpinerVirusBehavior)")]
        // Old: CucumberVirus.OnEnable()/SpinerVirus.OnEnable() -> `Random.Range(0, k_attackCooldown * 1.5f)` -
        // randomizes each spawned virus's initial attack timer so a batch doesn't all attack in lockstep.
        [SerializeField] private float _initialCooldownJitterMultiplier = 1.5f;

        // Old: CucumberVirus.FixedUpdate() -> `Time.time > _attackTime + k_attackCooldown / 2` - the "Attack"
        // wind-up animation starts halfway through the fire-cooldown window.
        [SerializeField] private float _bulletWindupLeadFraction = 0.5f;

        // Old: SpinerVirus.FixedUpdate() -> `Time.time > (_attackTime + k_attackCooldown - 1)` - the "Attack"
        // wind-up animation starts exactly 1 second before the rush fires.
        [SerializeField] private float _rushWindupLeadSeconds = 1f;

        public MicroVirusVariant Variant => _variant;
        public int MaxHealth => _maxHealth;
        public int MeleeDamage => _meleeDamage;
        public float CollisionDamageCooldownSeconds => _collisionDamageCooldownSeconds;
        public float KnockbackForceOnHit => _knockbackForceOnHit;

        public float DamageFlashDurationSeconds => _damageFlashDurationSeconds;
        public float DeathAnimationDelaySeconds => _deathAnimationDelaySeconds;
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
