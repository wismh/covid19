using UnityEngine;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>
    /// Ports GMTK.VirusBehaviors.AttackCell (Assets/GMTK/Scripts/Behaviors/MacroVirus/Behaviors/AttackCell.cs) -
    /// orbits/rams a specific cell once touching it. <see cref="Target"/> is exposed exactly like the old class
    /// (`public Transform Target => _target;`) because MacroVirusController.FixedUpdate needs it: old
    /// VirusController.FixedUpdate checked `if (_behavior is AttackCell attackBehavior && !attackBehavior.Target)`
    /// to fall back to MoveToCell if the target cell died mid-attack - MacroVirusController reproduces that same
    /// check against this property.
    /// </summary>
    public sealed class AttackCellBehavior : IMacroVirusBehavior
    {
        private readonly IDriftBody _body;
        private readonly MacroVirusConfig _config;
        private readonly Transform _target;

        private Transform _transform;

        public Transform Target => _target;

        public AttackCellBehavior(Transform target, IDriftBody body, MacroVirusConfig config)
        {
            _target = target;
            _body = body;
            _config = config;
        }

        public void Init(Transform transform)
        {
            _transform = transform;
        }

        public void FixedUpdate()
        {
            Vector3 direction = _target.position - _transform.position;
            float product = _transform.right.x * direction.y - _transform.right.y * direction.x;

            _body.Rotate(Mathf.Sign(product));
            _body.Move(_config.AttackOrbitThrottle);
        }
    }
}
