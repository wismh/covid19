using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lumenwake.UIModule;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Audio;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Combat;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Micro;
using _Project.Mechanics.WorldScale;
using _Project.UI.GameOver;
using _Project.UI.Hud;
using _Project.UI.Upgrades;
using CameraFollow = _Project.Mechanics.Camera.CameraFollow;

namespace _Project.Mechanics.Composition
{
    /// <summary>
    /// Phase 7 Stage 2 (REFACTORING_PLAN.md "Композиція та реальна інтеграція", п.2). Originally a fully
    /// hand-composed "arena builder" that `new`-ed every Mechanics/* POCO itself and wired every cross-system
    /// event by hand in one place (mirroring `td-over-unity`'s `MainSceneView.BuildArena()`).
    ///
    /// --- 2026-09-20 "MainCompositionRoot split into installers" ---
    /// The user didn't like this class doing everything (SerializeField config for every feature, cross-system
    /// event glue, game-over/save logic, the cell-proximity poll loop, all in one ~680-line file) and asked for
    /// it split into several installers, referencing space-bac/tactics-cards as the pattern to follow. Most of
    /// what used to live here was already-vestigial [SerializeField]/[Inject] copies duplicating what
    /// GameplaySceneInstaller's feature installers now bind directly (dead fields removed this round:
    /// _cellConfig, _macroVirusConfig, _waveScalingConfig, _upgradeDefinitions, _gameAudioCatalog,
    /// _cellSpawner, _macroVirusSpawner, _playerParticlesController, _arrowController - none of them were
    /// actually read anywhere in this class's body) or genuine cross-system event wiring with no dependency on
    /// anything Awake-timing-sensitive - that wiring is now five small `IInitializable`/`IDisposable` driver
    /// services (<see cref="MacroVirusReplacementDriver"/>, <see cref="MacroVirusPauseDriver"/>,
    /// <see cref="VirusExperienceDriver"/>, <see cref="HudStatisticsDriver"/>, <see cref="GameOverDriver"/>)
    /// bound by <see cref="GameplayEventWiringInstaller"/>, the same static-Install-helper shape
    /// PlayerInstaller/CellsInstaller/etc. already use.
    ///
    /// What's LEFT here, and why none of it could move to an installer:
    /// 1. <see cref="ResolvePlayerComponentReferences"/> - a runtime workaround for a Unity engine quirk (stripped
    ///    PrefabInstance references after a real `SceneManager.LoadScene` call) that only manifests once Awake()
    ///    actually runs on a real scene load - not expressible as a container binding.
    /// 2. <see cref="BuildWorldScaleTransitionContext"/>/<see cref="WorldScaleTransitionContext"/> itself - a
    ///    plain data holder whose per-cell fields (Cell/CellVisuals/CellRigidbody2D/...) are mutated every poll
    ///    tick by <see cref="PollNearestCellAsync"/> below, not a singleton service; deliberately not a container
    ///    binding (see that class's own doc comment - "a future composition root constructs from whatever live
    ///    scene references it holds").
    /// 3. One piece of the old WireCrossSystemEvents survives here rather than in a driver -
    ///    <see cref="WireForcedMacroExitOnCellDeath"/> - specifically because it reads
    ///    <see cref="_worldScaleTransitionContext"/>.Cell (owned here, not container-bound) to decide whether the
    ///    cell that just died is the one the player is actually standing in.
    /// 4. <see cref="_bulletManager"/>'s hand-composed Construct call (deliberately never `[Inject]`-tagged, see
    ///    its own class doc comment) and the initial <c>OpenScreen&lt;HudScreen&gt;()</c>/music-start calls,
    ///    which are Awake-order-sensitive startup sequencing rather than reactive event wiring - moving them to
    ///    an `IInitializable` (which Zenject runs BEFORE any scene MonoBehaviour's own Awake, strictly earlier
    ///    than today) risked calling into HudScreen/BaseScreenManager before their own Awake() has run, an
    ///    ordering guarantee this class's existing Awake-based sequencing doesn't need to disturb for no benefit.
    ///
    /// --- Namespace/folder choice: _Project.Mechanics.Composition, not _Project.Bootstrap ---
    /// `_Project.Bootstrap` (Bootstrap.cs, ProjectContextInstaller.cs) is the App-flow/Zenject-container layer -
    /// it decides WHICH scene loads and WHAT platform services exist, and knows nothing about cells, viruses, or
    /// player stats. This class is the opposite: it knows nothing about scene loading or the DI container's
    /// installation graph, and everything about gameplay POCOs and their events - the same "gameplay wiring, not
    /// app wiring" split REFACTORING_PLAN.md section 4 draws between ProjectContextInstaller (Zenject, platform)
    /// and a hand-composed arena builder (Mechanics/*, gameplay).
    ///
    /// --- Execution order: everything happens in Awake(), not Start() ---
    /// CellSpawner.Start() spawns 20 cells immediately (see its own doc comment: "Deferred to Start() ... so
    /// Construct() ... has definitely happened first"), and PlayerHealth.Start()/PlayerMovement's Effective* reads
    /// need PlayerStatsService to already exist. Unity guarantees every scene object's Awake() runs before ANY
    /// object's Start() runs (for everything present in the scene at load) - so every Construct(...) call and
    /// every event subscription below happens in THIS class's Awake(), never Start(), to land inside that window
    /// regardless of GameObject/script execution order between siblings. This class's own `[Inject] Construct`
    /// (see below) is guaranteed by Zenject to run before Unity calls Awake() on an injected MonoBehaviour.
    /// </summary>
    public sealed class MainSceneComposition : MonoBehaviour
    {
        [Header("Player (Phase 2) - PlayerMovement/PlayerTurret/PlayerHealth already placed on the player GameObject")]
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerTurret _playerTurret;
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private _Project.Mechanics.Combat.BulletManager _bulletManager;

