using UnityEngine;
using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Shared;
using _Project.Mechanics.Waves;

namespace _Project.Mechanics.Viruses.InCell
{
    /// <summary>
    /// Phase 7 Stage 1 - replaces GMTK.MicroViruses.InCellViruses (Assets/GMTK/Scripts/Behaviors/MicroVirus/
    /// InCellViruses.cs). Spawns the new InCellVirusController (Phase 3) instead of the old InCellVirus, and
    /// registers/unregisters with a private <see cref="InCellVirusRegistry"/> instead of the old
    /// InCellVirusManager - see that class's doc comment for why this one is privately owned per-instance rather
    /// than a shared singleton like CellRegistry/MacroVirusRegistry.
    ///
    /// --- Per-cell placement: intended as a sibling component on each Cell prefab instance ---
    /// Old GMTK.MicroViruses.InCellViruses lived as a child GameObject under Cell.prefab, driven by
    /// CellController.SetMicroSprite()'s `_inCellVirusManager.SetVirusesCount(_virusCount)` call. This class is
    /// the same shape - one instance per cell, not a single global spawner - so Stage 3's prefab surgery is
    /// expected to add it alongside CellHealth/CellInfectionState/CellVisuals on Cell.prefab (or a child of it,
    /// matching the old hierarchy), not as a scene-level singleton like CellSpawner/MacroVirusSpawner.
    ///
    /// --- CellInfectionState wiring: sibling reference, not Construct-injection ---
    /// <see cref="_cellInfectionState"/> is a direct [SerializeField] (the same sibling-reference idiom
    /// CellHealth/CellVisuals already use for each other - see CellHealth's doc comment) rather than a
    /// Construct(...) parameter, because it is a same-prefab-local dependency (this cell's own
    /// CellInfectionState), not a cross-system service a composition root supplies. It closes the loop
    /// CellInfectionState's own doc comment names: "a future composition root wires
    /// InCellVirusController.OnKilled to this method [RemoveVirus] the same way once a per-cell virus registry
    /// exists" - this spawner does that wiring itself (see <see cref="SpawnVirus"/>), since it already owns both
    /// halves (the spawned virus's OnKilled event AND the sibling CellInfectionState reference) at spawn time.
    ///
    /// --- SetVirusesCount: expected reactive wiring, per CellVisuals's own doc comment ---
    /// Old CellController.SetMicroSprite() called `_inCellVirusManager.SetVirusesCount(_virusCount)` ONCE,
    /// lazily, only at the moment the player actually entered micro-world for that cell. CellVisuals.cs's own doc
    /// comment (Phase 3) already signals the intended new wiring is different: "left for a future composition
    /// root to wire via CellInfectionState.OnVirusCountChanged" - i.e. reactively, on every add/remove, not just
    /// on micro-world entry. <see cref="SetVirusesCount"/> is written to be safe either way (see its own doc
    /// comment for why the OnKilled -&gt; RemoveVirus -&gt; OnVirusCountChanged -&gt; SetVirusesCount chain
    /// can't loop), but which cadence to actually wire is Stage 2/3's call, not decided here.
    ///
    /// --- Elapsed-time source ---
    /// Same substitution as MacroVirusSpawner (`Time.timeSinceLevelLoad` standing in for old
    /// `_playerStats.Time`, since no shared elapsed-time service exists yet) - see that class's doc comment for
    /// the full reasoning.
    /// </summary>
    public sealed class InCellVirusSpawner : MonoBehaviour
    {
        [Header("Sibling reference (this cell's own CellInfectionState - see class doc comment)")]
        [SerializeField] private CellInfectionState _cellInfectionState;

        [Header("Prefabs - assigned by a later stage (prefab/scene surgery), see class doc comment")]
        [SerializeField] private InCellVirusController _orangePrefab;
        [SerializeField] private InCellVirusController _cucumberPrefab;
        [SerializeField] private InCellVirusController _spinerPrefab;

        // Old: InCellViruses._bounds (values from Cell.prefab's InCellViruses component: center (0,0,0), extent
        // (2,2,0)) - local-space area viruses are randomly positioned within on spawn/reposition.
        [SerializeField] private Bounds _bounds = new Bounds(Vector3.zero, new Vector3(4f, 4f, 0f));

        private readonly InCellVirusRegistry _registry = new();

        private IPlayerTargetProvider _playerTargetProvider;
        private WaveScalingConfig _waveScalingConfig;

