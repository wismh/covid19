using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Combat;

namespace _Project.Mechanics.Viruses.InCell
{
    /// <summary>
    /// Ports GMTK.MicroViruses.InCellVirus (Assets/GMTK/Scripts/Behaviors/MicroVirus/InCellVirus.cs) - the
    /// per-instance host for the three IInCellVirusBehavior strategies, plus health/damage and melee contact
    /// damage against the player. No ServiceLocator, no transform.Find/GetChild - direct [SerializeField]
    /// references and Zenject method injection instead.
    ///
    /// Implements IDamageable.
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
    ///
    /// --- Damage/death audio + death animation (Phase 8 testing fix, 2026-09-11) ---
    /// Old InCellVirus.AnimationOnDamage()/AnimationOnDied() played `_audioSources[0]`/`[1]` (hit/death, via
    /// `GetComponents&lt;AudioSource&gt;()` on Awake) and `_animator.Play("Base Layer.Died")` on death - none of
    /// that survived the initial port, so kills were silent and animation-less. Restored the same way
    /// MacroVirusController already restored ITS audio half: <see cref="_audioSources"/>/<see cref="_animator"/>
    /// resolved via GetComponent(s) in Awake() rather than new [SerializeField]s, since Orange/Cucumber/Spiner.
    /// prefab already carry a root-level Animator + exactly two AudioSources (index 0 "EnemyHit", index 1 the
    /// variant's own "...Die" clip) in that same order - confirmed live via the Editor, not guessed. "Died" is
    /// each variant's actual Animator state name too (Orange/Cucmber/Spiner.controller all have a "Died" state
    /// under "Base Layer"), matching the old hardcoded string exactly.
    ///
    /// See MacroVirusController's own "Why HandleDeathAsync waits again" doc comment for why
    /// <see cref="HandleDeathAsync"/> waits an extra beat after triggering the animation/audio before raising
    /// <see cref="OnKilled"/> - identical reasoning, InCellVirusSpawner.HandleVirusKilled destroys the
    /// GameObject synchronously the instant OnKilled fires, same as MacroVirusSpawner.HandleVirusRemoved.
    /// </summary>
    public sealed class InCellVirusController : MonoBehaviour, IDamageable
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

        [SerializeField] private _Project.Mechanics.Combat.VirusBulletSpawner _bulletSpawner;

        private IPlayerTargetProvider _playerTargetProvider;
        private IInCellVirusBehavior _behavior;
        private Transform _playerTransform;

        // See class doc comment's "Damage/death audio + death animation" section.
        private AudioSource[] _audioSources;
        private Animator _animator;

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

            // See class doc comment's "Damage/death audio + death animation" section.
            _audioSources = GetComponents<AudioSource>();
            _animator = GetComponent<Animator>();
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
            // See FixedUpdate's doc comment - same reasoning applies here too.
            if (!_isAlive)
                return;

            _behavior?.Update();
        }

        /// <summary>
        /// Phase 9 testing fix (2026-09-12): neither this nor Update() above used to check <see cref="_isAlive"/>
        /// - the behavior kept running for the full ~1s HandleDeathAsync delay between death being detected and
        /// OnKilled actually destroying the GameObject (see that method's own doc comment for why that delay
        /// exists - giving the death animation/audio time to play). For CucumberVirusBehavior specifically, its
        /// own attack-cooldown logic doesn't know or care that the virus is already dead, so it kept firing
        /// `_animator.Play("Base Layer.Attack")` (and even spawning bullets) during that window - user-reported
        /// symptom: a dying Cucumber's death animation would play, then visibly flip back to the attack
        /// animation (or freeze on whatever frame that left it on) for the remainder of the death delay before
        /// finally disappearing. Guarding both loops on IsAlive stops the behavior dead in its tracks the instant
        /// Damage() detects lethal damage, leaving the "Died" animation HandleDeathAsync plays 0.2s later
        /// undisturbed for the rest of the delay.
        /// </summary>
        private void FixedUpdate()
        {
            if (!_isAlive)
                return;

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

            // Old: InCellVirus.Damage() -> `_health -= damage;`
            _health -= damage;

            // Old: AnimationOnDamage() -> `if (transform.TryGetComponent(out Rigidbody2D rd)) rd.AddForce(attackDirection * 0.8f, ForceMode2D.Impulse);`
            if (_rigidbody2D != null)
                _rigidbody2D.AddForce(attackDirection * 0.8f, ForceMode2D.Impulse);

            PlayDamageFlashAsync().Forget();

            // Old: AnimationOnDamage() -> `_audioSources[0].Play();`, unconditional - see class doc comment.
            PlayAudioIfAvailable(0);

            if (_health <= 0)
                HandleDeathAsync().Forget();
        }

        /// <summary>Mirrors MacroVirusController's own helper of the same name/shape.</summary>
        private void PlayAudioIfAvailable(int index)
        {
            if (_audioSources != null && index < _audioSources.Length && _audioSources[index] != null)
                _audioSources[index].Play();
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

            // Old: AnimationOnDied() -> `_animator.Play("Base Layer.Died"); _audioSources[1].Play();`.
            _animator?.Play("Base Layer.Died");
            PlayAudioIfAvailable(1);

            // See class doc comment's "Damage/death audio + death animation" section - gives the death
            // animation/audio time to actually be seen/heard before OnKilled triggers GameObject destruction
            // (InCellVirusSpawner.HandleVirusKilled destroys synchronously the instant OnKilled fires).
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.8f), cancellationToken: _lifetimeCts.Token);
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
