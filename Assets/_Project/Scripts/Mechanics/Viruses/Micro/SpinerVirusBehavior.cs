using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>No continuous movement - a periodic impulse rush toward the player with an "Attack" animation
    /// wind-up shortly before it fires.</summary>
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

        public void OnEnable()
        {
            _attackTime = Time.time + UnityEngine.Random.Range(0f, _config.RushCooldownSeconds * _config.InitialCooldownJitterMultiplier);
            _animationPlaying = false;
        }

        public void Update() { }

        public void FixedUpdate()
        {
            if (_target == null)
                return;

            if (Time.time > _attackTime + _config.RushCooldownSeconds - _config.RushWindupLeadSeconds && !_animationPlaying)
            {
                _animationPlaying = true;
                _animator?.Play("Base Layer.Attack");
            }

            if (Time.time > _attackTime + _config.RushCooldownSeconds)
            {
                _attackTime = Time.time + _config.RushCooldownSeconds;
                _animationPlaying = false;

                _rigidbody2D.AddForce((_target.position - _transform.position).normalized * _config.RushImpulse, ForceMode2D.Impulse);
            }
        }
    }
}
