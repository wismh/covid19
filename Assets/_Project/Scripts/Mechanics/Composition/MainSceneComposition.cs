using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lumenwake.UIModule;
using Project.Core.AudioSystem;
using UnityEngine;
using Zenject;
using _Project.Mechanics.AppFlow;
using _Project.Mechanics.Audio;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Combat;
using _Project.Mechanics.Navigation;
using _Project.Mechanics.Pickups;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Micro;
using _Project.Mechanics.Viruses.Macro;
using _Project.Mechanics.Waves;
using _Project.Mechanics.WorldScale;
using _Project.Save;
using _Project.UI.GameOver;
using _Project.UI.Hud;
using _Project.UI.Upgrades;
using CameraFollow = _Project.Mechanics.Camera.CameraFollow;

namespace _Project.Mechanics.Composition
{
    /// <summary>
    /// Phase 7 Stage 2 (REFACTORING_PLAN.md "Композиція та реальна інтеграція", п.2), updated 2026-09-19 by the
    /// SceneContext/modular-Installer DI migration (see Docs/REFACTORING_PLAN.md's "Пост-міграційний
    /// DI-рефакторинг" entry). Originally a fully hand-composed "arena builder" that `new`-ed every Mechanics/*
    /// POCO itself (mirroring `td-over-unity`'s `MainSceneView.BuildArena()`) - those POCOs
    /// (PlayerStatsService, CellRegistry, MacroVirusRegistry, WorldScaleController, GameAudioService) are now
    /// bound in GameplayScene's SceneContext container instead (GameplaySceneInstaller + its feature
    /// XxxInstaller.Install helpers) and reach this class via `[Inject] Construct`. This class remains the
    /// composition root for the cross-system event glue that isn't naturally a container binding
    /// (`WireCrossSystemEvents`) and for `WorldScaleTransitionContext` (a plain data holder whose per-cell
    /// fields change every poll tick, not a singleton service) - the same seam every prior phase's doc comments
    /// (PlayerHealth.Construct, CellSpawner's "Registry seam", MacroVirusSpawner's "Cross-system wiring left to
    /// Stage 2", HudScreen's "Composition-root note", CellProximityPrompt's two documented seams) already
    /// pointed at by name.
    ///
    /// --- Namespace/folder choice: _Project.Mechanics.Composition, not _Project.Bootstrap ---
    /// `_Project.Bootstrap` (Bootstrap.cs, ProjectContextInstaller.cs) is the App-flow/Zenject-container layer -
    /// it decides WHICH scene loads and WHAT platform services exist, and knows nothing about cells, viruses, or
    /// player stats. This class is the opposite: it knows nothing about scene loading or the DI container's
    /// installation graph, and everything about gameplay POCOs and their events - the same "gameplay wiring, not
    /// app wiring" split REFACTORING_PLAN.md section 4 draws between ProjectContextInstaller (Zenject, platform)
    /// and a hand-composed arena builder (Mechanics/*, gameplay). Filed under Mechanics/Composition/ rather than
    /// Bootstrap/ for that reason, and rather than a bare `_Project.Mechanics` for discoverability - a future
    /// Menu-scene equivalent (if one is ever needed beyond MainMenuScreen's own trivial two-button Construct) would
    /// have an obvious sibling home here too.
    ///
    /// --- SerializeField vs [Inject] since the DI migration ---
    /// Most `[SerializeField]` config assets below (PlayerConfig, CellConfig, etc.) are ALSO now bound in the
    /// SceneContext container by GameplaySceneInstaller, so the statically-placed spawner/screen MonoBehaviours
    /// can resolve their own `[Inject] Construct` directly - this class keeps its own copies too, only where
    /// still needed for `BuildWorldScaleTransitionContext`/the two spawner Construct calls that were never
    /// `[Inject]`-tagged (`MedicalSpawner`, `BulletManager`). See `Construct`'s own doc comment for the full
    /// list of what's now `[Inject]`-resolved on this class itself.
    ///
    /// --- Execution order: everything happens in Awake(), not Start() ---
    /// CellSpawner.Start() spawns 20 cells immediately (see its own doc comment: "Deferred to Start() ... so
    /// Construct() ... has definitely happened first"), and PlayerHealth.Start()/PlayerMovement's Effective* reads
    /// need PlayerStatsService to already exist. Unity guarantees every scene object's Awake() runs before ANY
    /// object's Start() runs (for everything present in the scene at load) - so every Construct(...) call and
    /// every event subscription below happens in THIS class's Awake(), never Start(), to land inside that window
    /// regardless of GameObject/script execution order between siblings. This class's own `[Inject] Construct`
    /// (see below) is guaranteed by Zenject to run before Unity calls Awake() on an injected MonoBehaviour - the
    /// same ordering guarantee every Phase 2-5 `[Inject] Construct` + separate `Awake()` pair already relies on.
    /// </summary>
    public sealed class MainSceneComposition : MonoBehaviour
    {
        [Header("Player (Phase 2) - PlayerMovement/PlayerTurret/PlayerHealth already placed on the player GameObject")]
        [SerializeField] private List<UpgradeDefinition> _upgradeDefinitions;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerTurret _playerTurret;
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private PlayerParticlesController _playerParticlesController;
        [SerializeField] private ArrowController _arrowController;
        [SerializeField] private _Project.Mechanics.Combat.BulletManager _bulletManager;

