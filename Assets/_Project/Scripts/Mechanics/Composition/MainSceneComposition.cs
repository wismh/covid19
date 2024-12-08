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
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Macro;
using _Project.Mechanics.Waves;
using _Project.Mechanics.WorldScale;
using _Project.Save;
using _Project.UI.GameOver;
using _Project.UI.Hud;
using _Project.UI.Upgrades;

namespace _Project.Mechanics.Composition
{
    /// <summary>
    /// Phase 7 Stage 2 (REFACTORING_PLAN.md "Композиція та реальна інтеграція", п.2). The hand-composed
    /// "arena builder" for the gameplay scene - directly mirrors `td-over-unity`'s `MainSceneView.BuildArena()`
    /// (REFACTORING_PLAN.md section 4: "MainSceneComposition/GameplayComposition клас, що вручну створює
    /// PlayerStats, WaveSpawner, CellManager, VirusManager й з'єднує їхні C#-події"). Every Mechanics/* type this
    /// class touches (PlayerStatsService, CellRegistry, MacroVirusRegistry, WorldScaleController, GameAudioService)
    /// is a plain C# POCO deliberately kept OUTSIDE Zenject per section 4's stated policy - this MonoBehaviour is
    /// the one place that `new`s them and wires their events together, exactly the seam every prior phase's doc
    /// comments (PlayerHealth.Construct, CellSpawner's "Registry seam", MacroVirusSpawner's "Cross-system wiring
    /// left to Stage 2", HudScreen's "Composition-root note", CellProximityPrompt's two documented seams) already
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
    /// --- Stage 6 will place this in the scene; Stage 2 only builds it (pure C#, per the phase brief) ---
    /// Every reference below is a `[SerializeField]` a human assigns once this component sits on a GameObject in
    /// SampleScene (Stage 6) - none are assigned here, none are `[Inject]`-resolved except the three genuinely
    /// Zenject-bound platform services (verified against ProjectContextInstaller.cs - see the `Construct`
    /// method's own doc comment, including its "SceneContext correction" section covering `IScreenManager`,
    /// which is NOT one of the three). No prefab/.scene file is touched by this stage.
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
        [SerializeField] private PlayerConfig _playerConfig;
        [SerializeField] private List<UpgradeDefinition> _upgradeDefinitions;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerTurret _playerTurret;
        [SerializeField] private PlayerHealth _playerHealth;

        [Header("Cells (Phase 3 components + Phase 7 Stage 1 registry/spawner)")]
        [SerializeField] private CellConfig _cellConfig;
        [SerializeField] private CellSpawner _cellSpawner;

        [Header("Macro viruses (Phase 3 components + Phase 7 Stage 1 registry/spawner)")]
        [SerializeField] private MacroVirusConfig _macroVirusConfig;
        [SerializeField] private MacroVirusSpawner _macroVirusSpawner;

        [Header("Waves (Phase 3) - shared by MacroVirusSpawner today; InCellVirusSpawner will need it too once " +
                "Stage 3 attaches one per cell, see class doc comment's per-cell virus seam")]
        [SerializeField] private WaveScalingConfig _waveScalingConfig;

        [Header("World scale (Phase 4)")]
        [SerializeField] private WorldScaleConfig _worldScaleConfig;
        [SerializeField] private Camera _worldCamera;
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

        // --- Zenject-bound platform services (verified against ProjectContextInstaller.cs) ---
        private IAudioSystem _audioSystem;
        private IGameSaveService _gameSaveService;
        private GlobalGameStateMachine _stateMachine;

        // --- NOT Zenject-bound, despite UiInstaller.cs existing to bind it - see Construct's doc comment's
        // "SceneContext correction" section. Built by hand in Awake() instead. ---
        private IScreenManager _screenManager;

        // Resolved off _playerCollider by ResolvePlayerComponentReferences(), same as PlayerMovement/PlayerTurret/
        // PlayerHealth - see that method's doc comment. Wired into _playerTurret and, once BulletManager is
        // resolvable (Start(), see its own doc comment), into itself.
        private GmtkBulletManagerProjectileSpawner _projectileSpawner;

