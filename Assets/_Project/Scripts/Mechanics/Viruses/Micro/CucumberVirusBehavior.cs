using Project.Core.AudioSystem;
using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>Homing at range plus a periodic VirusBullet volley with an "Attack" animation wind-up. The
    /// homing force points AWAY from the player, not toward it (unlike OrangeVirusBehavior) - it kites at range
    /// while shooting rather than closing to melee; not a sign error.</summary>
    public sealed class CucumberVirusBehavior : IMicroVirusBehavior
    {
        private readonly MicroVirusConfig _config;
        private readonly Transform _target;
        private readonly Transform _bulletSpawnPoint;
        private readonly IAudioSystem _audioSystem;
        private readonly AudioClip _attackClip;
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
            IAudioSystem audioSystem,
            AudioClip attackClip,
            IVirusBulletSpawner bulletSpawner)
        {
            _config = config;
            _target = target;
            _bulletSpawnPoint = bulletSpawnPoint;
            _audioSystem = audioSystem;
            _attackClip = attackClip;
            _bulletSpawner = bulletSpawner;
        }

        public void Init(Transform transform)
        {
            _transform = transform;
            _rigidbody2D = transform.GetComponent<Rigidbody2D>();
            _animator = transform.GetComponent<Animator>();
        }

        public void OnEnable()
        {
            _attackTime = Time.time + UnityEngine.Random.Range(0f, _config.BulletFireCooldownSeconds * _config.InitialCooldownJitterMultiplier);
            _animationPlaying = false;
        }

        public void Update() { }

        public void FixedUpdate()
        {
            if (_target == null)
                return;

            _rigidbody2D.AddForce((_transform.position - _target.position).normalized * _config.KitingForce, ForceMode2D.Force);

            if (Time.time > _attackTime + _config.BulletFireCooldownSeconds * _config.BulletWindupLeadFraction && !_animationPlaying)
            {
                _animationPlaying = true;
                _animator?.Play("Base Layer.Attack");
            }

            if (Time.time > _attackTime + _config.BulletFireCooldownSeconds)
            {
                _attackTime = Time.time + _config.BulletFireCooldownSeconds;
                _animationPlaying = false;

                Vector3 direction = (_target.position - _transform.position).normalized;
                Vector3 spawnPosition = _bulletSpawnPoint != null ? _bulletSpawnPoint.position : _transform.position;

                var bullet = _bulletSpawner?.SpawnBullet(spawnPosition, direction, _bulletSpawnPoint);
                bullet?.Construct(_config.VirusBulletSpeed, _config.VirusBulletLifetimeSeconds, _config.VirusBulletDamage);
                _audioSystem.PlaySfx(_attackClip);
            }
        }
    }
}