        [Header("Cells (Phase 3 components + Phase 7 Stage 1 registry/spawner)")]
        [SerializeField] private CellConfig _cellConfig;
        [SerializeField] private CellSpawner _cellSpawner;

        [Header("Macro viruses (Phase 3 components + Phase 7 Stage 1 registry/spawner)")]
        [SerializeField] private MacroVirusConfig _macroVirusConfig;
        [SerializeField] private MacroVirusSpawner _macroVirusSpawner;

        [Header("Medicals / Pickups (Stage 4)")]
        [SerializeField] private PickupsConfig _pickupsConfig;
        [SerializeField] private MedicalSpawner _medicalSpawner;

        [Header("Waves (Phase 3) - shared by MacroVirusSpawner today; MicroVirusSpawner will need it too once " +
                "Stage 3 attaches one per cell, see class doc comment's per-cell virus seam")]
        [SerializeField] private WaveScalingConfig _waveScalingConfig;

        [Header("World scale (Phase 4)")]
        [SerializeField] private UnityEngine.Camera _worldCamera;
        [SerializeField] private _Project.Mechanics.Camera.CameraFollow _cameraFollow;
        [SerializeField] private Transform _playerVisualRoot;
        [SerializeField] private BoxCollider2D _playerCollider;

        [Header("Audio (Phase 5)")]
        [SerializeField] private GameAudioCatalog _gameAudioCatalog;

        [Header("UI screens (Phase 5) - already placed on the gameplay Canvas, only Construct-wired here")]
        [SerializeField] private HudScreen _hudScreen;
        [SerializeField] private UpgradeScreen _upgradeScreen;
        [SerializeField] private GameOverScreen _gameOverScreen;
        [SerializeField] private CellProximityPrompt _cellProximityPrompt;

        [Header("Cell-proximity polling (see class doc comment's \"CellProximityPrompt polling\" section)")]
        [SerializeField] private float _cellProximityPollIntervalSeconds = 0.2f;

        // --- Zenject-bound: project-level platform services (ProjectContextInstaller.cs) ---
        private IAudioSystem _audioSystem;
        private IGameSaveService _gameSaveService;
        private GlobalGameStateMachine _stateMachine;

