using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>
    /// Ports GMTK.MicroViruses.OrangeVirus (Assets/GMTK/Scripts/Behaviors/MicroVirus/Behaviors/OrangeVirus.cs) -
    /// the simplest of the three: continuous homing force toward the player, melee-only (contact damage is
    /// handled by MicroVirusController.OnCollisionStay2D, shared by all three variants - see
    /// MicroVirusConfig's doc comment on the 2s collision-damage cooldown).
    /// </summary>
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

        // Old: OrangeVirus doesn't override VirusBehavior.OnEnable() - no wind-up/cooldown state to reset.
        public void OnEnable() { }

        public void Update() { }

        /// <summary>Old: OrangeVirus.FixedUpdate() -> `_rd.AddForce((_target.position - _transform.position)
        /// .normalized * k_speed, ForceMode2D.Force);`</summary>
        public void FixedUpdate()
        {
            if (_target == null)
                return;

            _rigidbody2D.AddForce((_target.position - _transform.position).normalized * _config.HomingForce, ForceMode2D.Force);
        }
    }
}
