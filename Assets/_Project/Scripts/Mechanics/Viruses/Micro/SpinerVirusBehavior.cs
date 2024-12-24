using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>
    /// Ports GMTK.MicroViruses.SpinerVirus (Assets/GMTK/Scripts/Behaviors/MicroVirus/Behaviors/SpinerVirus.cs) -
    /// no continuous movement at all, just a periodic impulse rush toward the player with an "Attack" animation
    /// wind-up 1 second before the rush fires.
    /// </summary>
    public sealed class SpinerVirusBehavior : IMicroVirusBehavior
    {
        private readonly MicroVirusConfig _config;
        private readonly Transform _target;

        private Transform _transform;
        private Rigidbody2D _rigidbody2D;
        private Animator _animator;

        private float _attackTime;
        private bool _animationPlaying;

        public SpinerVirusBehavior(MicroVirusConfig config, Transform target)
        {
            _config = config;
            _target = target;
        }

        public void Init(Transform transform)
        {
            _transform = transform;
            _rigidbody2D = transform.GetComponent<Rigidbody2D>();
            _animator = transform.GetComponent<Animator>();
        }

        /// <summary>Old: SpinerVirus.OnEnable() -> `_attackTime = Time.time + Random.Range(0, k_attackCooldown *
        /// 1.5f); _animationPlaying = false;`</summary>
        public void OnEnable()
        {
            _attackTime = Time.time + UnityEngine.Random.Range(0f, _config.RushCooldownSeconds * 1.5f);
            _animationPlaying = false;
        }

        public void Update() { }

        /// <summary>Old: SpinerVirus.FixedUpdate().</summary>
        public void FixedUpdate()
        {
            if (_target == null)
                return;

            // Old: `if (Time.time > (_attackTime + k_attackCooldown - 1) && !_animationPlaying) { ...Play("Base Layer.Attack"); }`
            if (Time.time > _attackTime + _config.RushCooldownSeconds - 1f && !_animationPlaying)
            {
                _animationPlaying = true;
                _animator?.Play("Base Layer.Attack");
            }

            // Old: `if (Time.time > _attackTime + k_attackCooldown) { ... AddForce(..., ForceMode2D.Impulse); }`
            if (Time.time > _attackTime + _config.RushCooldownSeconds)
            {
                _attackTime = Time.time + _config.RushCooldownSeconds;
                _animationPlaying = false;

                _rigidbody2D.AddForce((_target.position - _transform.position).normalized * _config.RushImpulse, ForceMode2D.Impulse);
            }
        }
    }
}
