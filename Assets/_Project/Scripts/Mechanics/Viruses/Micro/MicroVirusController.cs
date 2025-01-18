using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Combat;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>Per-instance host for the three <see cref="IMicroVirusBehavior"/> strategies, plus health/damage
    /// and melee contact damage against the player. Pooled via a Zenject <see cref="IMemoryPool"/> keyed by
    /// <see cref="MicroVirusVariant"/>, so per-life reset happens in <see cref="OnSpawned"/> (Start() only runs
    /// once per GameObject, not once per respawn).</summary>
    public sealed class MicroVirusController : MonoBehaviour, IDamageable, IPoolable<IMemoryPool>
    {
        [SerializeField] private MicroVirusConfig _config;
        [SerializeField] private Collider2D _collider;
        [SerializeField] private Rigidbody2D _rigidbody2D;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Material _damageMaterial;

        // Cucumber-only; left null for Orange/Spiner variants.
        [SerializeField] private Transform _bulletSpawnPoint;
        [SerializeField] private AudioSource _attackAudioSource;

        [SerializeField] private _Project.Mechanics.Combat.VirusBulletSpawner _bulletSpawner;

        private IPlayerTargetProvider _playerTargetProvider;
        private IMicroVirusBehavior _behavior;
        private Transform _playerTransform;

        // [0] plays on hit, [1] plays on death - resolved once (not per-spawn) since these don't change across a
        // pooled instance's lifetime.
        private AudioSource[] _audioSources;
        private Animator _animator;

        private int _health;

        // Deliberately NOT initialized to true: a never-yet-spawned pool reservist must read as not-alive, or
        // IDamageable.Damage's guard would let damage land on inert pool stock. OnSpawned sets this true on
        // every real spawn instead.
        private bool _isAlive;
        private float _lastCollisionDamageTime = float.NegativeInfinity;

        private CancellationTokenSource _lifetimeCts;
        private IMemoryPool _pool;
        private bool _componentsResolved;

        // Cached once per spawn, not re-captured inside PlayDamageFlashAsync - two overlapping flashes would
        // otherwise race on which material to revert to and can get stuck fully white.
        private Material _originalMaterial;

        public bool IsAlive => _isAlive;

        /// <summary>Raised on death by damage. XP-granting/GameObject lifecycle are left to whatever listens to
        /// this, not decided here.</summary>
        public event Action OnKilled;

        [Inject]
        public void Construct(IPlayerTargetProvider playerTargetProvider)
        {
            _playerTargetProvider = playerTargetProvider;
        }

        /// <summary>Runs on every (re)spawn from the pool, not just the first - folds together what would
        /// otherwise be split across Awake()/OnEnable()/Start(), none of which re-run on pool reactivation.</summary>
        public void OnSpawned(IMemoryPool pool)
        {
            _pool = pool;

            if (!_componentsResolved)
            {
                _audioSources = GetComponents<AudioSource>();
                _animator = GetComponent<Animator>();
                _componentsResolved = true;
            }

            _lifetimeCts = new CancellationTokenSource();
            _isAlive = true;
            _lastCollisionDamageTime = float.NegativeInfinity;
            _health = _config.MaxHealth;

            _collider.enabled = true;

            _spriteRenderer.material = _spriteRenderer.sharedMaterial;
            _originalMaterial = _spriteRenderer.material;

            _playerTransform = _playerTargetProvider.PlayerTransform;
            _behavior = BuildBehavior(_playerTransform);
            _behavior.Init(transform);
            _behavior.OnEnable();
        }

        public void OnDespawned()
        {
            _pool = null;
            _isAlive = false;

            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
            _lifetimeCts = null;
        }

        /// <summary>Replaces <c>Destroy(gameObject)</c> for this pooled type - see MicroVirusSpawner.HandleVirusKilled.</summary>
        public void Despawn()
        {
            _pool?.Despawn(this);
        }

        private void Update()
        {
            if (!_isAlive)
                return;

            _behavior?.Update();
        }

        // Guarded on IsAlive so a behavior like CucumberVirusBehavior's attack loop can't keep firing/animating
        // during the death delay between Damage() detecting lethal damage and OnKilled actually despawning it.
        private void FixedUpdate()
        {
            if (!_isAlive)
                return;

            _behavior?.FixedUpdate();
        }

        private IMicroVirusBehavior BuildBehavior(Transform target)
        {
            switch (_config.Variant)
            {
                case MicroVirusVariant.Orange:
                    return new OrangeVirusBehavior(_config, target);
                case MicroVirusVariant.Cucumber:
                    return new CucumberVirusBehavior(_config, target, _bulletSpawnPoint, _attackAudioSource, _bulletSpawner);
                case MicroVirusVariant.Spiner:
                    return new SpinerVirusBehavior(_config, target);
                default:
                    throw new ArgumentOutOfRangeException(nameof(_config.Variant), _config.Variant, null);
            }
        }

        /// <summary>Restricted to the known player Transform rather than a generic
        /// TryGetComponent&lt;IDamageable&gt;() - so it can't accidentally fire against other IDamageable
        /// implementers (other viruses).</summary>
        private void OnCollisionStay2D(Collision2D other)
        {
            if (_playerTransform == null || other.transform != _playerTransform)
                return;

            if (Time.time < _lastCollisionDamageTime + _config.CollisionDamageCooldownSeconds)
                return;

            if (other.gameObject.TryGetComponent(out IDamageable damageable))
            {
                _lastCollisionDamageTime = Time.time;
                damageable.Damage(_config.MeleeDamage, (other.transform.position - transform.position).normalized);
            }
        }

        void IDamageable.Damage(int damage, Vector3 attackDirection)
        {
            if (!_isAlive)
                return;

            _health -= damage;

            _rigidbody2D.AddForce(attackDirection * _config.KnockbackForceOnHit, ForceMode2D.Impulse);

            PlayDamageFlashAsync().Forget();
            PlayAudioIfAvailable(0);

            if (_health <= 0)
                HandleDeathAsync().Forget();
        }

        private void PlayAudioIfAvailable(int index)
        {
            if (_audioSources != null && index < _audioSources.Length && _audioSources[index] != null)
                _audioSources[index].Play();
        }

        private async UniTaskVoid HandleDeathAsync()
        {
            _isAlive = false;

            _collider.enabled = false;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_config.DeathAnimationDelaySeconds), cancellationToken: _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _animator?.Play("Base Layer.Died");
            PlayAudioIfAvailable(1);

            // Waits before raising OnKilled (which despawns this back into the pool) so the death animation/audio
            // actually has time to be seen/heard first.
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_config.DeathToKillDelaySeconds), cancellationToken: _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            OnKilled?.Invoke();
        }

        private async UniTaskVoid PlayDamageFlashAsync()
        {
            _spriteRenderer.material = _damageMaterial;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_config.DamageFlashDurationSeconds), cancellationToken: _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _spriteRenderer.material = _originalMaterial;
        }

        public class Factory : PlaceholderFactory<MicroVirusController>
        {
        }
    }
}
