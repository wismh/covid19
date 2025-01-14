using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Shared;
using _Project.Mechanics.Viruses.Micro;
using _Project.Mechanics.Waves;

namespace _Project.Mechanics.Cells
{
    /// <summary>
    /// Phase 7 Stage 1 - replaces GMTK.CellSpawner (Assets/GMTK/Scripts/Behaviors/Cell/CellSpawner.cs). Spawns
    /// the new CellHealth+CellInfectionState+CellVisuals trio (Phase 3) instead of the old monolithic
    /// CellController, and registers each spawned cell with <see cref="CellRegistry"/> instead of the old
    /// ServiceLocator-registered CellManager/VirusCellManager pair.
    ///
    /// --- Placement: SpawnPlacementUtility reused verbatim ---
    /// Old CellSpawner.SpawnCell() had its own copy of the rejection-sampling loop (`MAX_CHOICES_VALUE = 5000`,
    /// `_distanceThreshold`, `_sqrSizeOfStartZone`) - Phase 3 already unified this into
    /// <see cref="_Project.Mechanics.Shared.SpawnPlacementUtility.FindPosition"/>. This class calls that utility
    /// with the same field names/defaults old CellSpawner's GameplayScene.unity instance used (cellCount=20,
    /// minSqrDistanceBetweenCells=35, minSqrDistanceFromOrigin=10, maxPlacementAttempts=5000, spawnBounds
    /// center=(0,0,0) extent=(10,25,0)) rather than re-guessing numbers - read directly from the scene, not
    /// invented - so a future Stage 6 scene-composition step can drop this component in with correct-by-default
    /// values and only needs to reassign the prefab reference.
    ///
    /// --- Prefab content validation ---
    /// SpawnCell() checks the assigned <see cref="_cellPrefab"/> actually carries CellHealth/CellInfectionState
    /// before registering it, logging an error instead of throwing if it doesn't.
    ///
    /// --- Registry seam ---
    /// <see cref="CellRegistry"/> is NOT constructed here - per REFACTORING_PLAN.md Phase 7 Stage 2, a future
    /// MainSceneComposition owns the single shared CellRegistry instance (also needed by MacroVirusSpawner as an
    /// ICellTargetProvider, and by HudScreen/GameOverScreen/CellProximityPrompt wiring) and passes it to this
    /// spawner via <see cref="Construct"/>, the same manual-composition seam Phase 2/3 already established for
    /// MacroVirusConfig/ICellTargetProvider.
    ///
    /// --- GameObject lifecycle on death ---
    /// CellRegistry only does bookkeeping (see its own doc comment) - actual GameObject destruction on death is
    /// this spawner's job, since it's the thing that Instantiate'd the cell. Delayed by
    /// <see cref="_deathDestroyDelaySeconds"/> (default 2s, matching old CellController.Die()'s
    /// `Destroy(gameObject, 2f)`) via UniTask per REFACTORING_PLAN.md section 4/5's coroutine->UniTask rule,
    /// rather than a bare `Destroy(gameObject, delay)` call, so a scene teardown mid-delay can still be cancelled
    /// cleanly through <see cref="_lifetimeCts"/>.
    /// </summary>
    public sealed class CellSpawner : MonoBehaviour
    {
        [Header("Prefab - assigned by a later stage (prefab/scene surgery), see class doc comment")]
        [SerializeField] private Transform _cellPrefab;

        // Placement/lifecycle tunables (cell count, spacing, spawn bounds, death-destroy delay) now live on
        // CellConfig instead of local [SerializeField]s - see CellConfig's own "Spawning & lifecycle" header.
        private CellConfig _cellConfig;
        private CellRegistry _registry;
        private IPlayerTargetProvider _playerTargetProvider;
        private WaveScalingConfig _waveScalingConfig;
        private IReadOnlyDictionary<MicroVirusVariant, IFactory<MicroVirusController>> _microVirusFactories;

        private readonly List<Vector3> _spawnedPositions = new();
        private CancellationTokenSource _lifetimeCts;

        /// <summary>
        /// Phase 8 testing fix (2026-09-11): lets MainSceneComposition reach each cell's own MicroVirusSpawner
        /// the moment it's constructed, without this class needing to know why (today: granting XP on in-cell
        /// virus kills - see MicroVirusSpawner.OnVirusKilled's doc comment. Mirrors MacroVirusRegistry.
        /// OnRegistered's role for macro viruses, adapted for the fact that in-cell viruses have no shared
        /// registry to subscribe to - CellSpawner is the one place that constructs every cell's spawner.
        /// </summary>
        public event Action<MicroVirusSpawner> OnMicroVirusSpawnerCreated;

