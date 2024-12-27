using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Audio;
using _Project.Mechanics.Combat;

namespace _Project.Mechanics.Player
{
    /// <summary>
    /// Ports GMTK.PlayerDamageable (Assets/GMTK/Scripts/Behaviors/Player/PlayerDamageable.cs). Implements the
    /// IDamageable interface.
    ///
    /// --- Damage/death audio + material flash + death animation (Phase 8 testing fix, 2026-09-11) ---
    /// None of old PlayerDamageable's presentation side effects survived the initial port - HandleDeathAsync's
    /// own earlier doc comment explicitly deferred them ("a later UI/animation phase can subscribe to OnDamaged/
    /// OnDied to restore staged death-animation timing"), but no such phase ever followed up, so hits/death were
    /// silent, effect-less, and animation-less. Restored directly here instead of via a separate subscriber -
    /// matches how MacroVirusController/MicroVirusController already own their own damage/death presentation
    /// rather than delegating it to a listener, now that this class also needs the same
    /// per-instance timing control (see <see cref="HandleDeathAsync"/>).
    ///
    /// Old code's audio went through `GetComponent<AudioList>().Play(i)` (a single shared AudioSource with
    /// a swappable clip list - index 2 "PlayerHit", index 3 "Explosion", confirmed live against Player.prefab's
    /// still-present AudioList component/clips) rather than per-effect AudioSources like the virus classes use -
    /// reused via <see cref="_audioList"/> instead of re-wiring two new AudioSources with duplicate clips.
    /// <see cref="_animator"/>/<see cref="_spriteRenderer"/>/<see cref="_damageMaterial"/> mirror the virus
    /// classes' own fields exactly (child "Body" GameObject's Animator/SpriteRenderer; Damage.mat, the same
    /// shared asset BigBob/PinkGlist/Orange/Cucumber/Spiner already flash) - confirmed "Body"'s Animator
    /// controller (Died.controller) has a "Base Layer.Died" state, matching the old hardcoded string.
    /// </summary>
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        // Direct references instead of the old `transform.Find("Turret")` / GetComponentInChildren lookups.
        [SerializeField] private PlayerMovement _movement; // on-hit knockback impulse, disabled on death
        [SerializeField] private PlayerTurret _turret; // disabled on death

        /// <summary>
        /// Phase 9 testing fix (2026-09-12): user-reported symptom - after dying, the player's Body sprite went
        /// invisible (the "Died" animation clip hides it) but the player could still move, aim, and fire, and
        /// the nearest-infected-cell arrow (old GMTK.ArrowController, still active on Player.prefab's "Arrow"
        /// child) kept pointing and showing. HandleDeathAsync only ever hid the turret's own visual and played
        /// the death animation - it never stopped PlayerMovement/PlayerTurret from still processing input
        /// (neither old PlayerDamageable nor this port's initial version disabled them), and ArrowController's
        /// own StartCoroutine(StartFindTargets(...)) keeps running regardless of the component's enabled state
        /// (a well-known Unity gotcha - disabling a MonoBehaviour does not stop coroutines it already started;
        /// only deactivating its GameObject, or explicitly stopping the coroutine, does). Disabling
        /// PlayerMovement/PlayerTurret (their OnDisable() unsubscribes input, matching how OnEnable() wired it -
        /// see those classes' own lifecycle methods) stops movement/aiming/firing outright; deactivating the
        /// whole "Arrow" GameObject is the only way to actually stop its coroutine too, not just hide it for one
        /// frame until the next FindTarget() tick turns it back on.
        /// </summary>
        [SerializeField] private GameObject _arrowRoot;

        // See class doc comment's "Damage/death audio + material flash + death animation" section.
        [SerializeField] private _Project.Mechanics.Audio.AudioList _audioList;
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Material _damageMaterial;

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

            // Matches the old shipped game exactly: PlayerDamageable.Start() hardcoded `_health = 3;`
            // (TECHNICAL_AUDIT.md section 5) - kept as its own StartingHealth value, distinct from
            // EffectiveMaxHealth (the real, separate upgrade ceiling - see PlayerConfig's doc comment).
            _health = _config.StartingHealth;
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

            // Old: PlayerDamageable.Damage -> `_audioList.Play(2);` (unconditional, same as the virus classes'
            // own hit-sound calls - see class doc comment).
            _audioList?.Play(2);
            PlayDamageFlashAsync().Forget();

            if (_health <= 0)
                HandleDeathAsync().Forget();
        }

        /// <summary>Old: PlayerDamageable.AnimationOnDamage() - swaps in the damage material for 0.1s. Mirrors
        /// MacroVirusController/MicroVirusController's own PlayDamageFlashAsync exactly.</summary>
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

        private async UniTaskVoid HandleDeathAsync()
        {
            _isAlive = false;

            // See _arrowRoot's doc comment - stop movement/aiming/firing and the target arrow the instant death
            // is detected, not after the animation delay below, so the player can't keep playing during it.
            if (_movement != null)
                _movement.enabled = false;

            if (_turret != null)
                _turret.enabled = false;

            if (_arrowRoot != null)
                _arrowRoot.SetActive(false);

            // Old: PlayerDamageable.AnimationOnDied -> `transform.Find("Turret").gameObject.SetActive(false);`
            // - see PlayerTurret.TurretVisualObject's doc comment for why this can't be `_turret.gameObject`
            // (that resolves to the whole player root, not the old code's child "Turret" object - confirmed live,
            // dying used to deactivate the entire Player GameObject instead of just hiding the turret).
            if (_turret != null && _turret.TurretVisualObject != null)
                _turret.TurretVisualObject.SetActive(false);

            // Old: PlayerDamageable.Damage -> `StartCoroutine(AnimationOnDied()); Destroy(gameObject, 2f);` -
            // AnimationOnDied() itself waited 0.2s before playing the death sound/animation. Split into two
            // delays instead of old code's single 2s wait (Phase 8 testing fix, 2026-09-11) so the death
            // sound/animation start at the same relative moment old code did, while OnDied still fires at the
            // same total ~2s mark this class already used (HandleGameOver/game-over UI timing unchanged).
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Old: AnimationOnDied() -> `_audioList.Play(3); ... _animator.Play("Base Layer.Died");`.
            _audioList?.Play(3);
            _animator?.Play("Base Layer.Died");

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1.8f), cancellationToken: _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            OnDied?.Invoke();
        }
    }
}