        // --- Zenject-bound: scene-level, from GameplayScene's SceneContext (GameplaySceneInstaller + feature
        // XxxInstaller.Install helpers, UiInstaller for IScreenManager) - see this class's Construct method. ---
        private IScreenManager _screenManager;
        private PlayerStatsService _playerStats;
        private CellRegistry _cellRegistry;
        private MacroVirusRegistry _macroVirusRegistry;
        private WorldScaleController _worldScaleController;
        private GameAudioService _gameAudioService;
        private IPlayerTargetProvider _playerTargetProvider;

        // --- Still hand-built here: a plain data holder, not a DI-managed type (see BuildWorldScaleTransitionContext) ---
        private WorldScaleTransitionContext _worldScaleTransitionContext;

        // Cached delegates so lambda subscriptions can be symmetrically unsubscribed in OnDestroy (matching this
        // codebase's PlayerHealth/CellHealth/CellVisuals +=/-= discipline instead of relying on GC to clean up).
        private Action<Vector3, int> _handleCellDied;
        private Action<Vector3, int> _handleCellDiedForceMacroExit;
        private Action _handlePauseMacroSpawning;
        private Action _handleResumeMacroSpawning;
        private Action<MacroVirusController> _handleVirusRegistered;
        private Action<MicroVirusSpawner> _handleMicroVirusSpawnerCreated;

        private CancellationTokenSource _lifetimeCts;

        /// <summary>
        /// DI-migration update (2026-09-19, see Docs/REFACTORING_PLAN.md's "Пост-міграційний DI-рефакторинг"):
        /// GameplayScene now has a real `SceneContext` (`GameplaySceneInstaller` + feature `XxxInstaller.Install`
        /// helpers + `UiInstaller`), so this `[Inject]`-tagged method is called automatically by Zenject before
        /// Unity calls `Awake()` on this object - no more self-injection. Three params are project-level
        /// (`ProjectContextInstaller.cs`: `IAudioSystem`, `IGameSaveService`, `GlobalGameStateMachine`); the rest
        /// are scene-level, bound by the installers above. Every ScriptableObject config this class' spawners
        /// still reference directly via `[SerializeField]` (PlayerConfig, CellConfig, etc.) is ALSO now bound in
        /// the container (so the statically-placed Player/CellSpawner/MacroVirusSpawner/UI-screen MonoBehaviours
        /// can resolve their own `[Inject] Construct(...)` automatically) - this class keeps its own
        /// `[SerializeField]` copies too, only where still needed for `WorldScaleTransitionContext`/spawner calls
        /// that were never `[Inject]`-tagged (`MedicalSpawner`, `BulletManager` - see their own Construct doc
        /// comments, deliberately not migrated).
        /// </summary>
        // Bullet pooling migration (see Bullet/BulletManager/PlayerInstaller doc comments): BulletManager.Construct
        // stays hand-composed (deliberately not [Inject]-tagged, see its own doc comment), so this class resolves
        // the pool's Factory too and forwards it alongside the rest in ConstructSpawners().
        private Bullet.Factory _bulletFactory;

        [Inject]
        public void Construct(
            IAudioSystem audioSystem,
            IGameSaveService gameSaveService,
            GlobalGameStateMachine stateMachine,
            PlayerStatsService playerStats,
            CellRegistry cellRegistry,
            MacroVirusRegistry macroVirusRegistry,
            WorldScaleController worldScaleController,
            GameAudioService gameAudioService,
            IPlayerTargetProvider playerTargetProvider,
            IScreenManager screenManager,
            Bullet.Factory bulletFactory)
        {
            _audioSystem = audioSystem;
            _gameSaveService = gameSaveService;
            _stateMachine = stateMachine;
            _playerStats = playerStats;
            _cellRegistry = cellRegistry;
            _macroVirusRegistry = macroVirusRegistry;
            _worldScaleController = worldScaleController;
            _gameAudioService = gameAudioService;
            _playerTargetProvider = playerTargetProvider;
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
            WireCrossSystemEvents();

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
                if (_hudScreen != null)
                    _cellRegistry.OnStatisticsChanged -= _hudScreen.SetStatistics;

                _cellRegistry.OnAllCellsDied -= HandleAllCellsDied;
                _cellRegistry.OnCellDied -= _handleCellDied;
                _cellRegistry.OnCellDied -= _handleCellDiedForceMacroExit;
            }

