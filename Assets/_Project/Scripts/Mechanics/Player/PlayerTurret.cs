using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace _Project.Mechanics.Player
{
    /// <summary>
    /// Ports GMTK.PlayerTurretController (Assets/GMTK/Scripts/Behaviors/Player/PlayerTurretController.cs):
    /// independent turret aiming at the pointer regardless of body facing, with a reload cooldown that scales
    /// with the AttackSpeed upgrade. No ServiceLocator, no transform.Find - direct [SerializeField] references
    /// and Zenject method injection instead.
    ///
    /// Firing seam: projectile spawning goes through <see cref="IProjectileSpawner"/> rather than a new
    /// pooling system (out of scope for this phase) - see GmtkBulletManagerProjectileSpawner for the concrete
    /// bridge to the existing GMTK.BulletManager, and its doc comment for where damage calculation currently
    /// still runs through old GMTK code instead of PlayerStatsService.EffectiveDamage.
    ///
    /// UI seam: the old controller drove a DOTween-animated Image directly (`_filledReloadImage.fillAmount`).
    /// Mechanics shouldn't own presentation, so this exposes <see cref="ReloadProgress01"/> instead for a future
    /// UI screen (Phase 5, BaseScreen-based per REFACTORING_PLAN.md) to bind to.
    ///
    /// Input bridge: see PlayerMovement's doc comment - same temporary GMTK.Actions bridge, action map
    /// "PlayerTurret" (actions "Pointer", "Fire").
    /// </summary>
    public sealed class PlayerTurret : MonoBehaviour
    {
        [SerializeField] private Transform _turretTransform;
        [SerializeField] private Transform _muzzlePoint;

        /// <summary>
        /// Phase 7 testing fix (2026-09-11): PlayerHealth.HandleDeathAsync's old-code port does `_turret.gameObject
        /// .SetActive(false)` to hide the turret on death (old: `transform.Find("Turret").gameObject.SetActive
        /// (false)`). Old code's "Turret" was a CHILD GameObject holding only the turret sub-mesh - disabling it
        /// hid the visual while the player's own root object (movement, health, everything) stayed alive. This
        /// PlayerTurret SCRIPT, however, lives on the PLAYER'S ROOT GameObject, not on that child - so
        /// `_turret.gameObject` (where `_turret` is a PlayerTurret reference) resolves to the whole player, not
        /// the turret visual. Confirmed live via Unity CLI: killing the player in a cell deactivated the entire
        /// Player GameObject instantly. Exposes the actual turret child object so PlayerHealth can disable the
        /// right one.
        /// </summary>
        public GameObject TurretVisualObject => _turretTransform != null ? _turretTransform.gameObject : null;

        // Typed as the concrete bridge class (not IProjectileSpawner) because Unity can't serialize
        // interface-typed fields for Inspector assignment; it's used through the interface in code below.
        [SerializeField] private GmtkBulletManagerProjectileSpawner _projectileSpawner;

        private PlayerConfig _config;
        private PlayerStatsService _stats;

        private GMTK.Actions _inputActions;

        private bool _isFiring;
        private float _lastFireTime = float.NegativeInfinity;
        private float _currentReloadTime;

        // Old: PlayerTurretController.Start() -> `_audioSource = _turret.GetComponent<AudioSource>();`, played
        // in Update() on every shot. Player.prefab's "Turret" child (= _turretTransform's GameObject) still
        // carries that same AudioSource - resolved here instead of adding a new [SerializeField] since the
        // reference is already reachable off an existing one.
        private AudioSource _fireAudioSource;

        /// <summary>0 (just fired) to 1 (ready to fire) - for a future UI screen to bind a reload indicator to.</summary>
        public float ReloadProgress01 => _currentReloadTime <= 0f
            ? 1f
            : Mathf.Clamp01((Time.time - _lastFireTime) / _currentReloadTime);

        [Inject]
        public void Construct(PlayerConfig config, PlayerStatsService stats)
        {
            _config = config;
            _stats = stats;
        }

        /// <summary>
        /// Phase 7 testing fix (2026-09-11): Player.prefab never had a GmtkBulletManagerProjectileSpawner
        /// component, so this field was always null in the scene and Fire() silently no-op'd (its own
        /// "if (_projectileSpawner != null)" guard, meant for editor-authoring gaps, was masking a real one).
        /// A composition root now resolves the sibling spawner component and wires it in via this setter.
        /// </summary>
        public void SetProjectileSpawner(GmtkBulletManagerProjectileSpawner spawner)
        {
            _projectileSpawner = spawner;
        }

        private void Awake()
        {
            _inputActions = new GMTK.Actions();

            if (_turretTransform != null)
                _fireAudioSource = _turretTransform.GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            _inputActions.PlayerTurret.Enable();
            _inputActions.PlayerTurret.Fire.performed += OnFirePerformed;
            _inputActions.PlayerTurret.Fire.canceled += OnFireCanceled;
        }

        private void OnDisable()
        {
            _inputActions.PlayerTurret.Fire.performed -= OnFirePerformed;
            _inputActions.PlayerTurret.Fire.canceled -= OnFireCanceled;
            _inputActions.PlayerTurret.Disable();
        }

        private void OnDestroy()
        {
            _inputActions?.Dispose();
        }

        private void OnFirePerformed(InputAction.CallbackContext context) => _isFiring = true;
        private void OnFireCanceled(InputAction.CallbackContext context) => _isFiring = false;

        private void Update()
        {
            AimAtPointer();

            if (_isFiring && Time.time >= _lastFireTime + _currentReloadTime)
                Fire();
        }

        private void AimAtPointer()
        {
            Vector2 screenPosition = _inputActions.PlayerTurret.Pointer.ReadValue<Vector2>();
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(screenPosition);

            Vector3 direction = (worldPosition - _turretTransform.position).normalized;
            direction.z = 0f;

            _turretTransform.right = direction;
        }

        private void Fire()
        {
            if (_projectileSpawner != null)
                _projectileSpawner.SpawnProjectile(_muzzlePoint.position, _turretTransform.right);

            // Old: PlayerTurretController.Update() -> `_audioSource.Play();`, unconditionally on every shot.
            if (_fireAudioSource != null)
                _fireAudioSource.Play();

            _lastFireTime = Time.time;

            // Old: PlayerTurretController.Update() ->
            // `_reloadTime = _baseReloadTime - level * _scaleByLevelUpgrade; _reloadTime = _reloadTime <= 0 ? 0.25f : _reloadTime;`
            _currentReloadTime = _stats.EffectiveReloadTime;
        }
    }
}