        /// <summary>See MacroVirusSpawner's doc comment's "Elapsed-time source" section - identical reasoning.</summary>
        private float ElapsedSeconds => Time.timeSinceLevelLoad;

        [Inject]
        public void Construct(IPlayerTargetProvider playerTargetProvider, WaveScalingConfig waveScalingConfig)
        {
            _playerTargetProvider = playerTargetProvider;
            _waveScalingConfig = waveScalingConfig;
        }

        /// <summary>Old: InCellViruses.SetVirusesCount(int) - see class doc comment's "SetVirusesCount" section.</summary>
        public void SetVirusesCount(int count)
        {
            for (int i = _registry.Count; i < count; i++)
                SpawnVirus();

            foreach (InCellVirusController virus in _registry.Viruses)
                SetRandomPosition(virus.transform);
        }

        /// <summary>Old: InCellViruses.SpawnVirus() - variant selection ported from InCellViruses.SpawnVirus()'s
        /// prefab-list weighting (`_playerStats.Time &gt;= 50` / `&gt;= 100`, now
        /// WaveScalingConfig.CucumberUnlockTimeSeconds/SpinerUnlockTimeSeconds).</summary>
        private void SpawnVirus()
        {
            if (_orangePrefab == null)
            {
                Debug.LogWarning($"{nameof(InCellVirusSpawner)}.{nameof(SpawnVirus)} called before prefabs were assigned - no-op. See class doc comment's prefab seam.", this);
                return;
            }

            InCellVirusController prefab = ChoosePrefab();

            InCellVirusController virus = Instantiate(prefab, transform);
            virus.Construct(_playerTargetProvider);

            // Old: InCellViruses.SpawnVirus() -> `Instantiate(prefabs[...], transform);` (no explicit position -
            // SetVirusesCount's SetRandomPosition pass, run right after this loop, places every registered virus
            // including the one just spawned).

            _registry.Register(virus);

            virus.OnKilled += () => HandleVirusKilled(virus);
        }

        private InCellVirusController ChoosePrefab()
        {
            // Old: `prefabs = [_orangePrefab]; if (Time >= 50) prefabs.Add(_cucumberPrefab); if (Time >= 100)
            // prefabs.Add(_spinerPrefab); Instantiate(prefabs[Random.Range(0, prefabs.Count)]);`
            int available = 1;
            bool cucumberUnlocked = _cucumberPrefab != null && _waveScalingConfig != null && ElapsedSeconds >= _waveScalingConfig.CucumberUnlockTimeSeconds;
            bool spinerUnlocked = _spinerPrefab != null && _waveScalingConfig != null && ElapsedSeconds >= _waveScalingConfig.SpinerUnlockTimeSeconds;

            if (cucumberUnlocked)
                available++;
            if (spinerUnlocked)
                available++;

            int choice = Random.Range(0, available);
            if (choice == 0)
                return _orangePrefab;
            if (choice == 1)
                return cucumberUnlocked ? _cucumberPrefab : _spinerPrefab;
            return _spinerPrefab;
        }

        private void SetRandomPosition(Transform virusTransform)
        {
            // Old: InCellViruses.SetRandomPosition(Transform) - plain Random.Range within _bounds, no
            // rejection-sampling against existing positions (unlike CellSpawner). Routed through
            // SpawnPlacementUtility anyway (existingPositions: null, minSqrDistanceFromExisting: 0,
            // maxAttempts: 1) so there's one placement code path in the codebase instead of a second hand-rolled
            // Random.Range pair - with these arguments it always accepts the first candidate, reproducing the old
            // unconditional placement exactly.
            virusTransform.localPosition = SpawnPlacementUtility.FindPosition(_bounds, null, 0f, 1);
        }

        private void HandleVirusKilled(InCellVirusController virus)
        {
            _registry.Unregister(virus);

            // Old: InCellVirus.Damage()'s death branch -> `_virusManager.RemoveObject(transform);` triggered
            // GMTK.CellController.OnDestroyVirus() via `_inCellVirusManager.OnObjectRemoved += OnDestroyVirus;`
            // (wired in CellController.Start()). This spawner reproduces that direct call itself since it already
            // holds the sibling CellInfectionState reference - see class doc comment.
            if (_cellInfectionState != null)
                _cellInfectionState.RemoveVirus();

            if (virus != null)
                Destroy(virus.gameObject);
        }
    }
}
