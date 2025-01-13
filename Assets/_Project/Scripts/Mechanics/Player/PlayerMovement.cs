using UnityEngine;
using Zenject;
using _Project.Input;

namespace _Project.Mechanics.Player
{
    /// <summary>
    /// Ports GMTK.DriftMovableObject (the generic inertia/drift primitive) fused with GMTK.PlayerBodyController
    /// (the player-specific input wiring on top of it) - Assets/GMTK/Scripts/Behaviors/DriftMovableObject.cs and
    /// Assets/GMTK/Scripts/Behaviors/Player/PlayerBodyController.cs. No ServiceLocator: config/stats arrive via
    /// Zenject method injection, input is read directly (no transform.Find/GetChild).
    /// </summary>
    public sealed class PlayerMovement : MonoBehaviour
    {
        // Optional: old PlayerBodyController zeroed an attached Rigidbody2D's velocity every FixedUpdate, since
        // movement is transform-driven here (see Update()), not physics-driven - the Rigidbody2D only exists for
        // collision detection. Left optional/nullable since this phase creates no prefab to wire it on.
        [SerializeField] private Rigidbody2D _rigidbody2D;

        private PlayerConfig _config;
        private PlayerStatsService _stats;

        private Actions _inputActions;

        private Vector3 _velocity;
        private float _angularVelocity;

        // Old: GMTK.Services.PlayerStats.SpeedScale (1f macro, 0.01f micro), consumed by GMTK.PlayerBodyController.
        // FixedUpdate: `_body.Move(inputValue.y * _playerStats.SpeedScale * backMovement)` - throttles forward/
        // backward thrust input only, never Rotate's turning input. WorldScaleController's own class doc comment
        // (seam item 4) already flagged this exact gap: WorldScaleTransition calls
        // `context.SetPlayerSpeedScale?.Invoke(...)` correctly end-to-end, but nothing on this side ever consumed
        // it - Phase 7 testing fix (2026-09-11): user reported movement in the micro world was much too fast, this
        // is why. Defaults to 1 (macro) so nothing changes until a composition root actually wires SetSpeedScale.
        private float _speedScale = 1f;

        public Vector3 Velocity => _velocity;
        public float AngularVelocity => _angularVelocity;
        public float MaxSpeed => _stats.EffectiveMoveSpeed;

        [Inject]
        public void Construct(PlayerConfig config, PlayerStatsService stats)
        {
            _config = config;
            _stats = stats;
        }

        /// <summary>See <see cref="_speedScale"/>'s doc comment. Wired from
        /// WorldScaleTransitionContext.SetPlayerSpeedScale by a composition root.</summary>
        public void SetSpeedScale(float scale)
        {
            _speedScale = scale;
        }

        private void Awake()
        {
            _inputActions = new Actions();
        }

        private void OnEnable()
        {
            _inputActions.PlayerBody.Enable();
        }

        private void OnDisable()
        {
            _inputActions.PlayerBody.Disable();
        }

        private void OnDestroy()
        {
            _inputActions?.Dispose();
        }

        private void FixedUpdate()
        {
            Vector2 input = _inputActions.PlayerBody.Move.ReadValue<Vector2>();

            // Backward movement is throttled to a small fraction of full thrust.
            // Old: GMTK.PlayerBodyController.FixedUpdate -> `inputValue.y < 0 ? 0.05f : 1f`.
            float backwardThrottle = input.y < 0f ? _config.BackwardMovementMultiplier : 1f;

            Move(input.y * backwardThrottle * _speedScale);
            Rotate(input.x);

            if (_rigidbody2D != null)
            {
                _rigidbody2D.linearVelocity = Vector2.zero;
                _rigidbody2D.angularVelocity = 0f;
            }
        }

        private void Update()
        {
            transform.position += Time.deltaTime * _velocity;
            transform.Rotate(0f, 0f, Time.deltaTime * _angularVelocity);
        }

        /// <summary>Old: GMTK.DriftMovableObject.Move(float).</summary>
        public void Move(float direction)
        {
            float maxSpeed = _stats.EffectiveMoveSpeed;

            _velocity += direction * _config.Acceleration * transform.right;
            _velocity += _config.Friction * -_velocity;
            _velocity = Vector3.ClampMagnitude(_velocity, maxSpeed);
        }

        /// <summary>Old: GMTK.DriftMovableObject.Rotate(float).</summary>
        public void Rotate(float direction)
        {
            float maxAngularSpeed = _stats.EffectiveTurnRate;

            _angularVelocity += direction * _config.AngularAcceleration;
            _angularVelocity += _config.AngularFriction * -Mathf.Sign(_angularVelocity);
            _angularVelocity = Mathf.Clamp(_angularVelocity, -maxAngularSpeed, maxAngularSpeed);
        }

        /// <summary>Old: GMTK.DriftMovableObject.Stop().</summary>
        public void Stop()
        {
            _velocity = Vector3.zero;
            _angularVelocity = 0f;
        }

        /// <summary>Old: GMTK.DriftMovableObject.Impulse(Vector3) - used by PlayerHealth for on-hit knockback.</summary>
        public void ApplyImpulse(Vector3 impulse)
        {
            _velocity += impulse;
        }
    }
}
