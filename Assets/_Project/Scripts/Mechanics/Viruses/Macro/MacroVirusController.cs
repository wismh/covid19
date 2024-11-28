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
    /// Not ported (out of scope for pure-code Phase 3, no prefab/hierarchy exists to reference): old
    /// VirusController.Update() reset a child sprite transform's rotation every frame
    /// (`transform.GetChild(0).transform.rotation = Quaternion.Euler(0,0,0);`, a billboard-style
    /// counter-rotation so the visual doesn't spin with the body) and played damage-flash/death
    /// animations+audio via Animator/AudioSource. A future visuals component (mirroring CellVisuals) can restore
    /// these once a macro-virus prefab exists to wire it to.
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

        private MacroVirusConfig _config;
        private ICellTargetProvider _cellTargetProvider;

        private Vector3 _velocity;
        private float _angularVelocity;

        private IMacroVirusBehavior _behavior;
        private int _health;
        private bool _isAlive = true;

        private CancellationTokenSource _lifetimeCts;

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

            if (_health <= 0)
                HandleDeathAsync().Forget();
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