        /// <summary>
        /// Phase 7 testing fix (2026-09-11): added <paramref name="playerTargetProvider"/>/
        /// <paramref name="waveScalingConfig"/> to construct each spawned cell's MicroVirusSpawner - Cell.prefab
        /// never had that component at all before this fix, and even once added, nothing constructed one per
        /// spawned cell instance, so a cell's interior was permanently empty. Construct() only - see SpawnCell's
        /// own comment for why SetVirusesCount is deliberately NOT called/wired from here.
        /// </summary>
        [Inject]
        public void Construct(CellConfig cellConfig, CellRegistry registry, IPlayerTargetProvider playerTargetProvider, WaveScalingConfig waveScalingConfig, IReadOnlyDictionary<MicroVirusVariant, IFactory<MicroVirusController>> microVirusFactories)
        {
            _cellConfig = cellConfig;
            _registry = registry;
            _playerTargetProvider = playerTargetProvider;
            _waveScalingConfig = waveScalingConfig;
            _microVirusFactories = microVirusFactories;
        }

        private void Awake()
        {
            _lifetimeCts = new CancellationTokenSource();
        }

        private void Start()
        {
            // Old: CellSpawner.Awake() -> `for (int i = 0; i &lt; _cellCount; ++i) SpawnCell();`. Deferred to
            // Start() (rather than Awake, matching old code) so Construct() - called manually by a future
            // composition root, not guaranteed to run before Unity's own Awake - has definitely happened first.
            for (int i = 0; i < _cellConfig.CellCount; i++)
                SpawnCell();
        }

        private void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        }

        /// <summary>Old: CellSpawner.SpawnCell().</summary>
        public void SpawnCell()
        {
            Vector3 position = SpawnPlacementUtility.FindPosition(
                _cellConfig.SpawnBounds,
                _spawnedPositions,
                _cellConfig.MinSqrDistanceBetweenCells,
                _cellConfig.MaxPlacementAttempts,
                _cellConfig.MinSqrDistanceFromOrigin);

            _spawnedPositions.Add(position);

            Transform instance = Instantiate(_cellPrefab, transform);
            instance.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0, 360)));

            if (!instance.TryGetComponent(out CellHealth health) || !instance.TryGetComponent(out CellInfectionState infectionState))
            {
                Debug.LogError($"{nameof(CellSpawner)}: assigned prefab '{_cellPrefab.name}' is missing CellHealth/CellInfectionState - cannot register with {nameof(CellRegistry)}.", instance);
                return;
            }

            // Manual construction, matching the composition-root seam MacroVirusController/CellHealth already
            // expect ([Inject] Construct methods callable directly without a Zenject container binding - see
            // PlayerHealth.Construct's doc comment for the precedent).
            health.Construct(_cellConfig);
            if (instance.TryGetComponent(out CellVisuals visuals))
                visuals.Construct(_cellConfig);

            _registry.Register(health, infectionState);

            health.OnDied += _ => DestroyCellAfterDelayAsync(instance).Forget();

            // Phase 7 testing fix (2026-09-11) - see Construct's doc comment. _cellInfectionState/_virusContainer
            // are wired as same-prefab sibling references directly on Cell.prefab (matching CellHealth's own
            // sibling-reference idiom - no cross-scene dependency, safe to bake into the prefab asset). Only
            // Construct() happens here - NOT a CellInfectionState.OnVirusCountChanged subscription (an earlier,
            // user-rejected draft of this fix did wire that reactively; see MicroVirusSpawner's own
            // "SetVirusesCount: LAZY, on micro-world entry only" doc comment for why that produced full-size
            // in-cell viruses wandering the macro world). SetVirusesCount is instead called lazily, once, by
            // WorldScaleTransition.EnterMicroWorldAsync at the moment the player actually enters this cell -
            // MainSceneComposition's proximity-polling loop resolves this same MicroVirusSpawner instance onto
            // WorldScaleTransitionContext the same way it already does CellVisuals/CellRigidbody2D/CellAnimator.
            if (instance.TryGetComponent(out MicroVirusSpawner inCellVirusSpawner))
            {
                inCellVirusSpawner.Construct(_cellConfig, _playerTargetProvider, _waveScalingConfig, _microVirusFactories);
                OnMicroVirusSpawnerCreated?.Invoke(inCellVirusSpawner);
            }
        }

        private async UniTaskVoid DestroyCellAfterDelayAsync(Transform instance)
        {
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(_cellConfig.DeathDestroyDelaySeconds), cancellationToken: _lifetimeCts.Token);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            if (instance != null)
                Destroy(instance.gameObject);
        }
    }
}