            if (_worldScaleController != null)
            {
                _worldScaleController.OnEnteredMicroWorld -= _handlePauseMacroSpawning;
                _worldScaleController.OnExitedMicroWorld -= _handleResumeMacroSpawning;
            }

            if (_playerHealth != null)
                _playerHealth.OnDied -= HandleGameOver;

            if (_macroVirusRegistry != null)
                _macroVirusRegistry.OnRegistered -= _handleVirusRegistered;

            if (_cellSpawner != null)
                _cellSpawner.OnMicroVirusSpawnerCreated -= _handleMicroVirusSpawnerCreated;

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
        /// Play path, where it already works) is left untouched. Also resolves
        /// <see cref="_projectileSpawner"/> here (same sibling-component reasoning) even though nothing ever
        /// wires it directly - Player.prefab only gained the GmtkBulletManagerProjectileSpawner component as
        /// part of this same testing round (see PlayerTurret.SetProjectileSpawner's doc comment), so there was
        /// never a "direct wiring" path for it to begin with.
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
            if (_playerParticlesController == null)
                _playerParticlesController = _playerCollider.GetComponent<PlayerParticlesController>();
            if (_arrowController == null)
                _arrowController = _playerCollider.GetComponentInChildren<ArrowController>();
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

        // --- 1. WorldScaleTransitionContext - still hand-built here, a plain data holder rather than a
        // DI-managed type (its per-cell fields change every poll tick, see PollNearestCellAsync). Every other
        // Mechanics-POCO this class used to `new` by hand (PlayerStatsService, CellRegistry, MacroVirusRegistry,
        // WorldScaleController, GameAudioService) is now [Inject]-ed via Construct instead - see
        // GameplaySceneInstaller and its feature XxxInstaller.Install helpers. ---

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

        // --- 2. Spawners (Cells/Macro viruses) ---
        //
        // DI-migration update (2026-09-19): CellSpawner/MacroVirusSpawner/PlayerMovement/PlayerTurret/
        // PlayerHealth/PlayerParticlesController/ArrowController all have [Inject]-tagged Construct methods and
        // are statically placed in GameplayScene, so SceneContext now calls their Construct automatically (see
        // GameplaySceneInstaller/PlayerInstaller/CellsInstaller/VirusesInstaller/WorldScaleInstaller). Only
        // MedicalSpawner/BulletManager remain here - their Construct methods were never [Inject]-tagged (see
        // their own doc comments) and stay hand-composed, using the same injected _playerStats/
        // _worldScaleController/_playerTargetProvider this class now receives via Construct instead of `new`-ing
        // a second, separate IPlayerTargetProvider instance.

        private void ConstructSpawners()
        {
            if (_medicalSpawner != null)
                _medicalSpawner.Construct(_pickupsConfig, _playerStats, _playerTargetProvider);

            if (_bulletManager != null)
                _bulletManager.Construct(_playerStats, _bulletFactory);

            if (_playerTurret != null)
                _playerTurret.SetProjectileSpawner(_bulletManager);
        }

        // --- 3. UI screens ---
        // DI-migration update (2026-09-19): HudScreen/UpgradeScreen/GameOverScreen/CellProximityPrompt all have
        // [Inject]-tagged Construct methods and are statically placed in GameplayScene, so SceneContext now calls
        // their Construct automatically (IScreenManager comes from UiInstaller, everything else from
        // GameplaySceneInstaller's feature installers). Only the non-Construct setup calls remain here.

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

        // --- 4. Cross-system event wiring - the actual "glue" this stage exists to write ---