        [Header("World scale (Phase 4)")]
        [SerializeField] private UnityEngine.Camera _worldCamera;
        [SerializeField] private _Project.Mechanics.Camera.CameraFollow _cameraFollow;
        [SerializeField] private Transform _playerVisualRoot;
        [SerializeField] private BoxCollider2D _playerCollider;

        [Header("UI screens (Phase 5) - already placed on the gameplay Canvas, only Construct-wired here")]
        [SerializeField] private HudScreen _hudScreen;
        [SerializeField] private UpgradeScreen _upgradeScreen;
        [SerializeField] private GameOverScreen _gameOverScreen;
        [SerializeField] private CellProximityPrompt _cellProximityPrompt;

        [Header("Cell-proximity polling (see class doc comment's \"CellProximityPrompt polling\" section)")]
        [SerializeField] private float _cellProximityPollIntervalSeconds = 0.2f;

        // --- Zenject-bound: scene-level, from GameplayScene's SceneContext (GameplaySceneInstaller + feature
        // XxxInstaller.Install helpers, UiInstaller for IScreenManager) - see this class's Construct method. ---
        private IScreenManager _screenManager;
        private PlayerStatsService _playerStats;
        private CellRegistry _cellRegistry;
        private WorldScaleController _worldScaleController;
        private GameAudioService _gameAudioService;

        // --- Still hand-built here: a plain data holder, not a DI-managed type (see BuildWorldScaleTransitionContext) ---
        private WorldScaleTransitionContext _worldScaleTransitionContext;

        // Cached delegate so the lambda subscription can be symmetrically unsubscribed in OnDestroy (matching
        // this codebase's PlayerHealth/CellHealth/CellVisuals +=/-= discipline instead of relying on GC).
        private Action<Vector3, int> _handleCellDiedForceMacroExit;

        private CancellationTokenSource _lifetimeCts;

        // Bullet pooling migration (see Bullet/BulletManager/PlayerInstaller doc comments): BulletManager.Construct
        // stays hand-composed (deliberately not [Inject]-tagged, see its own doc comment), so this class resolves
        // the pool's Factory too and forwards it alongside the rest in ConstructSpawners().
        private Bullet.Factory _bulletFactory;

        [Inject]
        public void Construct(
            PlayerStatsService playerStats,
            CellRegistry cellRegistry,
            WorldScaleController worldScaleController,
            GameAudioService gameAudioService,
            IScreenManager screenManager,
            Bullet.Factory bulletFactory)
        {
            _playerStats = playerStats;
            _cellRegistry = cellRegistry;
            _worldScaleController = worldScaleController;
            _gameAudioService = gameAudioService;
            _screenManager = screenManager;
            _bulletFactory = bulletFactory;
        }

