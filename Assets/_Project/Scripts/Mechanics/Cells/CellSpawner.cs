using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Shared;
using _Project.Mechanics.Viruses.InCell;
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
    /// with the same field names/defaults old CellSpawner's SampleScene.unity instance used (cellCount=20,
    /// minSqrDistanceBetweenCells=35, minSqrDistanceFromOrigin=10, maxPlacementAttempts=5000, spawnBounds
    /// center=(0,0,0) extent=(10,25,0)) rather than re-guessing numbers - read directly from the scene, not
    /// invented - so a future Stage 6 scene-composition step can drop this component in with correct-by-default
    /// values and only needs to reassign the prefab reference.
    ///
    /// --- Prefab seam (this phase's core constraint - no .prefab/.scene edits) ---
    /// <see cref="_cellPrefab"/> is a plain [SerializeField] left unassigned by this phase, exactly like old
    /// CellSpawner's own `_cellPrefab` field - a human (or Stage 3's prefab surgery, which adds CellHealth/
    /// CellInfectionState/CellVisuals to Cell.prefab) assigns it once a prefab carrying those three new components
    /// exists. Until then this component compiles and is fully testable in isolation (e.g. via a fake prefab in a
    /// PlayMode test) but does nothing useful in a real scene. SpawnCell() defensively no-ops (logs a warning
    /// once) if the assigned prefab is missing the expected components, rather than throwing - a spawner
    /// half-wired during Stage 3's incremental prefab surgery shouldn't hard-crash the rest of the scene.
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

        [Header("Placement (ported from GMTK.CellSpawner, values from SampleScene.unity's CellSpawner instance)")]
        [SerializeField] private int _cellCount = 20;
        [SerializeField] private float _minSqrDistanceBetweenCells = 35f;
        [SerializeField] private float _minSqrDistanceFromOrigin = 10f;
        [SerializeField] private int _maxPlacementAttempts = 5000;
        [SerializeField] private Bounds _spawnBounds = new Bounds(Vector3.zero, new Vector3(20f, 50f, 0f));

        [Header("GameObject lifecycle")]
        // Old: CellController.Die() -> `Destroy(gameObject, 2f);`
        [SerializeField] private float _deathDestroyDelaySeconds = 2f;

        private CellConfig _cellConfig;
        private CellRegistry _registry;
        private IPlayerTargetProvider _playerTargetProvider;
        private WaveScalingConfig _waveScalingConfig;

        private readonly List<Vector3> _spawnedPositions = new();
        private CancellationTokenSource _lifetimeCts;

        /// <summary>
        /// Phase 7 testing fix (2026-09-11): added <paramref name="playerTargetProvider"/>/
        /// <paramref name="waveScalingConfig"/> to wire InCellVirusSpawner - this exact spot was already named as
        /// the intended seam by this class's own OLD doc comment (see the "per-cell in-cell-virus wiring" note
        /// further down, now updated) and by MainSceneComposition's "documented gap" trailing comment: "the
        /// natural place for cellInfectionState.OnVirusCountChanged += inCellVirusSpawner.SetVirusesCount is
        /// CellSpawner.SpawnCell() itself". User-reported symptom this fixes: entering a cell showed no viruses
        /// inside it at all - Cell.prefab never had an InCellVirusSpawner component, and even once added, nothing
        /// constructed or wired one per spawned cell instance.
        /// </summary>
        [Inject]
        public void Construct(CellConfig cellConfig, CellRegistry registry, IPlayerTargetProvider playerTargetProvider, WaveScalingConfig waveScalingConfig)
        {
            _cellConfig = cellConfig;
            _registry = registry;
            _playerTargetProvider = playerTargetProvider;
            _waveScalingConfig = waveScalingConfig;
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
            for (int i = 0; i < _cellCount; i++)
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
            if (_cellPrefab == null || _cellConfig == null || _registry == null)
            {
                Debug.LogWarning($"{nameof(CellSpawner)}.{nameof(SpawnCell)} called before prefab/config/registry were assigned - no-op. See class doc comment's prefab/registry seams.", this);
                return;
            }

            Vector3 position = SpawnPlacementUtility.FindPosition(
                _spawnBounds,
                _spawnedPositions,
                _minSqrDistanceBetweenCells,
                _maxPlacementAttempts,
                _minSqrDistanceFromOrigin);

            _spawnedPositions.Add(position);

            Transform instance = Instantiate(_cellPrefab, transform);
            instance.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, Random.Range(0, 360)));

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

            // Phase 7 testing fix (2026-09-11) - see Construct's doc comment. _cellInfectionState is wired as a
            // same-prefab sibling reference directly on Cell.prefab (matching CellHealth's own sibling-reference
            // idiom for CellInfectionState - no cross-scene dependency, safe to bake into the prefab asset).
            if (instance.TryGetComponent(out InCellVirusSpawner inCellVirusSpawner))
            {
                inCellVirusSpawner.Construct(_playerTargetProvider, _waveScalingConfig);
                infectionState.OnVirusCountChanged += inCellVirusSpawner.SetVirusesCount;
            }
        }

        private async UniTaskVoid DestroyCellAfterDelayAsync(Transform instance)
        {
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(_deathDestroyDelaySeconds), cancellationToken: _lifetimeCts.Token);
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