        private void WireCrossSystemEvents()
        {
            if (_hudScreen != null)
                _cellRegistry.OnStatisticsChanged += _hudScreen.SetStatistics;

            _cellRegistry.OnAllCellsDied += HandleAllCellsDied;

            // Phase 8 testing fix (2026-09-11): old CellController.Die() forced the player back to macro world
            // BEFORE checking Pool.Count == 0 (`if (nearestCell == transform) GamePlayState.State =
            // WorldState.MACRO_WORLD;` runs unconditionally, ahead of the OnCellDied/OnAllCellsDied-equivalent
            // branch below it) - CellHealth.OnDied's own doc comment explicitly scoped this out as a gap ("old
            // CellController.Die() additionally forced the player out of macro world if they were inside the
            // dying cell... None of that is reproduced here"). User-reported symptom: the player stayed shrunk
            // to micro scale forever if their cell died while they were inside it. Position-matched against
            // WorldScaleTransitionContext.Cell (the cell the player actually entered - see MainSceneComposition's
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

            if (_macroVirusSpawner != null)
            {
                _handleCellDied = (position, virusCountAtDeath) =>
                    _macroVirusSpawner.SpawnReplacementVirusesAsync(position, virusCountAtDeath).Forget();
                _cellRegistry.OnCellDied += _handleCellDied;

                _handlePauseMacroSpawning = () => _macroVirusSpawner.SetPaused(true);
                _handleResumeMacroSpawning = () => _macroVirusSpawner.SetPaused(false);
                _worldScaleController.OnEnteredMicroWorld += _handlePauseMacroSpawning;
                _worldScaleController.OnExitedMicroWorld += _handleResumeMacroSpawning;
            }

            if (_playerHealth != null)
                _playerHealth.OnDied += HandleGameOver;

            // Phase 7 testing fix (2026-09-11): XP-granting was never wired at all - MacroVirusController's own
            // doc comment names this exact gap ("XP-granting is a composition-root concern for a later phase"),
            // and this composition root never followed up on it. Old VirusController.Damage()'s death branch
            // called `_playerStats.AddExperience()` directly; this is the equivalent for the new architecture.
            // Subscribes to the REGISTRY's OnRegistered (not each spawner's per-instance OnKilled directly,
            // which this class has no access to until a virus already exists) so every macro virus - spawned
            // fresh, or as a cell-death replacement wave - gets exactly one XP-on-kill subscription. Deliberately
            // only OnKilled, not OnPenetratedIntoCell - old code never granted XP for a virus successfully
            // penetrating a cell, only for killing one; MacroVirusSpawner.SpawnVirus() already subscribes both
            // events to its own HandleVirusRemoved for unregister/destroy, this is an independent, additional
            // subscription for the XP side effect alone. No matching per-virus unsubscribe on death: the
            // subscribed virus instance is destroyed by MacroVirusSpawner immediately after (HandleVirusRemoved),
            // so the closure has nothing left to fire on - only the registry-level OnRegistered subscription
            // itself needs symmetric cleanup, done in OnDestroy() below.
            _handleVirusRegistered = virus => virus.OnKilled += () => _playerStats.AddExperience();
            _macroVirusRegistry.OnRegistered += _handleVirusRegistered;

            // Phase 8 testing fix (2026-09-11): the macro-virus XP wiring above has no in-cell equivalent -
            // in-cell viruses were killable, granted no XP at all. Old InCellVirus.Damage()'s death branch called
            // `_playerStats.AddExperience()` directly, same as old macro-virus code. Mirrors the shape above
            // exactly, adapted for the fact that in-cell viruses have no shared registry to subscribe to (each
            // cell owns a private MicroVirusRegistry) - CellSpawner.OnMicroVirusSpawnerCreated fires once per
            // cell, the moment that cell's own MicroVirusSpawner is constructed, so this subscribes the XP grant
            // to every cell's spawner exactly once. See MicroVirusSpawner.OnVirusKilled's doc comment.
            if (_cellSpawner != null)
            {
                _handleMicroVirusSpawnerCreated = spawner => spawner.OnVirusKilled += () => _playerStats.AddExperience();
                _cellSpawner.OnMicroVirusSpawnerCreated += _handleMicroVirusSpawnerCreated;
            }
        }

