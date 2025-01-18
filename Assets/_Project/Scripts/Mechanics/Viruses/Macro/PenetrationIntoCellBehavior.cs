using System;
using DG.Tweening;
using UnityEngine;
using _Project.Mechanics.Cells;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>DOTween-shrinks the virus into its target cell, infects the cell, then invokes
    /// <c>onComplete</c> - despawn/removal is left to whatever that callback is wired to.</summary>
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
            if (_target != null && _target.TryGetComponent(out CellInfectionState infectionState))
                infectionState.AddVirus();

            _onComplete?.Invoke();
        }

        public void FixedUpdate()
        {
            // No-op - the DOTween sequence drives the whole animation, no per-physics-tick work needed.
        }
    }
}
