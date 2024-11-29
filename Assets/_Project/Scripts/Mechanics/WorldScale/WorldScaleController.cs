using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace _Project.Mechanics.WorldScale
{
    /// <summary>
    /// Replaces GMTK.GameStates.GamePlayState's world-scale half (Assets/GMTK/Scripts/GameStates/GameplayState.cs).
    /// Old GamePlayState did double duty as BOTH an app-flow `IState` (registered with GMTK.GameStates.
    /// GameStateManager, `Enter()`/`Update()`/`Exit()` all near-empty) AND the Macro/Micro mode holder (the
    /// `State` property setter that triggers transitions) - exactly the conflation REFACTORING_PLAN.md section 5
    /// calls out ("Змішування рівнів абстракції") and section 6 Phase 4 item 1 asks to split out. This class is
    /// only the second half; the app-flow half is already superseded by `_Project.Mechanics.AppFlow.
    /// GlobalGameStateMachine` from Phase 1 and is not this class's concern.
    ///
    /// Plain C# class (POCO), not a MonoBehaviour and not bound through Zenject - per REFACTORING_PLAN.md section
    /// 4, Mechanics/* gameplay systems are hand-composed (`new WorldScaleController(...)`), matching Phase 2's
    /// PlayerStatsService and Phase 3's CellHealth/CellInfectionState-adjacent design.
    ///
    /// --- Orchestration split (a judgement call the phase brief left open) ---
    /// WorldScaleController is a PURE STATE HOLDER + EVENT SOURCE. It does not touch DOTween, Camera, Rigidbody2D,
    /// or any live scene object itself - it holds <see cref="CurrentScale"/>, exposes the async entry points a
    /// caller awaits, and fires events once a transition is done. All the actual work (every DOTween sequence,
    /// physics/layer/audio/sprite step) lives in an injected <see cref="IWorldScaleTransition"/> collaborator
    /// (WorldScaleTransition, in this same folder) that this class drives and calls back into via
    /// <see cref="SetScale"/>-equivalent logic once the collaborator's UniTask completes.
    ///
    /// Rationale for this exact split: it mirrors CellHealth/CellInfectionState/CellVisuals from Phase 3 - one
    /// class owns "the fact" (state + events), a sibling/collaborator owns "the doing" (DOTween tweens, shader
    /// params), and neither needs to know the other's internals beyond a narrow public surface. It also keeps
    /// WorldScaleController trivially composable/testable (swap in a fake IWorldScaleTransition that completes
    /// instantly) without needing a live Camera/Rigidbody2D/DOTween runtime at all, which matters given this
    /// entire phase has no Unity Editor access to verify against.
    ///
    /// --- WHEN does CurrentScale change: a deliberate behavior change from the old code ---
    /// Old GamePlayState.State's setter flipped `_worldState` and fired `OnWorldChanged` SYNCHRONOUSLY, at the
    /// very top of the transition - before a single DOTween tween had even started animating (GameplayState.cs:
    /// 36-37, called from inside TransitionIntoMicroWorld()/TransitionIntoMacroWorld() themselves, i.e. mid-call).
    /// Any code reading `GamePlayState.State` (GMTK.Bullet.Update - Bullet.cs:21, GMTK.UI.AdviceDialog.Update -
    /// AdviceDialog.cs:47) would see the NEW value roughly a full second before the visual/physical transition
    /// (player scale, camera, layer, collider) actually finished.
    ///
    /// Here, <see cref="CurrentScale"/> only changes and events only fire AFTER `IWorldScaleTransition`'s UniTask
    /// completes - i.e. after the position tween's OnComplete callback (layer/collider/volume fixups included)
    /// has run. This is possible now specifically BECAUSE the transition is awaitable instead of fire-and-forget,
    /// and directly addresses REFACTORING_PLAN.md section 8's risk callout: "Зміна fixed-timestep/UniTask-
    /// скасування може по-іншому поводитись під час DOTween-переходів масштабу - протестувати граничні випадки
    /// (смерть клітини під час переходу)" - a future listener reacting to OnEnteredMicroWorld/OnExitedMicroWorld
    /// now sees a world that is ACTUALLY fully switched (final layer/collider/physics state included), not one
    /// that merely started switching. No Unity Editor was available to verify this doesn't introduce a new visible
    /// timing gap (e.g. bullets not yet re-scaled for ~1s after CurrentScale flips old-Micro-early vs new-Micro-
    /// late) - flagged as an open item for whoever wires this into a live scene to sanity-check.
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

        /// <summary>True while an EnterMicroWorldAsync/EnterMacroWorldAsync call is in flight. Old code had no
        /// equivalent guard - GamePlayState.State's setter would happily kick off a second transition mid-tween
        /// if called again before the first one's ~1s DOTween sequence finished, since `_worldState` was already
        /// flipped synchronously by the time a second call could re-enter (see class doc comment above). That
        /// specific race is closed here: a second call while `_isTransitioning` is true throws instead of
        /// silently starting overlapping/conflicting DOTween sequences on the same transforms.</summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>Raised once CurrentScale becomes Micro (after the transition fully completes - see class doc
        /// comment). Old: the MICRO_WORLD branch of GamePlayState.OnWorldChanged.</summary>
        public event Action OnEnteredMicroWorld;

        /// <summary>Raised once CurrentScale becomes Macro (after the transition fully completes - see class doc
        /// comment). Old: the MACRO_WORLD branch of GamePlayState.OnWorldChanged.</summary>
        public event Action OnExitedMicroWorld;

        /// <summary>
        /// Raised alongside whichever of the two events above fires, carrying the new scale explicitly. Kept
        /// alongside the two specific events rather than instead of them - the same "named edge events PLUS one
        /// generic value event" shape Phase 3's CellInfectionState already established
        /// (OnInfectionStarted/OnInfectionCleared alongside OnVirusCountChanged) for the same reason: some future
        /// listeners only care about a specific edge (e.g. "pause macro virus spawning" only needs to know when
        /// entering, not the value), others want the value itself without an if/else on which specific event
        /// fired. Old: `GamePlayState.OnWorldChanged` (single Action&lt;WorldState&gt;) - this is that event,
        /// plus the two convenience ones split out.
        /// </summary>
        public event Action<WorldScale> OnScaleChanged;

        /// <summary>
        /// Old: GMTK.GameStates.GamePlayState.State's setter, MICRO_WORLD branch (`TransitionIntoMicroWorld();
        /// _worldState = value; OnWorldChanged?.Invoke(value);`) - `_virusManager.gameObject.SetActive(false)`
        /// from the same setter is deliberately NOT reproduced here; see the "Other systems coupled to world
        /// scale" section at the bottom of this file for why.
        /// </summary>
        public async UniTask EnterMicroWorldAsync(WorldScaleTransitionContext context, CancellationToken cancellationToken = default)
        {
            // Old: `if (value == _worldState) return;` - GameplayState.cs:27-28.
            if (_currentScale == WorldScale.Micro)
                return;

            if (_isTransitioning)
                throw new InvalidOperationException(
                    $"{nameof(WorldScaleController)}.{nameof(EnterMicroWorldAsync)} called while a transition was already in flight.");

            _isTransitioning = true;
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

        /// <summary>Old: GMTK.GameStates.GamePlayState.State's setter, MACRO_WORLD branch. See
        /// <see cref="EnterMicroWorldAsync"/>'s doc comment - identical shape, opposite direction.</summary>
        public async UniTask EnterMacroWorldAsync(WorldScaleTransitionContext context, CancellationToken cancellationToken = default)
        {
            if (_currentScale == WorldScale.Macro)
                return;

            if (_isTransitioning)
                throw new InvalidOperationException(
                    $"{nameof(WorldScaleController)}.{nameof(EnterMacroWorldAsync)} called while a transition was already in flight.");

            _isTransitioning = true;
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

        // --- Other systems coupled to world scale (REFACTORING_PLAN.md Phase 4 item 5) ---
        //
        // These are real GamePlayState.State/WorldState dependents found while reading the old code for this
        // phase. None of them are ported here - each belongs to a system outside "world scale" itself (particles,
        // bullets, a virus registry, player movement feel), and porting them is explicitly out of this phase's
        // scope per the brief. Listed here as precise pointers for whoever migrates each system next, so they know
        // to read WorldScaleController.CurrentScale (or subscribe to OnScaleChanged/OnEnteredMicroWorld/
        // OnExitedMicroWorld) instead of re-inventing a GamePlayState-shaped dependency:
        //
        // 1. GMTK.PlayerParticlesController (Assets/GMTK/Scripts/Behaviors/Player/PlayerParticlesController.cs)
        //    - Start() (line 24-25) subscribes to `GamePlayState.OnWorldChanged`; ChangeParticle() (lines 33-36)
        //    sets a `_microWorldValue` multiplier to 3f in MICRO_WORLD, 1f otherwise, which ChangeEmission()
        //    (lines 37-41) folds into the trail particle system's `rateOverTime`. Whoever ports
        //    PlayerParticlesController should read `WorldScaleController.CurrentScale == WorldScale.Micro` (or
        //    subscribe to OnScaleChanged) in place of the old ServiceLocator/GamePlayState lookup.
        //
        // 2. GMTK.Bullet (Assets/GMTK/Scripts/Behaviors/Bullet/Bullet.cs) - Update() (line 21) multiplies bullet
        //    travel speed by 0.3x when `_gamePlayState.State == WorldState.MICRO_WORLD`; OnEnable() (lines 34-35)
        //    sets the bullet's `transform.localScale` to (0.3, 0.3, 1) in micro, (1, 1, 1) in macro. Same
        //    replacement: read WorldScaleController.CurrentScale instead of GamePlayState.State.
        //
        // 3. GMTK.GameStates.GamePlayState.State's setter itself (GameplayState.cs:46, :54) also toggles
        //    `_virusManager.gameObject.SetActive(false/true)` - hides/shows the macro virus spawner+AI while the
        //    player is inside a cell. No _Project equivalent of GMTK.VirusManager exists yet (Phase 3's own doc
        //    comments confirm no cell/virus registry has been ported - see CellInfectionState.cs's class comment).
        //    A future virus-registry port should subscribe to OnEnteredMicroWorld/OnExitedMicroWorld to
        //    pause/resume itself the same way.
        //
        // 4. Player movement feel: old GMTK.Services.PlayerStats.SpeedScale (PlayerStats.cs:14-30) is set to 1f/
        //    0.01f by TransitionIntoMacroWorld.cs:35/TransitionIntoMicroWorld.cs:36 and consumed by GMTK.
        //    PlayerBodyController.FixedUpdate (PlayerBodyController.cs:41 - `_body.Move(inputValue.y *
        //    _playerStats.SpeedScale * backMovement)`), throttling forward/backward thrust input to 1% in micro
        //    world. This is arguably PART of the world-scale mechanic per GDD.md section 2 ("швидкість
        //    переміщення падає до 1% від макро-швидкості"), and WorldScaleTransition DOES still call
        //    `context.SetPlayerSpeedScale?.Invoke(...)` (see that class) so the STEP is preserved end-to-end -
        //    but Phase 2's PlayerMovement (Assets/_Project/Scripts/Mechanics/Player/PlayerMovement.cs) has no
        //    SpeedScale-equivalent hook to actually consume it yet (its FixedUpdate reads raw input with no such
        //    multiplier), and this phase is not permitted to edit Phase-2 files. So the value flows out of
        //    WorldScaleTransition correctly, but nothing on the PlayerMovement side reads it yet - a genuine gap,
        //    not merely a documentation seam like items 1-3 above. Whoever next touches PlayerMovement should add
        //    a settable speed-scale multiplier consumed in FixedUpdate's `Move(...)`/`Rotate(...)` calls and wire
        //    WorldScaleTransitionContext.SetPlayerSpeedScale to it.
    }
}
