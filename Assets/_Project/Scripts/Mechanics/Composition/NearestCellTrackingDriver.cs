using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Micro;
using _Project.Mechanics.WorldScale;
using _Project.UI.Hud;

namespace _Project.Mechanics.Composition
{
    public sealed class NearestCellTrackingDriver : IInitializable, IDisposable
    {
        private readonly PlayerMovement _playerMovement;
        private readonly CellRegistry _cellRegistry;
        private readonly CellProximityPrompt _cellProximityPrompt;
        private readonly WorldScaleTransitionContext _worldScaleTransitionContext;
        private readonly float _pollIntervalSeconds;

        private CancellationTokenSource _lifetimeCts;

        public NearestCellTrackingDriver(
            PlayerMovement playerMovement,
            CellRegistry cellRegistry,
            CellProximityPrompt cellProximityPrompt,
            WorldScaleTransitionContext worldScaleTransitionContext,
            float pollIntervalSeconds)
        {
            _playerMovement = playerMovement;
            _cellRegistry = cellRegistry;
            _cellProximityPrompt = cellProximityPrompt;
            _worldScaleTransitionContext = worldScaleTransitionContext;
            _pollIntervalSeconds = pollIntervalSeconds;
        }

        public void Initialize()
        {
            _lifetimeCts = new CancellationTokenSource();
            PollNearestCellAsync(_lifetimeCts.Token).Forget();
        }

        public void Dispose()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        }

        private async UniTaskVoid PollNearestCellAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                UpdateNearestCellTarget();

                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void UpdateNearestCellTarget()
        {
            if (_playerMovement == null)
                return;

            Transform nearestCell = _cellRegistry.FindNearestCell(_playerMovement.transform.position, out _);

            _cellProximityPrompt.SetTargetCell(nearestCell);

            _worldScaleTransitionContext.Cell = nearestCell;
            _worldScaleTransitionContext.CellVisuals = nearestCell != null ? nearestCell.GetComponent<CellVisuals>() : null;
            _worldScaleTransitionContext.CellRigidbody2D = nearestCell != null ? nearestCell.GetComponent<Rigidbody2D>() : null;
            _worldScaleTransitionContext.CellAnimator = nearestCell != null ? nearestCell.GetComponent<Animator>() : null;
            _worldScaleTransitionContext.CellMicroVirusSpawner = nearestCell != null ? nearestCell.GetComponent<MicroVirusSpawner>() : null;
            _worldScaleTransitionContext.CellInfectionState = nearestCell != null ? nearestCell.GetComponent<CellInfectionState>() : null;
        }
    }
}
