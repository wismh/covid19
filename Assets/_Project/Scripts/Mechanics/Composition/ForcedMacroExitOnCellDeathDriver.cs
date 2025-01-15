using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Composition
{
    public sealed class ForcedMacroExitOnCellDeathDriver : IInitializable, IDisposable
    {
        private readonly CellRegistry _cellRegistry;
        private readonly WorldScaleController _worldScaleController;
        private readonly WorldScaleTransitionContext _worldScaleTransitionContext;

        private CancellationTokenSource _lifetimeCts;
        private Action<UnityEngine.Vector3, int> _handleCellDiedForceMacroExit;

        public ForcedMacroExitOnCellDeathDriver(
            CellRegistry cellRegistry,
            WorldScaleController worldScaleController,
            WorldScaleTransitionContext worldScaleTransitionContext)
        {
            _cellRegistry = cellRegistry;
            _worldScaleController = worldScaleController;
            _worldScaleTransitionContext = worldScaleTransitionContext;
        }

        public void Initialize()
        {
            _lifetimeCts = new CancellationTokenSource();

            _handleCellDiedForceMacroExit = (position, _) =>
            {
                if (_worldScaleController.CurrentScale != _Project.Mechanics.WorldScale.WorldScale.Micro)
                    return;

                if (_worldScaleTransitionContext.Cell == null || _worldScaleTransitionContext.Cell.position != position)
                    return;

                EnterMacroWorldSafeAsync().Forget();
            };
            _cellRegistry.OnCellDied += _handleCellDiedForceMacroExit;

            _cellRegistry.OnAllCellsDied += HandleAllCellsDiedForceMacroExit;
        }

        public void Dispose()
        {
            _cellRegistry.OnAllCellsDied -= HandleAllCellsDiedForceMacroExit;
            _cellRegistry.OnCellDied -= _handleCellDiedForceMacroExit;

            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        }

        // GameOverDriver independently handles showing the game-over screen on this same event.
        private void HandleAllCellsDiedForceMacroExit()
        {
            if (_worldScaleController.CurrentScale == _Project.Mechanics.WorldScale.WorldScale.Micro)
                EnterMacroWorldSafeAsync().Forget();
        }

        private async UniTaskVoid EnterMacroWorldSafeAsync()
        {
            try
            {
                await _worldScaleController.EnterMacroWorldAsync(_worldScaleTransitionContext, _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