        // --- Hand-composed Mechanics/* POCOs, built in Awake() ---
        private PlayerStatsService _playerStats;
        private CellRegistry _cellRegistry;
        private MacroVirusRegistry _macroVirusRegistry;
        private WorldScaleController _worldScaleController;
        private WorldScaleTransitionContext _worldScaleTransitionContext;
        private GameAudioService _gameAudioService;

        // Cached delegates so lambda subscriptions can be symmetrically unsubscribed in OnDestroy (matching this
        // codebase's PlayerHealth/CellHealth/CellVisuals +=/-= discipline instead of relying on GC to clean up).
        private Action<Vector3, int> _handleCellDied;
        private Action _handlePauseMacroSpawning;
        private Action _handleResumeMacroSpawning;

        private CancellationTokenSource _lifetimeCts;

        /// <summary>
        /// The three types actually bound in Zenject's container this class needs, confirmed by reading
        /// ProjectContextInstaller.cs directly rather than assuming: `IAudioSystem` (AudioSystemInstaller),
        /// `IGameSaveService` (GameSaveModuleInstaller -&gt; GameSaveService), `GlobalGameStateMachine` (bound
        /// directly in ProjectContextInstaller, needed here only to hand to GameOverScreen.Construct - see that
        /// call below). Every OTHER type this class touches (PlayerConfig, CellConfig, MacroVirusConfig,
        /// WorldScaleConfig, WaveScalingConfig, GameAudioCatalog, UpgradeDefinition) is a ScriptableObject asset
        /// assigned via `[SerializeField]` instead, per REFACTORING_PLAN.md section 4 - Mechanics/* configs are
        /// not Zenject bindings in this project (unlike some `td-over-unity` setups that DO bind config SOs).
        ///
        /// --- SceneContext correction (found during Stage 6, applies retroactively to this Stage 2 class) ---
        /// This method is a plain `[Inject]`-tagged method, which Zenject only calls automatically for objects
        /// that a `SceneContext` (or another container) actually injects. Checked both local reference projects
        /// this migration cites (`C:\Projects\Lumenwake\template` and `C:\Projects\Lumenwake\td-over-unity`) -
        /// grepping every `.unity` scene file in both for "SceneContext" returns zero matches in either project.
        /// Neither uses one; `td-over-unity`'s own `MainSceneView.BuildArena()` composition root documents why in
        /// its own source comment - no SceneContext exists, so gameplay/UI wiring there is hand-composed instead
        /// of Zenject-injected, exactly the same shape this class already takes for every Mechanics/* POCO. This
        /// project follows the same precedent: no SceneContext is built for Phase 7, and `Awake()` below
        /// self-injects via `ProjectContext.Instance.Container.Inject(this)` (a documented, safe-to-call-anytime
        /// static accessor - `ProjectContext.Instance` lazily bootstraps itself on first access if not already
        /// running, per `Assets/Plugins/Zenject/Source/Install/Contexts/ProjectContext.cs`) instead of relying on
        /// a SceneContext to call this method automatically. `IScreenManager` was dropped from this method's
        /// parameters entirely because nothing binds it without `UiInstaller.InstallBindings()` actually running,
        /// and nothing runs a scene-placed `MonoInstaller` without a SceneContext either - see
        /// `BuildScreenManager()`'s own doc comment for how `_screenManager` is built instead.
        /// `Assets/_Project/Scripts/UI/UiInstaller.cs` (Phase 1) is consequently unused/dead code under this
        /// architecture - left in place, not deleted, per this migration's "disable/leave, don't delete yet" rule.
        /// </summary>
        [Inject]
        public void Construct(IAudioSystem audioSystem, IGameSaveService gameSaveService, GlobalGameStateMachine stateMachine)
        {
            _audioSystem = audioSystem;
            _gameSaveService = gameSaveService;
            _stateMachine = stateMachine;
        }

