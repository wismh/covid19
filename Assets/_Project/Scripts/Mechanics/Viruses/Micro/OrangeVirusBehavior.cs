using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>Continuous homing force toward the player; melee-only (contact damage is handled by
    /// MicroVirusController.OnCollisionStay2D, shared by all three variants).</summary>
    public sealed class OrangeVirusBehavior : IMicroVirusBehavior
    {
        private readonly MicroVirusConfig _config;
        private readonly Transform _target;

        private Transform _transform;
        private Rigidbody2D _rigidbody2D;

        public OrangeVirusBehavior(MicroVirusConfig config, Transform target)
        {
            _config = config;
            _target = target;
        }

        public void Init(Transform transform)
        {
            _transform = transform;
            _rigidbody2D = transform.GetComponent<Rigidbody2D>();
        }

        public void OnEnable() { }

        public void Update() { }

        public void FixedUpdate()
        {
            if (_target == null)
                return;

            _rigidbody2D.AddForce((_target.position - _transform.position).normalized * _config.HomingForce, ForceMode2D.Force);
        }
    }
}