        private void Awake()
        {
            _lifetimeCts = new CancellationTokenSource();

            ResolvePlayerComponentReferences();

            HideOverlayScreensOnStart();
            BuildWorldScaleTransitionContext();
            ConstructSpawners();
            ConstructScreens();
            WireForcedMacroExitOnCellDeath();

            _gameAudioService.PlayGameplayMusic();

            // HudScreen is the one screen guaranteed open for the whole gameplay session (see its own class doc
            // comment's "Auto-opening" section) - nothing else in the codebase opens it, so this composition root
            // does, once, at gameplay start.
            _screenManager.OpenScreen<HudScreen>().Forget();

            PollNearestCellAsync(_lifetimeCts.Token).Forget();
        }

        /// <summary>
        /// Phase 7 testing fix (2026-09-11): wires the old-GMTK-ServiceLocator dependencies this composition
        /// root needs (<see cref="GMTK.CameraFollow"/>, <see cref="GMTK.BulletManager"/>,
        /// <see cref="GMTK.Services.PlayerStats"/>) - all three still fully active/registering themselves in
        /// GameplayScene.unity (deliberately kept running per Phase 5's "keep the old game playable" rule), each
        /// registering itself via GMTK.Services.ServiceLocator in its own Awake() (PlayerStats itself is
        /// registered by the old BootstrapState, Assets/GMTK/Scripts/GameStates/BootstrapState.cs, which still
        /// runs alongside the new _Project.Bootstrap flow). Deliberately done in Start(), not Awake(), even
        /// though everything else in this class runs in Awake() (see this class's own doc comment, "Execution
        /// order" section): Unity only guarantees Awake() order WITHIN a single object's Awake() call, not
        /// BETWEEN different GameObjects' Awake() calls, so reading ServiceLocator here from this class's own
        /// Awake() risked running before these services had registered themselves. Start() is safe because Unity
        /// guarantees every object's Awake() completes before ANY object's Start() runs.
        ///
        /// Camera: old PlayerFactory.Start() (Assets/GMTK/Scripts/Behaviors/Player/PlayerFactory.cs) set
        /// CameraFollow.Target after spawning the runtime player - PlayerFactory has been fully disabled since
        /// Phase 7 Stage 6 (Player.prefab is now placed statically instead), so nothing has set Target since,
        /// and the camera never moved.
        ///
        /// Shooting: see PlayerTurret.SetProjectileSpawner/GmtkBulletManagerProjectileSpawner.SetBulletManager's
        /// doc comments - Player.prefab never had a GmtkBulletManagerProjectileSpawner component before this
        /// testing round, so Fire() was a silent no-op regardless of BulletManager's own state.
        ///
        /// PlayerStats.Player: old PlayerFactory.CreatePlayer() also did `_playerStats.Player = instance;`
        /// (the SAME line disabling PlayerFactory broke). Discovered indirectly, not from the user's original
        /// report: fixing shooting let a live Fire() actually hit a GMTK.Medical pickup for the first time,
        /// whose explosion spawns GMTK.Vitamin children that fly toward `_playerStats.Player.position` every
        /// frame (Vitamin.Update()) - a NullReferenceException storm that was previously unreachable because
        /// nothing could ever damage a Medical without working projectiles. GMTK.UI.AdviceDialog also reads
        /// this same field but already null-guards it (`if (_playerStats.Player == null) return;`), so it was
        /// never at risk - Vitamin was the one caller without a guard. Set here rather than patching Vitamin
        /// itself, since any other not-yet-triggered old caller of PlayerStats.Player gets fixed by the same
        /// one assignment instead of being chased down one crash report at a time.
        ///
        /// --- Second testing round (2026-09-11) additions ---
        ///
        /// Pickup XP bridge: GMTK.Medical.VitaminCallback() still calls the OLD PlayerStats.AddExperience()
        /// directly (Medical.cs is untouched old code, not part of this migration's file list) - that increments
        /// the OLD, ServiceLocator-only counter, completely disconnected from the NEW PlayerStatsService HudScreen/
        /// UpgradeScreen actually read from, so pickups visibly granted nothing. Old PlayerStats already exposes
        /// `OnExperienceChange` for exactly this kind of external observation - subscribing it to also call the
        /// NEW _playerStats.AddExperience() bridges every old XP source (today: only Medical/Vitamin, since the
        /// other two old callers - VirusController/InCellVirus - were removed outright, see that commit) without
        /// touching Medical.cs, and without double-counting (nothing else calls the old AddExperience() anymore).
        ///
        /// Arrow bridge: GMTK.Arrow/ArrowController (the "nearest virus/infected cell" world-space indicator) read
        /// old GMTK.VirusManager/VirusCellManager (plain ObjectManager list+event pools), which only the OLD,
        /// now-disabled GMTK.VirusSpawner/CellController ever populated - so the arrow had nothing to point at and
        /// never showed. Mirrors MacroVirusRegistry's add/remove and CellRegistry's new OnCellInfected/
        /// OnCellRecovered events into those old pools via their own AddObject/RemoveObject (plain bookkeeping,
        /// no transition/side-effect risk - unlike GamePlayState.State below, ObjectManager has none to avoid).
        ///
        /// Bullet scale in micro world: GMTK.Bullet still reads old GamePlayState.State (never MICRO_WORLD since
        /// nothing sets it anymore) to decide whether to shrink/slow itself 0.3x - user-reported symptom:
        /// oversized, full-speed projectiles inside a cell. Cannot just assign GamePlayState.State normally: that
        /// property's SETTER re-runs the old, independent TransitionIntoMicro/MacroWorld DOTween sequences on the
        /// same camera/player/cell transforms the NEW WorldScaleTransition already animates, which would double-
        /// transition everything. GamePlayState.SyncWorldState(WorldState) (new, additive-only method - see its
        /// own doc comment) sets just the backing field, keeping old readers correct without re-triggering old
        /// writers. Subscribed to WorldScaleController.OnScaleChanged so it fires exactly once per real transition,
        /// after it completes (matching this class's already-established "read the NEW controller, not old
        /// GamePlayState, as the source of truth" stance).
        ///
        /// Camera target while inside a cell: WorldScaleTransitionContext.SetCameraTarget was left null (see
        /// BuildWorldScaleTransitionContext's own comment) - old code re-pointed the camera at the CELL while
        /// inside it, the PLAYER once back outside. Wired here now that CameraFollow is resolvable.
        /// </summary>
        private void Start()
        {
            if (_cameraFollow != null && _playerMovement != null)
                _cameraFollow.SetTarget(_playerMovement.transform);

            if (_cameraFollow != null && _worldScaleTransitionContext != null)
                _worldScaleTransitionContext.SetCameraTarget = t => _cameraFollow.SetTarget(t);
        }

