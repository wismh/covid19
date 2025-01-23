using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;
using _Project.Input;
using _Project.Mechanics.WorldScale;

namespace _Project.UI.Hud
{
    /// <summary>
    /// World-space "press E to enter the cell" prompt that appears near a cell and toggles Macro/Micro world.
    /// Not a BaseScreen - it's a small always-active proximity watcher, not a stacked panel. Its target cell
    /// is assigned per-instance via <see cref="_targetCell"/>/<see cref="SetTargetCell"/> rather than found
    /// through a cell registry, since none exists yet. <see cref="SetContext"/> supplies the shared
    /// <see cref="WorldScaleTransitionContext"/> passed through to <see cref="WorldScaleController"/>; until
    /// called, <see cref="_context"/> is null and the E-key handler is a safe no-op.
    /// </summary>
    public sealed class CellProximityPrompt : MonoBehaviour
    {
        [SerializeField] private GameObject _promptCanvas;
        [SerializeField] private Transform _player;
        [SerializeField] private Transform _targetCell;

        private WorldScaleConfig _worldScaleConfig;
        private WorldScaleController _worldScale;
        private WorldScaleTransitionContext _context;

        private Actions _inputActions;
        private CancellationTokenSource _lifetimeCts;

        private bool _isPromptShown;

        [Inject]
        public void Construct(WorldScaleConfig worldScaleConfig, WorldScaleController worldScale)
        {
            _worldScaleConfig = worldScaleConfig;
            _worldScale = worldScale;
        }

        public void SetContext(WorldScaleTransitionContext context)
        {
            _context = context;
        }

        /// <summary>Lets a future cell registry repoint this prompt at whichever cell is currently nearest.</summary>
        public void SetTargetCell(Transform cell)
        {
            _targetCell = cell;
        }

        public void SetPlayer(Transform player)
        {
            _player = player;
        }

        private void Awake()
        {
            _inputActions = new Actions();
            _lifetimeCts = new CancellationTokenSource();

            _promptCanvas.SetActive(false);
        }

        private void OnEnable()
        {
            _inputActions.PlayerPenetration.Enable();
            _inputActions.PlayerPenetration.Penetration.performed += OnPenetrationPerformed;
        }

        private void OnDisable()
        {
            _inputActions.PlayerPenetration.Penetration.performed -= OnPenetrationPerformed;
            _inputActions.PlayerPenetration.Disable();
        }

        private void OnDestroy()
        {
            _inputActions?.Dispose();

            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        }

        private void Update()
        {
            if (_player == null || _targetCell == null)
                return;

            if (_worldScale.CurrentScale != WorldScale.Macro)
            {
                HidePrompt();
                return;
            }

            float sqrDistance = (_targetCell.position - _player.position).sqrMagnitude;
            if (sqrDistance < _worldScaleConfig.CellProximityActivationSqrDistance)
                ShowPrompt();
            else
                HidePrompt();
        }

        private void ShowPrompt()
        {
            _promptCanvas.transform.position = _targetCell.position;

            if (!_isPromptShown)
            {
                _promptCanvas.SetActive(true);
                _isPromptShown = true;
            }
        }

        private void HidePrompt()
        {
            if (!_isPromptShown)
                return;

            _promptCanvas.SetActive(false);
            _isPromptShown = false;
        }

        /// <summary>E while the prompt is shown enters micro world; otherwise exits back to macro.</summary>
        private void OnPenetrationPerformed(InputAction.CallbackContext context)
        {
            if (_context == null)
                return; // No-op until SetContext is called.

            // WorldScaleController throws if a second transition starts while one is already in flight.
            if (_worldScale.IsTransitioning)
                return;

            if (_isPromptShown)
            {
                HidePrompt();
                EnterMicroWorldSafeAsync().Forget();
            }
            else
            {
                EnterMacroWorldSafeAsync().Forget();
            }
        }

        private async UniTaskVoid EnterMicroWorldSafeAsync()
        {
            try
            {
                await _worldScale.EnterMicroWorldAsync(_context, _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTaskVoid EnterMacroWorldSafeAsync()
        {
            try
            {
                await _worldScale.EnterMacroWorldAsync(_context, _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
