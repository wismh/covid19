using UnityEngine;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>
    /// Ports GMTK.VirusBehaviors.MoveToCell (Assets/GMTK/Scripts/Behaviors/MacroVirus/Behaviors/MoveToCell.cs).
    /// Dependencies old code resolved via ServiceLocator (CellManager) and GetComponent (DriftMovableObject) are
    /// now constructor-injected instead: <see cref="ICellTargetProvider"/> replaces the ServiceLocator lookup,
    /// <see cref="IDriftBody"/> replaces the GetComponent call (implemented directly by MacroVirusController -
    /// see IDriftBody's doc comment).
    /// </summary>
    public sealed class MoveToCellBehavior : IMacroVirusBehavior
    {
        private readonly ICellTargetProvider _cellTargetProvider;
        private readonly IDriftBody _body;
        private readonly MacroVirusConfig _config;

        private Transform _transform;
        private Transform _target;

        public MoveToCellBehavior(ICellTargetProvider cellTargetProvider, IDriftBody body, MacroVirusConfig config)
        {
            _cellTargetProvider = cellTargetProvider;
            _body = body;
            _config = config;
        }

        public void Init(Transform transform)
        {
            _transform = transform;
            _target = _cellTargetProvider.FindNearestCell(transform.position);
        }

        public void FixedUpdate()
        {
            if (_target == null)
            {
                _target = _cellTargetProvider.FindNearestCell(_transform.position);
                return;
            }

            Vector3 direction = _target.position - _transform.position;
            float product = _transform.right.x * direction.y - _transform.right.y * direction.x;

            _body.Rotate(Mathf.Sign(product));
            _body.Move(_config.MoveToCellThrottle);
        }
    }
}