        private void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();

            if (_cellRegistry != null)
            {
                _cellRegistry.OnAllCellsDied -= HandleAllCellsDiedForceMacroExit;
                _cellRegistry.OnCellDied -= _handleCellDiedForceMacroExit;
            }

            _gameAudioService?.Dispose();
        }

        /// <summary>
        /// Workaround for a reproducible Unity engine quirk, confirmed via Unity CLI Play Mode testing
        /// (2026-09-11, both directly and through the real Menu -&gt; GameplayScene flow): when this scene loads
        /// through a runtime <see cref="UnityEngine.SceneManagement.SceneManager.LoadScene(string)"/> call (the
        /// actual gameplay path - Menu's "Play" button, ported in MenuManager.LoadGameScene) rather than being
        /// opened directly in the Editor, the direct <c>[SerializeField]</c> "stripped" PrefabInstance
        /// placeholder references this class holds to <see cref="PlayerMovement"/>/<see cref="PlayerTurret"/>/
        /// <see cref="PlayerHealth"/> (custom MonoBehaviour types) deserialize as null, while references to
        /// built-in component types on the exact same Player GameObject (<see cref="_playerCollider"/>:
        /// BoxCollider2D, <see cref="_playerVisualRoot"/>: Transform) resolve correctly every time, through
        /// either load path. Cause not fully root-caused (looks like a Unity-internal PrefabInstance-override
        /// resolution quirk specific to custom-script MonoBehaviour references under a runtime scene load), but
        /// re-resolving these custom-script references via GetComponent off the reliably-resolving
        /// <see cref="_playerCollider"/> sidesteps it entirely - the same "sibling-component GetComponent"
        /// idiom CellSpawner.SpawnCell already uses for freshly-instantiated cells. Only fills in whichever
        /// fields the direct wiring left null, so a correctly-resolved direct reference (e.g. the Editor-direct-
        /// Play path, where it already works) is left untouched.
        /// </summary>
        private void ResolvePlayerComponentReferences()
        {
            if (_playerCollider == null)
                return;

            if (_playerMovement == null)
                _playerMovement = _playerCollider.GetComponent<PlayerMovement>();
            if (_playerTurret == null)
                _playerTurret = _playerCollider.GetComponent<PlayerTurret>();
            if (_playerHealth == null)
                _playerHealth = _playerCollider.GetComponent<PlayerHealth>();
            if (_bulletManager == null)
                _bulletManager = FindFirstObjectByType<_Project.Mechanics.Combat.BulletManager>();
        }

