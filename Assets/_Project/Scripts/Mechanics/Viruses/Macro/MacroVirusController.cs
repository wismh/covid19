using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Cells;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>
    /// Ports GMTK.VirusController (Assets/GMTK/Scripts/Behaviors/MacroVirus/VirusController.cs) - the
    /// three-state strategy host (MoveToCell/AttackCell/PenetrationIntoCell) plus health/damage. No
    /// ServiceLocator, no transform.Find; behavior dependencies arrive via constructor injection at the call
    /// site (see IMacroVirusBehavior/ICellTargetProvider/IDriftBody doc comments).
    ///
    /// Implements GMTK.IDamageable unchanged for interop with old-code collision handlers (old GMTK.Bullet.
    /// OnTriggerEnter2D does `TryGetComponent(out IDamageable damageable)`) - same reasoning as Phase 2's
    /// PlayerHealth: REFACTORING_PLAN.md section 5 keeps IDamageable as-is ("Добрий патерн - залишити майже без
    /// змін, DI-конструктор замість ServiceLocator.Get&lt;T&gt;() всередині").
    ///
    /// Also implements <see cref="IDriftBody"/> directly (see that interface's doc comment for why this control
    /// ler owns velocity state itself instead of composing a separate movement component).
    ///
    /// XP seam: old VirusController.Damage() called `_playerStats.AddExperience()` directly via a
    /// ServiceLocator-resolved GMTK.Services.PlayerStats on death. This class raises <see cref="OnKilled"/>
    /// instead and grants no XP itself - XP-granting is a composition-root concern for a later phase (mirrors
    /// how Phase 2 kept PlayerStatsService decoupled from anything Player-specific outside itself). Likewise,
    /// old code called `_virusManager.RemoveObject(transform)` then `Destroy(gameObject, 1)` on death, and
    /// `VirusManager.RemoveObject` + `Object.Destroy` again on successful penetration (see
    /// PenetrationIntoCellBehavior) - both manager-list-removal and GameObject lifecycle (Destroy today, a
    /// pooled SetActive(false) later per REFACTORING_PLAN.md section 4) are left to whatever listens to
    /// <see cref="OnKilled"/>/<see cref="OnPenetratedIntoCell"/>, not decided here.
    ///
    /// Damage/death AUDIO is ported (see <see cref="_audioSources"/>'s doc comment, Phase 7 testing fix).
    ///
    /// --- Death animation (Phase 8 testing fix, 2026-09-11) ---
    /// The Animator half (old: `_animator.Play("Base Layer.Died")` on death) was left unported by the previous
    /// round pending verification of BigBob.prefab/PinkGlist.prefab's actual clip/state names - confirmed live
    /// via the Editor: both controllers (BigBob.controller/PinkGlist.controller) have a "Died" state under "Base
    /// Layer", exactly matching the old hardcoded string, so it's safe to restore verbatim. Wired via
    /// <see cref="_animator"/> (the child "Sprite" GameObject's Animator, same object <see cref="_spriteRenderer"/>
    /// already points to - old code fetched it via `GetComponentInChildren&lt;Animator&gt;()`, this uses an
    /// explicit reference instead, matching every other typed-lookup convention this migration established).
    ///
    /// --- Why HandleDeathAsync waits again after playing the animation ---
    /// MacroVirusSpawner.HandleVirusRemoved (subscribed to <see cref="OnKilled"/>) destroys the GameObject
    /// synchronously the instant OnKilled fires - so any animation/audio triggered in the same breath as
    /// OnKilled.Invoke() would never actually be seen or heard, cut off mid-frame. Old code decoupled this
    /// entirely: `Destroy(gameObject, 1f)` was scheduled once, immediately, at the moment health hit 0, running
    /// in parallel with (not gated behind) the death animation/audio - giving the "Died" clip ~0.8s of visible
    /// screen time (1f total minus the 0.2f AnimationOnDied delay) before the object actually disappeared. This
    /// class reproduces that same ~0.8s visible window the only way an event-driven single lifecycle owner can:
    /// waiting for it before raising the event that triggers destruction, rather than destroying and animating
    /// as two independent timers like old code did.
    /// </summary>
    public sealed class MacroVirusController : MonoBehaviour, GMTK.IDamageable, IDriftBody
    {
        // Old: VirusController._maxHealth (serialized per-prefab-instance, not a shared config value -
        // BigBob.prefab: 3, PinkGlist.prefab: 4). Kept as a per-instance field here for the same reason: the two
        // macro-virus variants share every DriftMovableObject/behavior number (see MacroVirusConfig) but differ
        // only in health and penetration delay, exactly like old code serialized them directly on the component
        // rather than in a shared asset. Default matches BigBob.prefab; a PinkGlist-equivalent prefab created in
        // a later phase would override this to 4.
        [SerializeField] private int _maxHealth = 3;

        // Old: VirusController._delayBeforePenetration (BigBob.prefab: 10, PinkGlist.prefab: 8). Same per-instance
        // reasoning as _maxHealth above.
        [SerializeField] private float _delayBeforePenetrationSeconds = 10f;

        [SerializeField] private Collider2D _collider;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Material _damageMaterial;

        /// <summary>See class doc comment's "Death animation" section. Child "Sprite" GameObject's Animator.</summary>
        [SerializeField] private Animator _animator;

        private MacroVirusConfig _config;
        private ICellTargetProvider _cellTargetProvider;

        private Vector3 _velocity;
        private float _angularVelocity;

        private IMacroVirusBehavior _behavior;
        private int _health;
        private bool _isAlive = true;

        private CancellationTokenSource _lifetimeCts;

        // Old: VirusController._audioSources = GetComponents<AudioSource>() (root-only, matching Animator/
        // AudioSource layout on BigBob.prefab/PinkGlist.prefab); [0].Play() on every hit (AnimationOnDamage),
        // [1].Play() on death (AnimationOnDied). Neither prefab's AudioSource count changed since old
        // VirusController was removed (Phase 7 testing fix, 2026-09-11 - see that removal's commit message), so
        // the same array-index pairing still holds; resolved here rather than adding two new [SerializeField]s
        // to avoid re-wiring both prefabs for something GetComponents already finds unambiguously.
        private AudioSource[] _audioSources;

        public bool IsAlive => _isAlive;

        /// <summary>Raised on death by damage (old: VirusController.Damage(), the `_health &lt;= 0` branch).</summary>
        public event Action OnKilled;

        /// <summary>Raised once this virus finishes penetrating a cell (old: PenetrationIntoCell.OnAnimationEnd).</summary>
        public event Action OnPenetratedIntoCell;

        [Inject]
        public void Construct(MacroVirusConfig config, ICellTargetProvider cellTargetProvider)
        {
            _config = config;
            _cellTargetProvider = cellTargetProvider;
        }

        private void Awake()
        {
            _lifetimeCts = new CancellationTokenSource();
            _audioSources = GetComponents<AudioSource>();
        }

        private void Start()
        {
            _health = _maxHealth;

            // Old: VirusController.Start() -> `ChangeBehavior(new MoveToCell());`
            SwitchBehavior(new MoveToCellBehavior(_cellTargetProvider, this, _config));
        }

        private void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        }

        private void Update()
        {
            transform.position += Time.deltaTime * _velocity;
            transform.Rotate(0f, 0f, Time.deltaTime * _angularVelocity);

            // Phase 7 testing fix (2026-09-11): old VirusController.Update() also did
            // `transform.GetChild(0).transform.rotation = Quaternion.Euler(0,0,0);` every frame - a billboard-
            // style counter-rotation so the visible sprite stays upright while the body (and its collider) still
            // drifts/turns freely. Not ported in Phase 3 (no prefab existed yet to reference); BigBob.prefab/
            // PinkGlist.prefab (Phase 7 Stage 3) always wire _spriteRenderer, so this restores it against that
            // reference directly instead of a magic-index GetChild(0).
            if (_spriteRenderer != null)
                _spriteRenderer.transform.rotation = Quaternion.identity;
        }

        private void FixedUpdate()
        {
            // Old: VirusController.FixedUpdate() ->
            // `if (_behavior is AttackCell attackBehavior && !attackBehavior.Target) { ChangeBehavior(new MoveToCell()); return; }`
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

            // Old: `if (other.gameObject.TryGetComponent(out CellController cell) && cell.IsAlive)`
            if (other.TryGetComponent(out CellHealth cellHealth) && cellHealth.IsAlive)
            {
                SwitchBehavior(new AttackCellBehavior(cellHealth.transform, this, _config));
                StartPenetrationAfterDelayAsync(cellHealth).Forget();
            }
        }

        /// <summary>Old: VirusController.StartPenetrationAfterDelay() coroutine, ported to UniTask per
        /// REFACTORING_PLAN.md section 4/5 ("Корутини -> UniTask").</summary>
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

            // Old: `if (cell && cell.GetComponent<CellController>().IsAlive) ChangeBehavior(new PenetrationIntoCell(cell));`
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

        void GMTK.IDamageable.Damage(int damage, Vector3 attackDirection)
        {
            if (!_isAlive)
                return;

            // Old: VirusController.Damage() -> `_health -= damage;`
            _health -= damage;

            // Old: AnimationOnDamage() -> `_body.Impulse(attackDirection);` (unscaled, unlike PlayerHealth's
            // knockback which multiplies by 0.3 - preserved exactly as the old macro-virus formula).
            Impulse(attackDirection);
            PlayDamageFlashAsync().Forget();

            // Old: AnimationOnDamage() -> `_audioSources[0].Play();`, unconditional (not tied to the material-
            // flash's own null-guards on _spriteRenderer/_damageMaterial - it's a separate concern).
            PlayAudioIfAvailable(0);

            if (_health <= 0)
                HandleDeathAsync().Forget();
        }

        /// <summary>Mirrors old VirusController's `_audioSources[index].Play()` calls (AnimationOnDamage: index
        /// 0, AnimationOnDied: index 1) - see <see cref="_audioSources"/>'s own doc comment.</summary>
        private void PlayAudioIfAvailable(int index)
        {
            if (_audioSources != null && index < _audioSources.Length && _audioSources[index] != null)
                _audioSources[index].Play();
        }

        /// <summary>Old: VirusController.Damage() death branch + AnimationOnDied() coroutine (0.2s delay before
        /// the death animation/audio played).</summary>
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

            // See class doc comment's "Why HandleDeathAsync waits again" section - gives the death animation/
            // audio time to actually be seen/heard before OnKilled triggers GameObject destruction.
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

        /// <summary>Old: VirusController.AnimationOnDamage() - swaps in the damage material for 0.1s.</summary>
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

        // --- IDriftBody: fresh port of GMTK.DriftMovableObject.Move/Rotate/Impulse, see IDriftBody doc comment. ---

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
    }
}
