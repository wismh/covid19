using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace _Project.Mechanics.WorldScale
{
    /// <summary>
    /// Tracks the current Macro/Micro world scale and drives transitions between them. Pure state holder + event
    /// source - the actual DOTween/physics/audio work for a transition lives in the injected
    /// <see cref="IWorldScaleTransition"/> collaborator. <see cref="CurrentScale"/> and the events below only
    /// change/fire after that collaborator's transition fully completes, not when it starts - so listeners always
    /// see a fully-switched world (see <see cref="OnEnteringMicroWorld"/> for the one pair of events that fire at
    /// the start instead, for cases that need that timing).
    /// </summary>
    public sealed class WorldScaleController
    {
        private readonly IWorldScaleTransition _transition;

        private WorldScale _currentScale = WorldScale.Macro;
        private bool _isTransitioning;

        public WorldScaleController(IWorldScaleTransition transition)
        {
            _transition = transition ?? throw new ArgumentNullException(nameof(transition));
        }

        public WorldScale CurrentScale => _currentScale;

        /// <summary>True while a transition is in flight; a second call while true throws instead of starting
        /// overlapping transitions.</summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>Raised once CurrentScale becomes Micro, after the transition fully completes.</summary>
        public event Action OnEnteredMicroWorld;

        /// <summary>Raised once CurrentScale becomes Macro, after the transition fully completes.</summary>
        public event Action OnExitedMicroWorld;

        /// <summary>Raised at the very start of EnterMicroWorldAsync, before the transition begins - unlike
        /// OnEnteredMicroWorld above. For listeners that need start-of-transition timing (e.g. a whoosh SFX that
        /// should play as the shrink happens, not after it finishes).</summary>
        public event Action OnEnteringMicroWorld;

        /// <summary>See <see cref="OnEnteringMicroWorld"/> - same reasoning, opposite direction.</summary>
        public event Action OnEnteringMacroWorld;

        /// <summary>Raised alongside whichever edge event above fires, carrying the new scale directly.</summary>
        public event Action<WorldScale> OnScaleChanged;

        public async UniTask EnterMicroWorldAsync(WorldScaleTransitionContext context, CancellationToken cancellationToken = default)
        {
            if (_currentScale == WorldScale.Micro)
                return;

            if (_isTransitioning)
                throw new InvalidOperationException(
                    $"{nameof(WorldScaleController)}.{nameof(EnterMicroWorldAsync)} called while a transition was already in flight.");

            _isTransitioning = true;
            OnEnteringMicroWorld?.Invoke();
            try
            {
                await _transition.EnterMicroWorldAsync(context, cancellationToken);
                Apply(WorldScale.Micro);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public async UniTask EnterMacroWorldAsync(WorldScaleTransitionContext context, CancellationToken cancellationToken = default)
        {
            if (_currentScale == WorldScale.Macro)
                return;

            if (_isTransitioning)
                throw new InvalidOperationException(
                    $"{nameof(WorldScaleController)}.{nameof(EnterMacroWorldAsync)} called while a transition was already in flight.");

            _isTransitioning = true;
            OnEnteringMacroWorld?.Invoke();
            try
            {
                await _transition.EnterMacroWorldAsync(context, cancellationToken);
                Apply(WorldScale.Macro);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private void Apply(WorldScale scale)
        {
            _currentScale = scale;

            OnScaleChanged?.Invoke(scale);

            if (scale == WorldScale.Micro)
                OnEnteredMicroWorld?.Invoke();
            else
                OnExitedMicroWorld?.Invoke();
        }
    }
}
