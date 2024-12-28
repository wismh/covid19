using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>
    /// Ports GMTK.MicroViruses.CucumberVirus (Assets/GMTK/Scripts/Behaviors/MicroVirus/Behaviors/CucumberVirus.
    /// cs) - homing at range plus a periodic VirusBullet volley with an "Attack" animation wind-up.
    ///
    /// Direction sign, preserved exactly from old code: unlike OrangeVirusBehavior's chase-toward-player force,
    /// Cucumber's homing force points AWAY from the player (`(_transform.position - _target.position)
    /// .normalized`, not the reverse) - it kites at range while shooting rather than closing to melee. Easy to
    /// misread as a bug when porting; it isn't - see MicroVirusConfig.KitingForce's doc comment.
    ///
    /// Fixes a fragile lookup along the way: old CucumberVirus.Init() did
    /// `_bulletSpawnPoint = transform.GetChild(0); _attackAudio = _bulletSpawnPoint.GetComponent&lt;AudioSource&gt;();`
    /// - a GetChild(0) index lookup of exactly the kind TECHNICAL_AUDIT.md section 2.2 flags. Both are passed in
    /// via the constructor instead, sourced from MicroVirusController's direct [SerializeField] references.
    ///
    /// Bullet spawning goes through <see cref="IVirusBulletSpawner"/> rather than calling
    /// `Object.Instantiate` directly - see that interface's doc comment.
    /// </summary>
    public sealed class CucumberVirusBehavior : IMicroVirusBehavior
    {
        private readonly MicroVirusConfig _config;
        private readonly Transform _target;
        private readonly Transform _bulletSpawnPoint;
        private readonly AudioSource _attackAudioSource;
        private readonly IVirusBulletSpawner _bulletSpawner;

        private Transform _transform;
        private Rigidbody2D _rigidbody2D;
        private Animator _animator;

        private float _attackTime;
        private bool _animationPlaying;

        public CucumberVirusBehavior(
            MicroVirusConfig config,
            Transform target,
            Transform bulletSpawnPoint,
            AudioSource attackAudioSource,
            IVirusBulletSpawner bulletSpawner)
        {
            _config = config;
            _target = target;
            _bulletSpawnPoint = bulletSpawnPoint;
            _attackAudioSource = attackAudioSource;
            _bulletSpawner = bulletSpawner;
        }

        public void Init(Transform transform)
        {
            _transform = transform;
            _rigidbody2D = transform.GetComponent<Rigidbody2D>();
            _animator = transform.GetComponent<Animator>();
        }

        /// <summary>Old: CucumberVirus.OnEnable() -> `_attackTime = Time.time + Random.Range(0, k_attackCooldown
        /// * 1.5f); _animationPlaying = false;`</summary>
        public void OnEnable()
        {
            _attackTime = Time.time + UnityEngine.Random.Range(0f, _config.BulletFireCooldownSeconds * _config.InitialCooldownJitterMultiplier);
            _animationPlaying = false;
        }

        public void Update() { }

        /// <summary>Old: CucumberVirus.FixedUpdate().</summary>
        public void FixedUpdate()
        {
            if (_target == null)
                return;

            // Old: `_rd.AddForce((_transform.position - _target.position).normalized * k_speed, ForceMode2D.Force);`
            _rigidbody2D.AddForce((_transform.position - _target.position).normalized * _config.KitingForce, ForceMode2D.Force);

            // Old: `if (Time.time > _attackTime + k_attackCooldown / 2 && !_animationPlaying) { ...Play("Base Layer.Attack"); }`
            if (Time.time > _attackTime + _config.BulletFireCooldownSeconds * _config.BulletWindupLeadFraction && !_animationPlaying)
            {
                _animationPlaying = true;
                _animator?.Play("Base Layer.Attack");
            }

            // Old: `if (Time.time > _attackTime + k_attackCooldown) { ... Instantiate bullet ... }`
            if (Time.time > _attackTime + _config.BulletFireCooldownSeconds)
            {
                _attackTime = Time.time + _config.BulletFireCooldownSeconds;
                _animationPlaying = false;

                Vector3 direction = (_target.position - _transform.position).normalized;
                Vector3 spawnPosition = _bulletSpawnPoint != null ? _bulletSpawnPoint.position : _transform.position;

                var bullet = _bulletSpawner?.SpawnBullet(spawnPosition, direction, _bulletSpawnPoint);
                bullet?.Construct(_config.VirusBulletSpeed, _config.VirusBulletLifetimeSeconds, _config.VirusBulletDamage);
                _attackAudioSource?.Play();
            }
        }
    }
}
