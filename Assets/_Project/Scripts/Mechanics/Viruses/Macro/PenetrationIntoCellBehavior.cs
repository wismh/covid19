using System;
using DG.Tweening;
using UnityEngine;
using _Project.Mechanics.Cells;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>
    /// Ports GMTK.VirusBehaviors.PenetrationIntoCell (Assets/GMTK/Scripts/Behaviors/MacroVirus/Behaviors/
    /// PenetrationIntoCell.cs) - DOTween-shrinks the virus into its target cell, then infects the cell and signals
    /// completion.
    ///
    /// Old code reached directly into `ServiceLocator.Instance.Get&lt;VirusManager&gt;().RemoveObject(_transform)`
    /// and then `Object.Destroy(_transform.gameObject)` in OnAnimationEnd. Both of those are manager-list/pooling
    /// concerns REFACTORING_PLAN.md section 4 earmarks for a future composition root (which will eventually decide
    /// between `Destroy` and the planned `SetActive`-based pool per section 4's "Пулінг - крок за кроком"), so
    /// this behavior does neither - it only does the one thing that's actually part of "penetration": infecting
    /// the cell via <see cref="CellInfectionState.AddVirus"/> (a plain GetComponent call on the target, same as
    /// old code's `_target.GetComponent&lt;CellController&gt;().OnAddVirus()` - not a ServiceLocator lookup, so
    /// no interop concern there), then invokes the <paramref name="onComplete"/> callback so
    /// MacroVirusController can raise its own OnPenetratedIntoCell event for whatever composition root exists
    /// later to handle removal/destruction/pooling.
    /// </summary>
    public sealed class PenetrationIntoCellBehavior : IMacroVirusBehavior
    {
        private readonly Transform _target;
        private readonly MacroVirusConfig _config;
        private readonly Action _onComplete;

        private Transform _transform;

        public PenetrationIntoCellBehavior(Transform target, MacroVirusConfig config, Action onComplete)
        {
            _target = target;
            _config = config;
            _onComplete = onComplete;
        }

        public void Init(Transform transform)
        {
            _transform = transform;

            // Old: `transform.parent = _target;`
            transform.SetParent(_target);

            Vector3 originalScale = transform.localScale;
            Sequence sequence = DOTween.Sequence();

            sequence.Append(
                transform.DOScale(originalScale * _config.PenetrationScaleFactor, _config.PenetrationDurationSeconds)
                    .SetEase(Ease.InOutQuad)
            ).OnComplete(OnAnimationEnd);
        }

        private void OnAnimationEnd()
        {
            // Old: `_target.GetComponent<CellController>().OnAddVirus();`
            if (_target != null && _target.TryGetComponent(out CellInfectionState infectionState))
                infectionState.AddVirus();

            _onComplete?.Invoke();
        }

        public void FixedUpdate()
        {
            // Old: PenetrationIntoCell.IBehavior.FixedUpdate() was an empty no-op too - the DOTween sequence
            // drives the whole animation, no per-physics-tick work needed.
        }
    }
}
