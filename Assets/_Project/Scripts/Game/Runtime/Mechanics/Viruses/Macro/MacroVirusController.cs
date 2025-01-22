using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.AudioSystem;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Combat;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>Three-state strategy host (MoveToCell/AttackCell/PenetrationIntoCell) plus health/damage for a
    /// macro virus. Pooled via a Zenject <see cref="IMemoryPool"/> keyed by <see cref="MacroVirusVariant"/>, so
    /// per-life reset happens in <see cref="OnSpawned"/> rather than Awake/Start (which only run once per
    /// GameObject, not once per respawn).</summary>
    public sealed class MacroVirusController : MonoBehaviour, IDamageable, IDriftBody, IPoolable<Vector3, Quaternion, IMemoryPool>
    {
        // Per-instance, not shared config: health/penetration delay are the two numbers that differ between
        // virus variants (everything else lives on MacroVirusConfig).
        [SerializeField] private int _maxHealth = 3;
        [SerializeField] private float _delayBeforePenetrationSeconds = 10f;

        [SerializeField] private Collider2D _collider;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Material _damageMaterial;
        [SerializeField] private Animator _animator;
        [SerializeField] private SoundData _hitSound;
        [SerializeField] private SoundData _deathSound;

        private MacroVirusConfig _config;
        private ICellTargetProvider _cellTargetProvider;
        private IAudioSystem _audioSystem;

        private Vector3 _velocity;
        private float _angularVelocity;

        private IMacroVirusBehavior _behavior;
        private int _health;

        // Deliberately NOT initialized to true: a never-yet-spawned pool reservist must read as not-alive.
        // OnSpawned sets this true on every real spawn instead.
        private bool _isAlive;

        private CancellationTokenSource _lifetimeCts;
        private IMemoryPool _pool;
        private bool _componentsResolved;

        // Captured once at real construction, before PenetrationIntoCellBehavior ever shrinks this transform -
        // restored every respawn so a virus that previously penetrated a cell doesn't spawn back in shrunk.
        private Vector3 _originalScale;

        // Cached once per spawn, not re-captured inside PlayDamageFlashAsync - two overlapping flashes (e.g. two
        // bullets same frame) would otherwise race on which material to revert to and can get stuck fully white.
        private Material _originalMaterial;

        public bool IsAlive => _isAlive;

        /// <summary>Raised on death by damage. XP-granting/GameObject lifecycle are left to whatever listens to
        /// this, not decided here.</summary>
        public event Action OnKilled;

        /// <summary>Raised once this virus finishes penetrating a cell.</summary>
        public event Action OnPenetratedIntoCell;

        [Inject]
        public void Construct(MacroVirusConfig config, ICellTargetProvider cellTargetProvider, IAudioSystem audioSystem)
        {
            _config = config;
            _cellTargetProvider = cellTargetProvider;
            _audioSystem = audioSystem;
        }

        /// <summary>Runs on every (re)spawn from the pool, not just the first - folds together what would
        /// otherwise be split across Awake()/Start(), since Start() never re-runs on pool reactivation.</summary>
        public void OnSpawned(Vector3 position, Quaternion rotation, IMemoryPool pool)
        {
            _pool = pool;

            if (!_componentsResolved)
            {
                _originalScale = transform.localScale;
                _componentsResolved = true;
            }

            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = _originalScale;

            _lifetimeCts = new CancellationTokenSource();
            _isAlive = true;
            _health = _maxHealth;
            _velocity = Vector3.zero;
            _angularVelocity = 0f;

            _collider.enabled = true;

            _spriteRenderer.material = _spriteRenderer.sharedMaterial;
            _originalMaterial = _spriteRenderer.material;

            SwitchBehavior(new MoveToCellBehavior(_cellTargetProvider, this, _config));
        }

        public void OnDespawned()
        {
            _pool = null;
            _isAlive = false;

            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
            _lifetimeCts = null;
        }

        /// <summary>Replaces <c>Destroy(gameObject)</c> for this pooled type - see MacroVirusSpawner.HandleVirusRemoved.</summary>
        public void Despawn()
        {
            _pool?.Despawn(this);
        }

        private void Update()
        {
            transform.position += Time.deltaTime * _velocity;
            transform.Rotate(0f, 0f, Time.deltaTime * _angularVelocity);

            // Counter-rotation keeps the sprite upright (billboard-style) while the body/collider still turns freely.
            _spriteRenderer.transform.rotation = Quaternion.identity;
        }

        private void FixedUpdate()
        {
            if (_behavior is AttackCellBehavior attack && attack.Target == null)
            {
                SwitchBehavior(new MoveToCellBehavior(_cellTargetProvider, this, _config));
                return;
            }

            _behavior.FixedUpdate();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isAlive)
                return;

            if (other.TryGetComponent(out CellHealth cellHealth) && cellHealth.IsAlive)
            {
                SwitchBehavior(new AttackCellBehavior(cellHealth.transform, this, _config));
                StartPenetrationAfterDelayAsync(cellHealth).Forget();
            }
        }

        private async UniTaskVoid StartPenetrationAfterDelayAsync(CellHealth targetCell)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_delayBeforePenetrationSeconds), cancellationToken: _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (targetCell != null && targetCell.IsAlive)
                SwitchBehavior(new PenetrationIntoCellBehavior(targetCell.transform, _config, HandlePenetrationComplete));
        }

        private void HandlePenetrationComplete()
        {
            OnPenetratedIntoCell?.Invoke();
        }

        private void SwitchBehavior(IMacroVirusBehavior behavior)
        {
            _behavior = behavior;
            _behavior.Init(transform);
        }

        void IDamageable.Damage(int damage, Vector3 attackDirection)
        {
            if (!_isAlive)
                return;

            _health -= damage;

            Impulse(attackDirection);
            PlayDamageFlashAsync().Forget();
            _audioSystem.PlaySfx(_hitSound);

            if (_health <= 0)
                HandleDeathAsync().Forget();
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

            _animator.Play("Base Layer.Died");
            _audioSystem.PlaySfx(_deathSound);

            // Waits before raising OnKilled (which triggers GameObject destruction/despawn) so the death
            // animation/audio actually has time to be seen/heard first.
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

        public void Move(float direction)
        {
            _velocity += direction * _config.Acceleration * transform.right;
            _velocity += _config.Friction * -_velocity;
            _velocity = Vector3.ClampMagnitude(_velocity, _config.BaseMoveSpeed);
        }

        public void Rotate(float direction)
        {
            _angularVelocity += direction * _config.AngularAcceleration;
            _angularVelocity += _config.AngularFriction * -Mathf.Sign(_angularVelocity);
            _angularVelocity = Mathf.Clamp(_angularVelocity, -_config.AngularSpeed, _config.AngularSpeed);
        }

        public void Impulse(Vector3 impulse)
        {
            _velocity += impulse;
        }

        public class Factory : PlaceholderFactory<Vector3, Quaternion, MacroVirusController>
        {
        }
    }
}
