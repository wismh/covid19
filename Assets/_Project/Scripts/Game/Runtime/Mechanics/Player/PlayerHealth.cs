using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.AudioSystem;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Combat;

namespace _Project.Mechanics.Player
{
    /// <summary>Player health/armor/damage-death presentation (hit flash, death animation, audio).</summary>
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerTurret _turret;

        /// <summary>Deactivated (not just disabled) on death - the nearest-cell-arrow's polling loop runs via
        /// StartCoroutine, which disabling a MonoBehaviour doesn't stop; only deactivating its GameObject does.</summary>
        [SerializeField] private GameObject _arrowRoot;

        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Material _damageMaterial;
        [SerializeField] private SoundData _hitSound;
        [SerializeField] private SoundData _diedSound;

        private PlayerConfig _config;
        private PlayerStatsService _stats;
        private IAudioSystem _audioSystem;

        private CancellationTokenSource _lifetimeCts;

        // Cached once in Awake(), not re-captured per flash - avoids two overlapping flashes racing on which
        // material to revert to.
        private Material _originalMaterial;

        private int _health;
        private int _armor;
        private bool _isAlive = true;

        public int Health => _health;
        public int MaxHealth => _stats.EffectiveMaxHealth;
        public int Armor => _armor;
        public bool IsAlive => _isAlive;

        /// <summary>(damage taken, attack direction) - raised on every hit, whether or not armor ignored it.</summary>
        public event Action<int, Vector3> OnDamaged;

        /// <summary>Raised once health drops to 0 or below, after the death delay has elapsed.</summary>
        public event Action OnDied;

        /// <summary>
        /// HP/armor initialization happens here rather than in Start(): HudScreen reads Health/MaxHealth for its
        /// initial bar fill from <see cref="Composition.GameplayScreensStartupDriver"/>, and Unity does not
        /// guarantee this component's Start() runs before a different GameObject's - only that all [Inject]
        /// Construct calls finish before any driver's Initialize(). Start()-ordering would risk HudScreen reading
        /// the C# default (0) for _health.
        /// </summary>
        [Inject]
        public void Construct(PlayerConfig config, PlayerStatsService stats, IAudioSystem audioSystem)
        {
            _config = config;
            _stats = stats;
            _audioSystem = audioSystem;

            // Intentional starting value (PlayerConfig.StartingHealth), distinct from EffectiveMaxHealth
            // (the separate upgrade ceiling - see PlayerConfig's doc comment).
            _health = _config.StartingHealth;
            _armor = _config.BaseArmor;

            _stats.OnUpgraded += HandleUpgraded;
            _stats.OnBonusHealthGranted += HandleBonusHealthGranted;
        }

        private void Awake()
        {
            _lifetimeCts = new CancellationTokenSource();

            _originalMaterial = _spriteRenderer.material;
        }

        private void OnDestroy()
        {
            _stats.OnUpgraded -= HandleUpgraded;
            _stats.OnBonusHealthGranted -= HandleBonusHealthGranted;

            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        }

        private void HandleUpgraded(UpgradeStat stat, int newLevel)
        {
            if (stat == UpgradeStat.Armor && _armor < _config.MaxArmor)
                _armor += 1;

            if (stat == UpgradeStat.Health && _health < _stats.EffectiveMaxHealth)
                _health += 1;
        }

        private void HandleBonusHealthGranted(int amount)
        {
            _health += amount;
        }

        public void Damage(int damage, Vector3 attackDirection)
        {
            if (!_isAlive)
                return;

            float chanceToIgnore = _armor / _config.ArmorIgnoreChanceDivisor;
            if (UnityEngine.Random.Range(_config.ArmorIgnoreRollMin, _config.ArmorIgnoreRollMax) < chanceToIgnore)
                _armor = Mathf.Max(0, _armor - 1);
            else
                _health -= damage;

            OnDamaged?.Invoke(damage, attackDirection);

            _movement.ApplyImpulse(attackDirection * _config.KnockbackImpulseStrength);

            _audioSystem.PlaySfx(_hitSound);
            PlayDamageFlashAsync().Forget();

            if (_health <= 0)
                HandleDeathAsync().Forget();
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

        private async UniTaskVoid HandleDeathAsync()
        {
            _isAlive = false;

            // Stop movement/aiming/firing/the target arrow the instant death is detected, not after the
            // animation delay below, so the player can't keep playing during it.
            _movement.enabled = false;
            _turret.enabled = false;
            _arrowRoot.SetActive(false);
            _turret.TurretVisualObject.SetActive(false);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_config.DeathAnimationDelaySeconds), cancellationToken: _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _audioSystem.PlaySfx(_diedSound);
            _animator.Play("Base Layer.Died");

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_config.DeathToGameOverDelaySeconds), cancellationToken: _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            OnDied?.Invoke();
        }
    }
}
