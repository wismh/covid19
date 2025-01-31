using UnityEngine;
using Zenject;
using _Project.Input;

namespace _Project.Mechanics.Player
{
    /// <summary>Transform-driven inertia/drift movement with independent forward/backward thrust and rotation.</summary>
    public sealed class PlayerMovement : MonoBehaviour
    {
        // Movement is transform-driven (see Update()), not physics-driven - the Rigidbody2D only exists for
        // collision detection, so its own velocity is zeroed every FixedUpdate below. Optional/nullable since not
        // every prefab wires one.
        [SerializeField] private Rigidbody2D _rigidbody2D;

        private PlayerConfig _config;
        private PlayerStatsService _stats;

        private Actions _inputActions;

        private Vector3 _velocity;
        private float _angularVelocity;

        // World-scale speed multiplier (1 macro, 0.01 micro) - throttles forward/backward thrust only, not
        // turning. Wired via SetSpeedScale below.
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
            float verticalInput = input.y;

            // Backward movement is throttled to a small fraction of full thrust.
            float backwardThrottle = verticalInput < 0f ? _config.BackwardMovementMultiplier : 1f;

            Move(verticalInput * backwardThrottle * _speedScale);
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

        public void Move(float direction)
        {
            float maxSpeed = _stats.EffectiveMoveSpeed;

            _velocity += direction * _config.Acceleration * transform.right;
            _velocity += _config.Friction * -_velocity;
            _velocity = Vector3.ClampMagnitude(_velocity, maxSpeed);
        }

        public void Rotate(float direction)
        {
            float maxAngularSpeed = _stats.EffectiveTurnRate;

            _angularVelocity += direction * _config.AngularAcceleration;
            _angularVelocity += _config.AngularFriction * -Mathf.Sign(_angularVelocity);
            _angularVelocity = Mathf.Clamp(_angularVelocity, -maxAngularSpeed, maxAngularSpeed);
        }

        public void Stop()
        {
            _velocity = Vector3.zero;
            _angularVelocity = 0f;
        }

        /// <summary>Used by PlayerHealth for on-hit knockback.</summary>
        public void ApplyImpulse(Vector3 impulse)
        {
            _velocity += impulse;
        }
    }
}
