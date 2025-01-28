using Project.Core.AudioSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;
using _Project.Input;

namespace _Project.Mechanics.Player
{
    /// <summary>Independent turret aiming at the pointer regardless of body facing, with a reload cooldown that
    /// scales with the AttackSpeed upgrade.</summary>
    public sealed class PlayerTurret : MonoBehaviour
    {
        [SerializeField] private Transform _turretTransform;
        [SerializeField] private Transform _muzzlePoint;
        [SerializeField] private SoundData _fireSound;

        /// <summary>This script lives on the player's root GameObject, not the turret's own child GameObject -
        /// exposes the actual turret child so PlayerHealth can deactivate just the turret visual on death, not
        /// the whole player.</summary>
        public GameObject TurretVisualObject => _turretTransform.gameObject;

        private IProjectileSpawner _projectileSpawner;

        private PlayerConfig _config;
        private PlayerStatsService _stats;
        private IAudioSystem _audioSystem;

        private Actions _inputActions;

        private const float AimDirectionDeadzone = 0.2f;

        private bool _isFiring;
        private float _lastFireTime = float.NegativeInfinity;
        private float _currentReloadTime;

        /// <summary>0 (just fired) to 1 (ready to fire) - for a future UI screen to bind a reload indicator to.</summary>
        public float ReloadProgress01 => _currentReloadTime <= 0f
            ? 1f
            : Mathf.Clamp01((Time.time - _lastFireTime) / _currentReloadTime);

        [Inject]
        public void Construct(PlayerConfig config, PlayerStatsService stats, IAudioSystem audioSystem)
        {
            _config = config;
            _stats = stats;
            _audioSystem = audioSystem;
        }

        public void SetProjectileSpawner(IProjectileSpawner spawner)
        {
            _projectileSpawner = spawner;
        }

        private void Awake()
        {
            _inputActions = new Actions();
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
            Vector2 aimDirection = _inputActions.PlayerTurret.AimDirection.ReadValue<Vector2>();
            bool isStickAiming = aimDirection.magnitude >= AimDirectionDeadzone;

            bool wantsToFire;
            if (isStickAiming)
            {
                AimAtDirection(aimDirection);
                wantsToFire = true;
            }
            else
            {
                AimAtPointer();
                wantsToFire = _isFiring;
            }

            if (wantsToFire && Time.time >= _lastFireTime + _currentReloadTime)
                Fire();
        }

        private void AimAtDirection(Vector2 direction)
        {
            _turretTransform.right = direction.normalized;
        }

        private void AimAtPointer()
        {
            Vector2 screenPosition = _inputActions.PlayerTurret.Pointer.ReadValue<Vector2>();
            Vector3 worldPosition = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.ScreenToWorldPoint(screenPosition) : Vector3.zero;

            Vector3 direction = (worldPosition - _turretTransform.position).normalized;
            direction.z = 0f;

            _turretTransform.right = direction;
        }

        private void Fire()
        {
            _projectileSpawner.SpawnProjectile(_muzzlePoint.position, _turretTransform.right);

            _audioSystem.PlaySfx(_fireSound);

            _lastFireTime = Time.time;
            _currentReloadTime = _stats.EffectiveReloadTime;
        }
    }
}