        private void Awake()
        {
            // See Construct's doc comment "SceneContext correction" section - no SceneContext exists in this
            // project (matching both local reference projects), so nothing calls [Inject] methods on scene
            // objects automatically. Self-inject via the lazily-bootstrapping ProjectContext singleton instead.
            Zenject.ProjectContext.Instance.Container.Inject(this);

            _lifetimeCts = new CancellationTokenSource();

            ResolvePlayerComponentReferences();

            BuildScreenManager();
            BuildMechanicsPocos();
            ConstructSpawners();
            ConstructScreens();
            WireCrossSystemEvents();

            // NOT calling _gameAudioService.PlayGameplayMusic() here (tried it, reverted): the old
            // GMTK.AudioMixerController/AudioManagerFactory (Assets/GMTK/Scripts/{AudioMixerController.cs,
            // AudioManagerFactory.cs}) are still fully enabled and already drive menu/gameplay music crossfade on
            // their own DontDestroyOnLoad singleton - nothing in this migration has disabled them yet (they're
            // old-UI/audio-layer scripts, deliberately left running per Phase 5's "keep the old game playable"
            // rule, same as HUD.cs/AdviceDialog.cs). Calling PlayGameplayMusic() here made both systems play music
            // simultaneously (reported directly by the user after Stage 6 - "дві музики"). This was always a
            // judgement-call addition, not a verified port of an old call site (see the removed comment this one
            // replaces) - safe to drop entirely until a later phase actually retires GMTK.AudioMixerController.

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
        /// SampleScene.unity (deliberately kept running per Phase 5's "keep the old game playable" rule), each
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
        /// </summary>
        private void Start()
        {
            if (GMTK.Services.ServiceLocator.Instance.TryGet(out GMTK.CameraFollow cameraFollow) && _playerMovement != null)
                cameraFollow.Target = _playerMovement.transform;

            if (_projectileSpawner != null && GMTK.Services.ServiceLocator.Instance.TryGet(out GMTK.BulletManager bulletManager))
                _projectileSpawner.SetBulletManager(bulletManager);

            if (GMTK.Services.ServiceLocator.Instance.TryGet(out GMTK.Services.PlayerStats legacyPlayerStats) && _playerMovement != null)
                legacyPlayerStats.Player = _playerMovement.transform;
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
            }

            if (_worldScaleController != null)
            {
                _worldScaleController.OnEnteredMicroWorld -= _handlePauseMacroSpawning;
                _worldScaleController.OnExitedMicroWorld -= _handleResumeMacroSpawning;
            }

            if (_playerHealth != null)
                _playerHealth.OnDied -= HandleGameOver;

            _gameAudioService?.Dispose();
        }

        // --- 0. Screen manager - hand-built, NOT resolved from Zenject ---
        //
        // Old (Stage 2, before the Stage 6 SceneContext correction): `[Inject]`-resolved `IScreenManager`,
        // assuming `UiInstaller.InstallBindings()` (Assets/_Project/Scripts/UI/UiInstaller.cs, Phase 1) had run
        // somewhere. It never does - nothing in this project loads a Zenject `SceneContext`, and a scene-placed
        // `MonoInstaller` only runs if a `SceneContext` is configured to run it (see `Construct`'s doc comment).
        // `BaseScreenManager`'s constructor (`Assets/_Lumenwake/Scripts/Runtime/UIModule/BaseScreenManager.cs`)
        // is public and takes a plain `List<BaseScreen>` - exactly the shape `UiInstaller` would have passed it,
        // so this method reproduces that same construction by hand instead, mirroring how every other
        // Mechanics/* dependency in this class is already hand-composed rather than Zenject-resolved.