        /// <summary>
        /// Phase 8 testing fix (2026-09-11), still needed post-DI-migration: UpgradeScreen/GameOverScreen live on
        /// the same "HUD/UpgradeGroup"/"HUD/GameOverGroup" GameObjects the old GMTK.UI.HUD reused from - and the
        /// SCENE FILE itself has both marked active (an Editor-authoring artifact, not something either
        /// BaseScreenManager or BaseScreen ever accounted for). Old GMTK.UI.HUD.Awake() silently papered over this
        /// every time; removing that component removed the only thing hiding them, so both panels - plus their
        /// Upgrade-choice/"You Died" overlays underneath - were suddenly visible from the very first frame.
        /// BaseScreenManager (now built by UiInstaller, see Construct's doc comment) has no general "every
        /// registered screen starts hidden unless opened" invariant of its own, so this is the composition root's
        /// job. HudScreen is deliberately NOT included - it's the one screen guaranteed open for the whole
        /// gameplay session (see Awake's OpenScreen&lt;HudScreen&gt;() call), which sets it active anyway.
        /// </summary>
        private void HideOverlayScreensOnStart()
        {
            if (_upgradeScreen != null)
                _upgradeScreen.gameObject.SetActive(false);

            if (_gameOverScreen != null)
                _gameOverScreen.gameObject.SetActive(false);
        }

        // --- WorldScaleTransitionContext - still hand-built here, a plain data holder rather than a DI-managed
        // type (its per-cell fields change every poll tick, see PollNearestCellAsync). Every Mechanics-POCO this
        // class used to `new` by hand (PlayerStatsService, CellRegistry, MacroVirusRegistry, WorldScaleController,
        // GameAudioService) is [Inject]-ed via Construct or, for cross-system events, resolved by the
        // GameplayEventWiringInstaller drivers instead - see class doc comment. ---

        private void BuildWorldScaleTransitionContext()
        {
            // Built once; Cell/CellVisuals/CellRigidbody2D/CellAnimator are filled in reactively by the
            // proximity-polling loop below (see PollNearestCellAsync) since "which cell" changes at runtime and
            // isn't something a [SerializeField] can capture - see class doc comment's "CellProximityPrompt
            // polling" section and WorldScaleTransitionContext's own per-field doc comments for why the other
            // fields (SetCameraTarget, SetPlayerSpeedScale, the audio-mixer fields) are deliberately left null.
            _worldScaleTransitionContext = new WorldScaleTransitionContext
            {
                Player = _playerMovement != null ? _playerMovement.transform : null,
                PlayerVisualRoot = _playerVisualRoot,
                PlayerCollider = _playerCollider,
                PlayerMovement = _playerMovement,
                Camera = _worldCamera,
                SetCameraTarget = t =>
                {
                    if (_cameraFollow != null)
                        _cameraFollow.SetTarget(t);
                },

                // Phase 7 testing fix (2026-09-11): PlayerMovement.SetSpeedScale now exists (see its own doc
                // comment - user-reported symptom was movement far too fast inside a cell) and needs no
                // ServiceLocator lookup, so it's wired here directly rather than waiting for Start().
                SetPlayerSpeedScale = _playerMovement != null ? _playerMovement.SetSpeedScale : (Action<float>)null,

                // --- Deliberately left null: PlayerAudioSource / PlayerEnterMacroClip / PlayerEnterMicroClip /
                // GameplayMusicAudioSource / MacroMixerGroup / MicroMixerGroup ---
                // Phase 5's own doc comment (GameAudioService class comment, "Виявлена, але не вирішена колізія")
                // names this exact collision and recommends a resolution: "рекомендація агента: GameAudioService,
                // поля контексту прибрати." This composition root follows that recommendation - GameAudioService
                // already subscribes directly to WorldScaleController.OnEnteredMicroWorld/OnExitedMicroWorld and
                // plays the whoosh cues itself (see GameAudioService's own class doc comment). Populating these
                // context fields TOO would make the whoosh play twice per transition. The mixer-group swap
                // (GameplayMusicAudioSource/MacroMixerGroup/MicroMixerGroup) stays an open gap either way - see
                // GameAudioService's "What this seam does NOT (yet) do" section - Editor-authoring work, not
                // something this field could fix even if populated.
            };
        }

