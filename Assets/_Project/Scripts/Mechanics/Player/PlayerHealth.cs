using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace _Project.Mechanics.Player
{
    /// <summary>
    /// Ports GMTK.PlayerDamageable (Assets/GMTK/Scripts/Behaviors/Player/PlayerDamageable.cs). Implements the
    /// existing GMTK.IDamageable interface unchanged for interop with old-code collision handlers (e.g.
    /// GMTK.Bullet.OnTriggerEnter2D does `TryGetComponent(out IDamageable damageable)`) rather than forking a
    /// duplicate interface - REFACTORING_PLAN.md section 5 explicitly keeps IDamageable as-is ("добрий патерн -
    /// залишити майже без змін, DI-конструктор замість ServiceLocator.Get&lt;T&gt;() всередині").
    /// </summary>
    public sealed class PlayerHealth : MonoBehaviour, GMTK.IDamageable
    {
        // Direct references instead of the old `transform.Find("Turret")` / GetComponentInChildren lookups.
        [SerializeField] private PlayerMovement _movement; // on-hit knockback impulse
        [SerializeField] private PlayerTurret _turret; // disabled on death

        private PlayerConfig _config;
        private PlayerStatsService _stats;

        private CancellationTokenSource _lifetimeCts;

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
        /// Phase 7 testing fix (2026-09-11): HP/armor initialization and the OnUpgraded/OnBonusHealthGranted
        /// subscriptions used to happen in Start(), which reported "HP still 0" in the HUD - MainSceneComposition
        /// opens HudScreen (whose OnOpen() reads PlayerHealth.Health/MaxHealth for its initial bar fill) from
        /// its OWN Awake(), and Unity does not guarantee this component's Awake()/Start() run before or after a
        /// DIFFERENT GameObject's Awake() - only that ALL Awake()s finish before ANY Start(). So HudScreen's
        /// first read could land before this Start() had ever run, seeing the C# default (0) for _health. Moved
        /// into Construct() instead: MainSceneComposition.ConstructSpawners() calls this synchronously, before
        /// ConstructScreens()/OpenScreen(HudScreen) in the same Awake() call - deterministic regardless of any
        /// cross-GameObject ordering.
        /// </summary>
        [Inject]
        public void Construct(PlayerConfig config, PlayerStatsService stats)
        {
            _config = config;
            _stats = stats;

            // Fixes the old hardcoded-3 bug: GMTK.PlayerDamageable.Start() did `_health = 3;`, ignoring the
            // serialized `_maxHealth` field entirely (TECHNICAL_AUDIT.md section 5). Starts at the configured
            // base max health instead.
            _health = _stats.EffectiveMaxHealth;
            _armor = _config.BaseArmor;

            _stats.OnUpgraded += HandleUpgraded;
            _stats.OnBonusHealthGranted += HandleBonusHealthGranted;
        }

        private void Awake()
        {
            _lifetimeCts = new CancellationTokenSource();
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
            // Old: PlayerDamageable.OnUpgrade -
            // `if (_upgrade == UpgradeType.PLAYER_ARMOR && Armor < 8) Armor += 1;`
            if (stat == UpgradeStat.Armor && _armor < _config.MaxArmor)
                _armor += 1;

            // Old: PlayerDamageable.OnUpgrade -
            // `if (_upgrade == UpgradeType.PLAYER_HP && _health < _maxHealth) _health += 1;`
            if (stat == UpgradeStat.Health && _health < _stats.EffectiveMaxHealth)
                _health += 1;
        }

        private void HandleBonusHealthGranted(int amount)
        {
            // Intentionally uncapped by max health - exact port of the old rule (see
            // PlayerStatsService.OnBonusHealthGranted doc comment for why).
            _health += amount;
        }

        public void Damage(int damage, Vector3 attackDirection)
        {
            if (!_isAlive)
                return;

            // Old: PlayerDamageable.Damage ->
            // `float chanceIgnore = Armor / 6.0f; if (Random.Range(0.1f, 1.0f) < chanceIgnore) Armor -= 1; else _health -= damage;`
            float chanceToIgnore = _armor / _config.ArmorIgnoreChanceDivisor;
            if (UnityEngine.Random.Range(0.1f, 1.0f) < chanceToIgnore)
                _armor = Mathf.Max(0, _armor - 1);
            else
                _health -= damage;

            OnDamaged?.Invoke(damage, attackDirection);

            // Old: PlayerDamageable.AnimationOnDamage -> `_body.Impulse(attackDirection * 0.3f);`
            if (_movement != null)
                _movement.ApplyImpulse(attackDirection * 0.3f);

            if (_health <= 0)
                HandleDeathAsync().Forget();
        }

        private async UniTaskVoid HandleDeathAsync()
        {
            _isAlive = false;

            // Old: PlayerDamageable.AnimationOnDied -> `transform.Find("Turret").gameObject.SetActive(false);`
            // - see PlayerTurret.TurretVisualObject's doc comment for why this can't be `_turret.gameObject`
            // (that resolves to the whole player root, not the old code's child "Turret" object - confirmed live,
            // dying used to deactivate the entire Player GameObject instead of just hiding the turret).
            if (_turret != null && _turret.TurretVisualObject != null)
                _turret.TurretVisualObject.SetActive(false);

            // Old code staggered this across a 0.2s death-animation delay + a further 1s before showing the
            // game-over UI, plus a separate `Destroy(gameObject, 2f)` call - all of it depended on an Animator
            // and a ServiceLocator-resolved HUD that don't exist in this pure-code phase. Simplified to a single
            // explicit ~2s UniTask delay (replacing the old coroutine per REFACTORING_PLAN.md section 4's
            // "Корутини -> UniTask") before raising OnDied; a later UI/animation phase can subscribe to
            // OnDamaged/OnDied to restore staged death-animation timing without PlayerHealth needing to know
            // about Animator/HUD specifics.
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(2f), cancellationToken: _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            OnDied?.Invoke();
        }
    }
}