        /// <summary>
        /// Old: CellController.Die()'s `if (_cellManager.Pool.Count == 0)` branch, reproduced via
        /// CellRegistry.OnAllCellsDied (see CellRegistry's own "OnCellDied vs OnAllCellsDied" doc comment).
        /// </summary>
        private void HandleAllCellsDied()
        {
            // See _handleCellDiedForceMacroExit's doc comment - old code's force-macro-exit check ran
            // unconditionally before its Pool.Count == 0 branch, so the last cell dying with the player inside
            // it needs the same treatment (OnAllCellsDied fires INSTEAD OF OnCellDied for that death - see
            // CellRegistry's own "OnCellDied vs OnAllCellsDied" doc comment - so _handleCellDiedForceMacroExit
            // above never sees it). No position check needed: CurrentScale can only be Micro here if the player
            // was inside the one cell that just brought the count to zero.
            if (_worldScaleController.CurrentScale == _Project.Mechanics.WorldScale.WorldScale.Micro)
                EnterMacroWorldSafeAsync().Forget();

            if (_gameOverScreen != null)
                _gameOverScreen.SetAllCellsDied(true);

            _screenManager.OpenScreen<GameOverScreen>().Forget();
        }

        /// <summary>See _handleCellDiedForceMacroExit's/HandleAllCellsDied's doc comments. Mirrors
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
        /// Old: the player-death branch of HUD.GameOverGroup(true), triggered here via PlayerHealth.OnDied
        /// (HudScreen's own OnDied subscription opens the GameOverScreen panel itself - see HudScreen's
        /// "Auto-opening" doc comment - this handler only owns the SAVE side of game-over, which no screen has a
        /// reason to know about).
        ///
        /// --- Elapsed-time source: reusing Stage 1's Time.timeSinceLevelLoad substitute, not a third timer ---
        /// Two elapsed-time readers already exist and were both deliberately left as independent, undocumented-as-
        /// canonical values: HudScreen's own MM:SS display counter (a UniTask tick starting from 0 in OnOpen) and
        /// MacroVirusSpawner/MicroVirusSpawner's `Time.timeSinceLevelLoad` reads (their own class doc comments:
        /// "no _Project equivalent shared elapsed-time service exists yet ... uses Time.timeSinceLevelLoad as a
        /// direct substitute"). Rather than add a THIRD independent accumulator here, this handler reads
        /// `Time.timeSinceLevelLoad` directly too - it is already the de-facto shared elapsed-time source for
        /// every other system in this phase that needs one, and is accurate here for the same reason Stage 1's
        /// doc comment gives: this composition root starts running at the same moment gameplay begins. A future
        /// phase that promotes elapsed time to a real shared service (as both Stage 1 doc comments suggest) has
        /// exactly one place to repoint in this file.
        /// </summary>
        private void HandleGameOver()
        {
            float elapsedSeconds = Time.timeSinceLevelLoad;

            _gameSaveService.RecordSurvivalTime(elapsedSeconds);
            _gameSaveService.RecordPlayerLevel(_playerStats.Level);

            // Old: no equivalent - GMTK never had a save system. IGameSaveService.SaveImmediate's own doc comment
            // explicitly names "right after game-over" as the case where losing the debounce window to a
            // crash/force-quit would be most noticeable to the player, so this flushes immediately rather than
            // relying on RecordSurvivalTime/RecordPlayerLevel's own debounced RequestSave.
            _gameSaveService.SaveImmediate();
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
