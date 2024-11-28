using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace _Project.Mechanics.Viruses.InCell
{
    /// <summary>
    /// Ports GMTK.MicroViruses.InCellVirus (Assets/GMTK/Scripts/Behaviors/MicroVirus/InCellVirus.cs) - the
    /// per-instance host for the three IInCellVirusBehavior strategies, plus health/damage and melee contact
    /// damage against the player. No ServiceLocator, no transform.Find/GetChild - direct [SerializeField]
    /// references and Zenject method injection instead.
    ///
    /// Implements GMTK.IDamageable unchanged for interop, same reasoning as PlayerHealth (Phase 2) and
    /// MacroVirusController above.
    ///
    /// XP seam: old InCellVirus.Damage() called `_playerStats.AddExperience()` directly on death. This class
    /// raises <see cref="OnKilled"/> instead - XP-granting deferred to a composition root, matching
    /// MacroVirusController.OnKilled. Old code's `_virusManager.RemoveObject(transform)` +
    /// `Destroy(gameObject, 1)` are likewise left to whatever listens to OnKilled (manager-list removal /
    /// GameObject lifecycle, per REFACTORING_PLAN.md section 4's pooling roadmap - see MacroVirusController's
    /// doc comment for the same reasoning).
    ///
    /// Ordering note (flagged for double-checking - see also the Phase 3 completion report): old InCellVirus
    /// built its VirusBehavior in Awake() (a plain `switch` on the serialized _type field, no external
    /// dependency needed for construction itself), then called `_virusBehavior.OnEnable()` in OnEnable() -
    /// BEFORE `_virusBehavior.Init(transform)` ran in Start() (Unity's Awake -> OnEnable -> Start order on first
    /// activation). That ordering was safe there because OnEnable()'s logic (resetting `_attackTime`/
    /// `_animationPlaying`) never touches anything Init() sets up. Here, behavior construction additionally
    /// needs <see cref="IPlayerTargetProvider"/> from `[Inject] Construct(...)`, whose timing relative to Awake
    /// isn't verified without a live Editor/scene (Zenject method injection can run before or after Awake
    /// depending on how the composition root wires it) - so construction AND Init() are both deferred to
    /// Start() together instead of splitting them across Awake/Start like old code. OnEnable() calls
    /// `_behavior?.OnEnable()` defensively to stay safe if it fires before Start() on first activation.
    /// </summary>
    public sealed class InCellVirusController : MonoBehaviour, GMTK.IDamageable
    {
        [SerializeField] private InCellVirusConfig _config;
        [SerializeField] private Collider2D _collider;
        [SerializeField] private Rigidbody2D _rigidbody2D;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Material _damageMaterial;

        // Cucumber-only wiring (old: `transform.GetChild(0)` for both - fixed per TECHNICAL_AUDIT.md section 2.2,
        // same as CellVisuals's entrails fix). Left null for Orange/Spiner variants.
        [SerializeField] private Transform _bulletSpawnPoint;
        [SerializeField] private AudioSource _attackAudioSource;

        // Typed as the concrete bridge class (Unity can't serialize interface-typed fields), used through
        // IVirusBulletSpawner in code - same pattern as PlayerTurret's _projectileSpawner field.
        [SerializeField] private GmtkVirusBulletPrefabSpawner _bulletSpawner;

        private IPlayerTargetProvider _playerTargetProvider;
        private IInCellVirusBehavior _behavior;
        private Transform _playerTransform;

        private int _health;
        private bool _isAlive = true;
        private float _lastCollisionDamageTime = float.NegativeInfinity;

        private CancellationTokenSource _lifetimeCts;

        public bool IsAlive => _isAlive;

        /// <summary>Raised on death by damage (old: InCellVirus.Damage(), the `_health &lt;= 0` branch).</summary>
        public event Action OnKilled;

        [Inject]
        public void Construct(IPlayerTargetProvider playerTargetProvider)
        {
            _playerTargetProvider = playerTargetProvider;
        }

        private void Awake()
        {
            _lifetimeCts = new CancellationTokenSource();

            // Old: InCellVirus.Awake() -> `_health = _maxHealth;`
            _health = _config.MaxHealth;
        }

        private void OnEnable()
        {
            // Old: InCellVirus.OnEnable() -> `_virusBehavior.OnEnable();` - see class doc comment for the
            // Awake/OnEnable/Start ordering note.
            _behavior?.OnEnable();
        }

        private void Start()
        {
            _playerTransform = _playerTargetProvider?.PlayerTransform;
            _behavior = BuildBehavior(_playerTransform);
            _behavior.Init(transform);
            _behavior.OnEnable();
        }

        private void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        }

        private void Update()
        {
            _behavior?.Update();
        }

        private void FixedUpdate()
        {
            _behavior?.FixedUpdate();
        }

        /// <summary>Old: InCellVirus.Awake()'s `switch (_type)` statement.</summary>
        private IInCellVirusBehavior BuildBehavior(Transform target)
        {
            switch (_config.Variant)
            {
                case InCellVirusVariant.Orange:
                    return new OrangeVirusBehavior(_config, target);
                case InCellVirusVariant.Cucumber:
                    return new CucumberVirusBehavior(_config, target, _bulletSpawnPoint, _attackAudioSource, _bulletSpawner);
                case InCellVirusVariant.Spiner:
                    return new SpinerVirusBehavior(_config, target);
                default:
                    throw new ArgumentOutOfRangeException(nameof(_config.Variant), _config.Variant, null);
            }
        }

        /// <summary>
        /// Old: InCellVirus.OnCollisionStay2D() -> `if (other.gameObject.TryGetComponent(out PlayerDamageable
        /// damageable)) { ... damageable.Damage(...); }`. Restricted to the known player Transform (rather than a
        /// generic `TryGetComponent&lt;IDamageable&gt;()`) to preserve that old specificity - a generic
        /// IDamageable check would also fire against other IDamageable implementers (MacroVirusController, other
        /// InCellVirusController instances), which old code never did since it checked the concrete
        /// PlayerDamageable type. Still resolves damage through GMTK.IDamageable so it works whether the player
        /// GameObject carries the old PlayerDamageable or the new PlayerHealth.
        /// </summary>
        private void OnCollisionStay2D(Collision2D other)
        {
            if (_playerTransform == null || other.transform != _playerTransform)
                return;

            if (Time.time < _lastCollisionDamageTime + _config.CollisionDamageCooldownSeconds)
                return;

            if (other.gameObject.TryGetComponent(out GMTK.IDamageable damageable))
            {
                _lastCollisionDamageTime = Time.time;
                damageable.Damage(_config.MeleeDamage, (other.transform.position - transform.position).normalized);
            }
        }

        void GMTK.IDamageable.Damage(int damage, Vector3 attackDirection)
        {
            if (!_isAlive)
                return;

            // Old: InCellVirus.Damage() -> `_health -= damage;`
            _health -= damage;

            // Old: AnimationOnDamage() -> `if (transform.TryGetComponent(out Rigidbody2D rd)) rd.AddForce(attackDirection * 0.8f, ForceMode2D.Impulse);`
            if (_rigidbody2D != null)
                _rigidbody2D.AddForce(attackDirection * 0.8f, ForceMode2D.Impulse);

            PlayDamageFlashAsync().Forget();

            if (_health <= 0)
                HandleDeathAsync().Forget();
        }

        /// <summary>Old: InCellVirus.Damage() death branch + AnimationOnDied() coroutine (0.2s delay before the
        /// death animation/audio played).</summary>
        private async UniTaskVoid HandleDeathAsync()
        {
            _isAlive = false;

            if (_collider != null)
                _collider.enabled = false;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            OnKilled?.Invoke();
        }

        /// <summary>Old: InCellVirus.AnimationOnDamage() - swaps in the damage material for 0.1s.</summary>
        private async UniTaskVoid PlayDamageFlashAsync()
        {
            if (_spriteRenderer == null || _damageMaterial == null)
                return;

            Material original = _spriteRenderer.material;
            _spriteRenderer.material = _damageMaterial;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _spriteRenderer.material = original;
        }
    }
}
