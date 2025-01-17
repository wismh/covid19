using System.Threading;
using Cysharp.Threading.Tasks;

namespace _Project.Mechanics.WorldScale
{
    /// <summary>Performs the actual Macro/Micro transition work that WorldScaleController drives.</summary>
    public interface IWorldScaleTransition
    {
        UniTask EnterMacroWorldAsync(WorldScaleTransitionContext context, CancellationToken cancellationToken = default);

        UniTask EnterMicroWorldAsync(WorldScaleTransitionContext context, CancellationToken cancellationToken = default);
    }
}