        // --- Spawners: only BulletManager remains hand-composed here. CellSpawner/MacroVirusSpawner/
        // PlayerMovement/PlayerTurret/PlayerHealth all have [Inject]-tagged Construct methods and are statically
        // placed in GameplayScene, so SceneContext calls their Construct automatically (see
        // GameplaySceneInstaller/PlayerInstaller/CellsInstaller/VirusesInstaller/WorldScaleInstaller).
        // BulletManager's Construct method was never [Inject]-tagged (see its own doc comment) and stays
        // hand-composed here, using the injected _playerStats/_bulletFactory this class receives via Construct.

        private void ConstructSpawners()
        {
            if (_bulletManager != null)
                _bulletManager.Construct(_playerStats, _bulletFactory);

            if (_playerTurret != null)
                _playerTurret.SetProjectileSpawner(_bulletManager);
        }

        // --- UI screens: only the non-Construct setup calls remain here - HudScreen/UpgradeScreen/GameOverScreen/
        // CellProximityPrompt all have [Inject]-tagged Construct methods called automatically by SceneContext.

        private void ConstructScreens()
        {
            if (_cellProximityPrompt != null)
            {
                _cellProximityPrompt.SetContext(_worldScaleTransitionContext);

                // See CellProximityPrompt.SetPlayer's doc comment - nothing wired _player in GameplayScene.unity,
                // so the proximity prompt/E-key handler were silently inert.
                if (_playerMovement != null)
                    _cellProximityPrompt.SetPlayer(_playerMovement.transform);
            }
        }

        // --- Forced macro-exit on cell death - the one WireCrossSystemEvents concern that stayed here instead
        // of moving to a GameplayEventWiringInstaller driver, see class doc comment point 3. ---

