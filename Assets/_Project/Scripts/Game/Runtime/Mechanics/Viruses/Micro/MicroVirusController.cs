using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.AudioSystem;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Combat;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>Per-instance host for an <see cref="IMicroVirusBehavior"/> strategy, plus health/damage and melee
    /// contact damage against the player. Pooled via a Zenject <see cref="IMemoryPool"/> keyed by
    /// <see cref="MicroVirusVariant"/>, so per-life reset happens in <see cref="OnSpawned"/> (Start() only runs
    /// once per GameObject, not once per respawn). Each variant is its own sealed subclass
    /// (<see cref="OrangeVirusController"/>/<see cref="CucumberVirusController"/>/<see cref="SpinerVirusController"/>)
    /// instead of one shared class switching on <see cref="MicroVirusVariant"/> - keeps each variant's own fields
    /// (e.g. Cucumber's bullet spawn point) off the other variants entirely.</summary>
    public abstract class MicroVirusController : MonoBehaviour, IDamageable, IPoolable<IMemoryPool>
    {
        [SerializeField] private Collider2D _collider;
        [SerializeField] private Rigidbody2D _rigidbody2D;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Material _damageMaterial;

        [SerializeField] private SoundData _hitSound;
        [SerializeField] private SoundData _deathSound;

        [SerializeField] private Animator _animator;

        private IPlayerTargetProvider _playerTargetProvider;
        private IAudioSystem _audioSystem;
        private IMicroVirusBehavior _behavior;
        private Transform _playerTransform;

        private DamageFlashEffect _damageFlash;
        private PooledEntityDeathSequence _deathSequence;

        private int _health;

        // Deliberately NOT initialized to true: a never-yet-spawned pool reservist must read as not-alive, or
        // IDamageable.Damage's guard would let damage land on inert pool stock. OnSpawned sets this true on
        // every real spawn instead.
        private bool _isAlive;
        private float _lastCollisionDamageTime = float.NegativeInfinity;

        private CancellationTokenSource _lifetimeCts;
        private IMemoryPool _pool;

        protected Rigidbody2D Rigidbody2D => _rigidbody2D;
        protected Animator Animator => _animator;
        protected IAudioSystem AudioSystem => _audioSystem;

        /// <summary>Backed by each subclass's own strongly-typed <c>[SerializeField]</c> config asset - keeps the
        /// Inspector slot type-checked (e.g. an OrangeVirusController can't accept a SpinerVirusConfig) without a
        /// generic base class.</summary>
        protected abstract MicroVirusConfig Config { get; }

        public bool IsAlive => _isAlive;

        /// <summary>Raised on death by damage. XP-granting/GameObject lifecycle are left to whatever listens to
        /// this, not decided here.</summary>
        public event Action OnKilled;

        [Inject]
        public void Construct(IPlayerTargetProvider playerTargetProvider, IAudioSystem audioSystem, DamageDeathPresentationConfig presentation)
        {
            _playerTargetProvider = playerTargetProvider;
            _audioSystem = audioSystem;

            _damageFlash = new DamageFlashEffect(_spriteRenderer, _damageMaterial, presentation.DamageFlashDurationSeconds);
            _deathSequence = new PooledEntityDeathSequence(_collider, _animator, _audioSystem, _deathSound, presentation.DeathAnimationDelaySeconds, Config.DeathToKillDelaySeconds);
        }

        /// <summary>Runs on every (re)spawn from the pool. </summary>
        public void OnSpawned(IMemoryPool pool)
        {
            _pool = pool;

            _lifetimeCts = new CancellationTokenSource();
            _isAlive = true;
            _lastCollisionDamageTime = float.NegativeInfinity;
            _health = Config.MaxHealth;

            _collider.enabled = true;

            _damageFlash.ResetForSpawn();

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

        protected abstract IMicroVirusBehavior BuildBehavior(Transform target);

        /// <summary>Restricted to the known player Transform rather than a generic
        /// TryGetComponent&lt;IDamageable&gt;() - so it can't accidentally fire against other IDamageable
        /// implementers (other viruses).</summary>
        private void OnCollisionStay2D(Collision2D other)
        {
            if (_playerTransform == null || other.transform != _playerTransform)
                return;

            if (Time.time < _lastCollisionDamageTime + Config.CollisionDamageCooldownSeconds)
                return;

            if (other.gameObject.TryGetComponent(out IDamageable damageable))
            {
                _lastCollisionDamageTime = Time.time;
                damageable.Damage(Config.MeleeDamage, (other.transform.position - transform.position).normalized);
            }
        }

        void IDamageable.Damage(int damage, Vector3 attackDirection)
        {
            if (!_isAlive)
                return;

            _health -= damage;

            _rigidbody2D.AddForce(attackDirection * Config.KnockbackForceOnHit, ForceMode2D.Impulse);

            _damageFlash.PlayAsync(_lifetimeCts.Token).Forget();
            _audioSystem.PlaySfx(_hitSound);

            if (_health <= 0)
            {
                _isAlive = false;
                _deathSequence.RunAsync(_lifetimeCts.Token, () => OnKilled?.Invoke()).Forget();
            }
        }

        public class Factory : PlaceholderFactory<MicroVirusController>
        {
        }
    }
}