        /// <summary>
        /// Workaround for a reproducible Unity engine quirk, confirmed via Unity CLI Play Mode testing
        /// (2026-09-11, both directly and through the real Menu -&gt; SampleScene flow): when this scene loads
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
            if (_projectileSpawner == null)
                _projectileSpawner = _playerCollider.GetComponent<GmtkBulletManagerProjectileSpawner>();
        }

        private void BuildScreenManager()
        {
            var screens = new List<BaseScreen>();

            if (_hudScreen != null) screens.Add(_hudScreen);
            if (_upgradeScreen != null) screens.Add(_upgradeScreen);
            if (_gameOverScreen != null) screens.Add(_gameOverScreen);

            _screenManager = new BaseScreenManager(screens);
        }

        // --- 1. Hand-composed Mechanics/* POCOs (REFACTORING_PLAN.md section 4's manual-composition rule) ---

        private void BuildMechanicsPocos()
        {
            _playerStats = new PlayerStatsService(_playerConfig, _upgradeDefinitions);

            _cellRegistry = new CellRegistry();
            _macroVirusRegistry = new MacroVirusRegistry();

            IWorldScaleTransition worldScaleTransition = new WorldScaleTransition(_worldScaleConfig);
            _worldScaleController = new WorldScaleController(worldScaleTransition);

            _gameAudioService = new GameAudioService(_audioSystem, _gameAudioCatalog, _worldScaleController);

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

                // --- Deliberately left null: SetCameraTarget / SetPlayerSpeedScale ---
                // Old GMTK.CameraFollow has no _Project port yet (WorldScaleTransitionContext.SetCameraTarget's own
                // doc comment) and PlayerMovement has no speed-scale multiplier hook yet (WorldScaleController's
                // class doc comment, "seam item 4" - a genuine gap Phase 4 documented and this phase is not
                // permitted to fix by editing PlayerMovement). Both delegates are invoked only if non-null by
                // WorldScaleTransition, so leaving them null just means those two steps are silently skipped
                // today, exactly as both source phases already flagged.

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

        private void ConstructSpawners()
        {
            if (_cellSpawner != null)
                _cellSpawner.Construct(_cellConfig, _cellRegistry);

            if (_macroVirusSpawner != null)
                _macroVirusSpawner.Construct(_macroVirusConfig, _cellRegistry, _macroVirusRegistry, _waveScalingConfig);

            if (_playerMovement != null)
                _playerMovement.Construct(_playerConfig, _playerStats);

            if (_playerTurret != null)
            {
                _playerTurret.Construct(_playerConfig, _playerStats);

                // See PlayerTurret.SetProjectileSpawner's doc comment - Player.prefab had no
                // GmtkBulletManagerProjectileSpawner at all before this testing round, so shooting was a silent
                // no-op. _projectileSpawner itself is resolved in ResolvePlayerComponentReferences(); its own
                // BulletManager dependency is wired later, in Start() (see that method's doc comment).
                _playerTurret.SetProjectileSpawner(_projectileSpawner);
            }

            if (_playerHealth != null)
                _playerHealth.Construct(_playerConfig, _playerStats);
        }

        // --- 3. UI screens ---
        // Called manually for every screen below (not left to Zenject's own FromInstance injection in UiInstaller)
        // even where a screen's Construct signature happens to need ONLY a real Zenject binding (GameOverScreen
        // needs just GlobalGameStateMachine) - this removes any dependency on the unverified-without-an-Editor
        // question of whether Zenject actually re-injects `[Inject] Construct` on MonoBehaviours bound via
        // `Container.Bind(type).FromInstance(instance)` (UiInstaller.cs) before or after this composition root
        // runs. Calling Construct here directly is idempotent either way and removes the ambiguity outright.

        private void ConstructScreens()
        {
            if (_hudScreen != null)
                _hudScreen.Construct(_playerConfig, _playerHealth, _playerStats, _screenManager);

            if (_upgradeScreen != null)
                _upgradeScreen.Construct(_playerStats, _screenManager);

            if (_gameOverScreen != null)
                _gameOverScreen.Construct(_stateMachine);

            if (_cellProximityPrompt != null)
            {
                _cellProximityPrompt.Construct(_worldScaleController);
                _cellProximityPrompt.SetContext(_worldScaleTransitionContext);

                // See CellProximityPrompt.SetPlayer's doc comment - nothing wired _player in SampleScene.unity,
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

            // --- NOT wired here: CellInfectionState.OnVirusCountChanged -> InCellVirusSpawner.SetVirusesCount ---
            // Deliberately absent, not an oversight - see class doc comment's "per-cell in-cell-virus wiring"
            // section below for the full reasoning (CellSpawner, read directly for this stage, does not spawn or
            // attach an InCellVirusSpawner to any cell it creates - there is nothing of that shape in the scene
            // yet for this composition root to reach into).
        }

        /// <summary>
        /// Old: CellController.Die()'s `if (_cellManager.Pool.Count == 0)` branch, reproduced via
        /// CellRegistry.OnAllCellsDied (see CellRegistry's own "OnCellDied vs OnAllCellsDied" doc comment).
        /// </summary>
        private void HandleAllCellsDied()
        {
            if (_gameOverScreen != null)
                _gameOverScreen.SetAllCellsDied(true);

            _screenManager.OpenScreen<GameOverScreen>().Forget();
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
        /// MacroVirusSpawner/InCellVirusSpawner's `Time.timeSinceLevelLoad` reads (their own class doc comments:
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
        /// InCellVirusSpawner has no such loop but MacroVirusSpawner.Update() polls every frame instead - this
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
            }
        }

        // --- Documented gap: per-cell in-cell-virus wiring (CellInfectionState.OnVirusCountChanged ->
        // InCellVirusSpawner.SetVirusesCount) is NOT done by this class ---
        //
        // Read CellSpawner.cs directly for this stage (not just Stage 1's summary) to answer this precisely:
        // CellSpawner.SpawnCell() instantiates `_cellPrefab`, resolves CellHealth/CellInfectionState/CellVisuals
        // off the SAME instance via TryGetComponent, and registers those with CellRegistry - it does not
        // instantiate, look up, or reference any InCellVirusSpawner at all. InCellVirusSpawner's own class doc
        // comment confirms this is intentional and not yet done: "Stage 3's prefab surgery is expected to add it
        // alongside CellHealth/CellInfectionState/CellVisuals on Cell.prefab (or a child of it, matching the old
        // hierarchy), not as a scene-level singleton like CellSpawner/MacroVirusSpawner." So today, no spawned
        // cell has an InCellVirusSpawner sibling for this composition root (or CellSpawner) to reach into - there
        // is nothing of that shape anywhere in the scene yet, pure-C#-Stage-2 or otherwise.
        //
        // Once Stage 3 adds InCellVirusSpawner (+ its own IPlayerTargetProvider dependency, which also has no
        // implementation anywhere in this codebase yet - only the interface exists, see
        // _Project.Mechanics.Viruses.InCell.IPlayerTargetProvider) to the cell prefab, the natural place for
        // `cellInfectionState.OnVirusCountChanged += inCellVirusSpawner.SetVirusesCount;` is CellSpawner.SpawnCell()
        // itself - it already resolves CellInfectionState off the freshly-instantiated cell instance in one place
        // via TryGetComponent, and adding one more `instance.TryGetComponent(out InCellVirusSpawner ...)` +
        // Construct + subscribe right there mirrors exactly how InCellVirusSpawner's own OnKilled ->
        // CellInfectionState.RemoveVirus wiring already works (a sibling-instance concern, not a composition-root
        // concern - see InCellVirusSpawner's "CellInfectionState wiring: sibling reference, not Construct-
        // injection" doc comment for the identical reasoning). This composition root building an
        // IPlayerTargetProvider adapter today, with nothing yet in the scene that would consume it, would be
        // exactly the kind of ungrounded workaround the phase brief asks not to invent - left as this stage's
        // honest, documented gap instead.
    }
}
