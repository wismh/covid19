using System.Threading;
using Cysharp.Threading.Tasks;

namespace _Project.Mechanics.WorldScale
{
    /// <summary>
    /// The collaborator WorldScaleController drives to actually perform a macro/micro transition - see
    /// WorldScaleController's class doc comment for the full split rationale between "pure state holder" (the
    /// controller) and "does the DOTween/physics/audio work" (this). Exists as an interface, not just a concrete
    /// dependency on WorldScaleTransition, purely so WorldScaleController's own unit tests (whenever a test
    /// project exists) can substitute a fake that completes instantly instead of waiting on real DOTween tweens.
    /// </summary>
    public interface IWorldScaleTransition
    {
        /// <summary>Ports GMTK.GameStates.TransitionIntoMacroWorld.Start(). Completes once every step - including
        /// the position tween - has finished; see WorldScaleTransition for the exact preserved steps/order.</summary>
        UniTask EnterMacroWorldAsync(WorldScaleTransitionContext context, CancellationToken cancellationToken = default);

        /// <summary>Ports GMTK.GameStates.TransitionIntoMicroWorld.Start(). Completes once every step - including
        /// the position tween - has finished; see WorldScaleTransition for the exact preserved steps/order.</summary>
        UniTask EnterMicroWorldAsync(WorldScaleTransitionContext context, CancellationToken cancellationToken = default);
    }
}
