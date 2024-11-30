using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;
using _Project.Mechanics.WorldScale;

namespace _Project.UI.Hud
{
    /// <summary>
    /// Ports GMTK.UI.AdviceDialog (Assets/GMTK/Scripts/UI/AdviceDialog.cs) - the world-space "press E to enter
    /// the cell" prompt that appears near a cell and toggles Macro/Micro world.
    ///
    /// --- Why this is a plain MonoBehaviour, not a BaseScreen ---
    /// The old class was a world-space Canvas following a specific cell's position every frame, not a full-panel
    /// overlay someone opens/closes/stacks - it has no meaningful "open"/"close" lifecycle in the BaseScreen sense
    /// (no other screen ever needs to hide it, it's never one of several mutually-exclusive panels in a stack,
    /// and BaseScreenManager's layer-stack model - "opening a screen hides the previous top of its own layer" -
    /// doesn't describe anything this class needs). Forcing it into ScreenLayer.Default/Overlay would just mean
    /// always keeping it open and never touching OnOpen/OnClose for real work, which is a worse fit than what it
    /// actually is: a small always-active proximity watcher, exactly like GMTK.UI.AdviceDialog itself was (a
    /// plain MonoBehaviour + IService, never resembling a stacked panel).
    ///
    /// --- Where this lives - a judgement call ---
    /// Placed under UI/Hud/ alongside HudScreen rather than its own UI/CellProximity/ folder: it is HUD-adjacent
    /// presentation (a small world-space indicator, conceptually "part of the gameplay HUD experience") rather
    /// than a distinct feature with its own screen, config, and sub-parts the way Upgrades/GameOver are. If a
    /// second world-space prompt type is ever added, promoting both into their own folder then would be a trivial
    /// two-file move; splitting it out preemptively for a single class wasn't judged worth the indirection.
    ///
    /// --- The "which cell" problem: a documented seam, not an invented registry ---
    /// Old `AdviceDialog.Update()` called `_cellManager.FindNearToPoint(player.position, out sqrDistance)` -
    /// GMTK.CellManager knew about every live cell in the scene. REFACTORING_PLAN.md's own Phase 3 notes confirm
    /// no `_Project` equivalent (a cell/virus registry) has been ported yet (see CellInfectionState.cs's class
    /// doc comment). Per the phase brief's explicit instruction for exactly this situation, this class does NOT
    /// invent a registry or a FindObjectsOfType-based substitute - it takes its candidate cell directly via
    /// <see cref="_targetCell"/> (a plain [SerializeField] reference a human assigns per-cell-instance in the
    /// Editor once a real cell prefab/scene exists) instead of searching for "the nearest cell" among a set this
    /// class has no way to enumerate. A future cell-registry composition root that DOES have a "find nearest cell
    /// to point" query is expected to either (a) keep one CellProximityPrompt per cell with its own _targetCell -
    /// works unmodified with the single-target model - or (b) call <see cref="SetTargetCell"/> to repoint a single
    /// shared prompt instance at whichever cell is currently nearest, whichever shape that future registry ends
    /// up having.
    ///
    /// --- WorldScaleTransitionContext: also a documented seam ---
    /// EnterMicroWorldAsync/EnterMacroWorldAsync (Phase 4, WorldScaleController) take a WorldScaleTransitionContext
    /// bundling live references this class has no way to obtain itself (Camera, the player's collider/AudioSource,
    /// CellVisuals, the gameplay-music AudioSource/AudioMixerGroups, etc. - see WorldScaleTransitionContext.cs).
    /// That class is also not `[Serializable]` (several fields are delegates, which Unity cannot serialize
    /// regardless), so it cannot be a [SerializeField] here even in principle. <see cref="SetContext"/> is the
    /// entry point a future composition root uses to hand this class the one shared context object it should pass
    /// through to WorldScaleController - built once per scene, the same context every world-scale-transition
    /// caller in that scene would use. Until a composition root calls it, <see cref="_context"/> is null and the
    /// E-key handler is a safe no-op (documented below) rather than throwing.
    /// </summary>
    public sealed class CellProximityPrompt : MonoBehaviour
    {
        [SerializeField] private GameObject _promptCanvas;
        [SerializeField] private Transform _player;
        [SerializeField] private Transform _targetCell;

        // Old: `[SerializeField] private float _distanceToActivate;` compared directly against a SQUARED
        // distance (`_cellManager.FindNearToPoint` returns sqrDistance, never sqrt'd before the `<` check) -
        // i.e. despite its name, the old field was already a squared-distance threshold, not a linear one. Named
        // accurately here instead of reproducing the misleading old name.
        [SerializeField] private float _sqrDistanceToActivate = 4f;

        private WorldScaleController _worldScale;
        private WorldScaleTransitionContext _context;

        private GMTK.Actions _inputActions;
        private CancellationTokenSource _lifetimeCts;

        private bool _isPromptShown;

        /// <summary>WorldScaleController is a Mechanics/* POCO outside Zenject binding today (see
        /// WorldScaleController's own class doc comment - hand-composed per REFACTORING_PLAN.md section 4), same
        /// gap as PlayerConfig/PlayerStatsService elsewhere in this phase; a future composition root supplies it.</summary>
        [Inject]
        public void Construct(WorldScaleController worldScale)
        {
            _worldScale = worldScale;
        }

        /// <summary>See class doc comment's "WorldScaleTransitionContext" section.</summary>
        public void SetContext(WorldScaleTransitionContext context)
        {
            _context = context;
        }

        /// <summary>See class doc comment's "which cell" section - lets a future registry repoint this prompt.</summary>
        public void SetTargetCell(Transform cell)
        {
            _targetCell = cell;
        }

        private void Awake()
        {
            _inputActions = new GMTK.Actions();
            _lifetimeCts = new CancellationTokenSource();

            if (_promptCanvas != null)
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

        /// <summary>Old: AdviceDialog.Update() - only watches for proximity while in the macro world (`if
        /// (_gamePlayState.State != WorldState.MACRO_WORLD) return;`), moves the canvas to the cell's position and
        /// shows/hides it based on the squared-distance check.</summary>
        private void Update()
        {
            if (_player == null || _targetCell == null || _promptCanvas == null)
                return;

            if (_worldScale == null || _worldScale.CurrentScale != WorldScale.Macro)
            {
                HidePrompt();
                return;
            }

            float sqrDistance = (_targetCell.position - _player.position).sqrMagnitude;
            if (sqrDistance < _sqrDistanceToActivate)
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

        /// <summary>
        /// Old: AdviceDialog.OnPlayerPenetration -
        /// `if (_canvas.activeSelf) { _canvas.SetActive(false); _gamePlayState.State = WorldState.MICRO_WORLD; }
        /// else { _gamePlayState.State = WorldState.MACRO_WORLD; }` - i.e. pressing E while the prompt is showing
        /// (near a cell, in macro) enters micro; pressing E at any other time (prompt hidden - either not near a
        /// cell, or already inside micro since Update() early-returns and never re-shows the prompt while in
        /// micro) exits back to macro. Same toggle shape here, calling WorldScaleController's async entry points
        /// instead of the old synchronous `GamePlayState.State` setter (see WorldScaleController's class doc
        /// comment for why that's a deliberate, documented timing change from the old code).
        /// </summary>
        private void OnPenetrationPerformed(InputAction.CallbackContext context)
        {
            if (_worldScale == null || _context == null)
                return; // No-op until a composition root supplies both - see class doc comment.

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