        private void WireForcedMacroExitOnCellDeath()
        {
            // Phase 8 testing fix (2026-09-11): old CellController.Die() forced the player back to macro world
            // BEFORE checking Pool.Count == 0 (`if (nearestCell == transform) GamePlayState.State =
            // WorldState.MACRO_WORLD;` runs unconditionally, ahead of the OnCellDied/OnAllCellsDied-equivalent
            // branch below it) - CellHealth.OnDied's own doc comment explicitly scoped this out as a gap ("old
            // CellController.Die() additionally forced the player out of macro world if they were inside the
            // dying cell... None of that is reproduced here"). User-reported symptom: the player stayed shrunk
            // to micro scale forever if their cell died while they were inside it. Position-matched against
            // WorldScaleTransitionContext.Cell (the cell the player actually entered - see this class's
            // proximity-polling loop) rather than trusting OnCellDied's deathPosition alone, since an unrelated,
            // far-away cell dying should never eject the player from the one they're actually standing in.
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

        /// <summary>
        /// Old: CellController.Die()'s `if (_cellManager.Pool.Count == 0)` branch's force-macro-exit half - see
        /// <see cref="WireForcedMacroExitOnCellDeath"/>'s doc comment. The other half (showing GameOverScreen)
        /// is <see cref="GameOverDriver"/>'s job - both subscribe independently to
        /// <see cref="CellRegistry.OnAllCellsDied"/>, which is safe since neither depends on the other running
        /// first (this one's EnterMacroWorldSafeAsync().Forget() is non-blocking).
        /// </summary>
        private void HandleAllCellsDiedForceMacroExit()
        {
            if (_worldScaleController.CurrentScale == _Project.Mechanics.WorldScale.WorldScale.Micro)
                EnterMacroWorldSafeAsync().Forget();
        }

        /// <summary>See WireForcedMacroExitOnCellDeath's/HandleAllCellsDiedForceMacroExit's doc comments. Mirrors
        /// CellProximityPrompt.EnterMacroWorldSafeAsync's own try/catch shape.</summary>
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

        /// <summary>
        /// Old: `GMTK.UI.AdviceDialog.Update()`'s `_cellManager.FindNearToPoint(player.position, out sqrDistance)`
        /// call, re-run every frame. CellRegistry.FindNearestCell (Phase 7 Stage 1) is the direct replacement, but
        /// its own doc comment is explicit that IT does not decide the polling cadence - "re-run periodically by
        /// whatever Stage 2/3 wires up ... this registry doesn't decide the polling cadence itself". This
        /// composition root is that "whoever" - a small UniTask + CancellationTokenSource loop (matching the
        /// UniTask idiom used everywhere else in this codebase, e.g. CellHealth.PoisonTickLoopAsync,
        /// MicroVirusSpawner has no such loop but MacroVirusSpawner.Update() polls every frame instead - this
        /// loop intentionally polls on a fixed interval rather than every frame, since "nearest cell" changes far
        /// slower than player movement and a several-times-a-second check is indistinguishable to the player from
        /// a per-frame one while costing much less).
        ///
        /// Each tick also keeps <see cref="_worldScaleTransitionContext"/>'s per-cell fields (Cell/CellVisuals/
        /// CellRigidbody2D/CellAnimator) in sync with whichever cell is currently nearest - CellProximityPrompt
        /// itself only tracks a target Transform (via SetTargetCell), not the sibling components
        /// WorldScaleTransition needs once the player actually presses E to enter it, and nothing else in the
        /// codebase keeps those two in sync. Using typed GetComponent&lt;T&gt;() here (not a string-based
        /// transform.Find) on a freshly-resolved target, matching every other typed-lookup convention this
        /// migration already established (CellVisuals/CellHealth/CellInfectionState sibling references, etc.).
        /// </summary>
        private async UniTaskVoid PollNearestCellAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                UpdateNearestCellTarget();

                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_cellProximityPollIntervalSeconds), cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void UpdateNearestCellTarget()
        {
            if (_cellRegistry == null || _playerMovement == null)
                return;

            Transform nearestCell = _cellRegistry.FindNearestCell(_playerMovement.transform.position, out _);

            if (_cellProximityPrompt != null)
                _cellProximityPrompt.SetTargetCell(nearestCell);

            if (_worldScaleTransitionContext != null)
            {
                _worldScaleTransitionContext.Cell = nearestCell;
                _worldScaleTransitionContext.CellVisuals = nearestCell != null ? nearestCell.GetComponent<CellVisuals>() : null;
                _worldScaleTransitionContext.CellRigidbody2D = nearestCell != null ? nearestCell.GetComponent<Rigidbody2D>() : null;
                _worldScaleTransitionContext.CellAnimator = nearestCell != null ? nearestCell.GetComponent<Animator>() : null;

                // Phase 7 testing fix (2026-09-11) - see WorldScaleTransitionContext.CellMicroVirusSpawner's doc
                // comment for why these are resolved here (same per-poll pattern as CellVisuals/CellRigidbody2D/
                // CellAnimator above) rather than reactively wired at spawn time.
                _worldScaleTransitionContext.CellMicroVirusSpawner = nearestCell != null ? nearestCell.GetComponent<MicroVirusSpawner>() : null;
                _worldScaleTransitionContext.CellInfectionState = nearestCell != null ? nearestCell.GetComponent<CellInfectionState>() : null;
            }
        }

        // --- Per-cell in-cell-virus wiring: RESOLVED, 2026-09-11 (was a documented gap above this line) ---
        //
        // CellInfectionState.OnVirusCountChanged -> MicroVirusSpawner.SetVirusesCount is now wired directly in
        // CellSpawner.SpawnCell() (see that method), the exact spot this comment used to point at as the natural
        // place for it - MicroVirusSpawner was added to Cell.prefab, and CellSpawner resolves IPlayerTargetProvider
        // itself via its own [Inject] Construct (bound by GameplaySceneInstaller - see its own doc comment).
    }
}
