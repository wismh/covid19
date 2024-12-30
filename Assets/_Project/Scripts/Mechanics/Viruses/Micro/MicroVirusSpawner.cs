using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Shared;
using _Project.Mechanics.Waves;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>
    /// Phase 7 Stage 1 - replaces GMTK.MicroViruses.InCellViruses (Assets/GMTK/Scripts/Behaviors/MicroVirus/
    /// InCellViruses.cs). Spawns the new MicroVirusController (Phase 3) instead of the old InCellVirus, and
    /// registers/unregisters with a private <see cref="MicroVirusRegistry"/> instead of the old
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
    /// MicroVirusController.OnKilled to this method [RemoveVirus] the same way once a per-cell virus registry
    /// exists" - this spawner does that wiring itself (see <see cref="SpawnVirus"/>), since it already owns both
    /// halves (the spawned virus's OnKilled event AND the sibling CellInfectionState reference) at spawn time.
    ///
    /// --- SetVirusesCount: LAZY, on micro-world entry only - not reactive (corrected 2026-09-11) ---
    /// An earlier draft of this composition wired SetVirusesCount reactively to CellInfectionState.
    /// OnVirusCountChanged (an idea CellVisuals.cs's own doc comment had floated: "left for a future composition
    /// root to wire ... reactively, on every add/remove"). User-tested and rejected: that spawns viruses the
    /// moment a macro virus infects the cell, regardless of whether the player has ever been near it - combined
    /// with spawning outside containment (see _virusContainer's doc comment), the result was full-size in-cell
    /// viruses wandering the MACRO world. Old CellController.SetMicroSprite() only ever called
    /// `_inCellVirusManager.SetVirusesCount(_virusCount)` ONCE, lazily, at the exact moment the player entered
    /// micro-world for that cell - this port now matches that exactly: WorldScaleTransition.EnterMicroWorldAsync
    /// calls this alongside context.CellVisuals?.SetMicroSprite(), the same call site old code used.
    ///
    /// --- Elapsed-time source ---
    /// Same substitution as MacroVirusSpawner (`Time.timeSinceLevelLoad` standing in for old
    /// `_playerStats.Time`, since no shared elapsed-time service exists yet) - see that class's doc comment for
    /// the full reasoning.
    /// </summary>
    public sealed class MicroVirusSpawner : MonoBehaviour
    {
        [Header("Sibling reference (this cell's own CellInfectionState - see class doc comment)")]
        [SerializeField] private CellInfectionState _cellInfectionState;

        /// <summary>
        /// Phase 7 testing fix (2026-09-11): old GMTK.CellController held its InCellViruses manager as a child
        /// of `transform.GetChild(0)` - the SAME GameObject SetMacroSprite()/SetMicroSprite() toggle inactive/
        /// active (CellVisuals._entrailsRoot in this port, identical role - see its own doc comment). Spawning
        /// straight under the cell's own root transform (this component's own transform, since it lives directly
        /// on Cell.prefab) skipped that containment entirely - user-reported symptom: in-cell viruses visible,
        /// wandering freely in the MACRO world instead of hidden until the player is actually inside.
        ///
        /// --- Scale correction (second user report, same day) ---
        /// Wiring straight to _entrailsRoot ("InsideWorld" in Cell.prefab) fixed visibility/timing but not size:
        /// Cell.prefab's ROOT transform is scaled 3x (cells render larger than the player/viruses), and
        /// InsideWorld doesn't compensate (localScale 1,1,1) - anything parented there inherits that 3x directly,
        /// which is exactly the "still huge" the user reported next. Old code already solved this: Cell.prefab
        /// still has the OLD (disabled but never removed) GMTK.MicroViruses.InCellViruses manager, on a
        /// GameObject literally named "Viruses" - a CHILD of InsideWorld with localScale (0.15, 0.15, 1)
        /// specifically to counteract the 3x parent (net 0.45x world scale), and _orangePrefab/_cucumberPrefab/
        /// _spinerPrefab/_bounds fields already matching this class's own values almost exactly. Re-wired to that
        /// exact GameObject instead of InsideWorld directly - reusing old code's own compensating scale rather
        /// than guessing a new one. The old InCellViruses component itself stays harmlessly inert alongside it
        /// (Awake() only registers a ServiceLocator entry + adds a bookkeeping-only ObjectManager component;
        /// SetVirusesCount() is never called since nothing runs old CellController.SetMicroSprite() anymore).
        /// Falls back to this component's own transform if left unwired (keeps SpawnVirus() safe rather than
        /// throwing).
        /// </summary>
        [SerializeField] private Transform _virusContainer;

        // Spawn-area bounds now live on CellConfig.MicroVirusSpawnBounds instead of a local [SerializeField] -
        // see CellConfig's own doc comment.
        private readonly MicroVirusRegistry _registry = new();

        private CellConfig _cellConfig;
        private IPlayerTargetProvider _playerTargetProvider;
        private WaveScalingConfig _waveScalingConfig;

        // Pooling migration: replaces the 3 [SerializeField] prefab fields (Orange/Cucumber/Spiner) - each
        // variant's pool is bound by MicroVirusesInstaller and keyed by MicroVirusVariant (mirrors
        // ParticleFxPoolsInstaller<TId>'s Dictionary<TId, IFactory<...>> shape).
        private IReadOnlyDictionary<MicroVirusVariant, IFactory<MicroVirusController>> _factories;

        /// <summary>
        /// Phase 8 testing fix (2026-09-11): old InCellVirus.Damage()'s death branch called
        /// `_playerStats.AddExperience()` directly, same as old VirusController.Damage() did for macro viruses.
        /// MainSceneComposition already restored that for macro kills (see its "XP-granting was never wired at
        /// all" doc comment) by subscribing to MacroVirusRegistry.OnRegistered - but in-cell viruses have no
        /// equivalent global registry (each cell owns a private MicroVirusRegistry, see this class's own doc
        /// comment's "SetVirusesCount" section), so that same subscription shape doesn't reach them. Raised here
        /// instead, once per killed virus, alongside <see cref="HandleVirusKilled"/>'s existing bookkeeping -
        /// CellSpawner.SpawnCell() forwards this spawner instance out via CellSpawner.OnMicroVirusSpawnerCreated
        /// the moment it's constructed, so MainSceneComposition can subscribe an XP grant to every cell's spawner
        /// without this class needing to know about PlayerStatsService itself (same decoupling MacroVirusController.
        /// OnKilled already uses).
        /// </summary>
        public event Action OnVirusKilled;

        /// <summary>See MacroVirusSpawner's doc comment's "Elapsed-time source" section - identical reasoning.</summary>
        private float ElapsedSeconds => Time.timeSinceLevelLoad;

        [Inject]
        public void Construct(CellConfig cellConfig, IPlayerTargetProvider playerTargetProvider, WaveScalingConfig waveScalingConfig, IReadOnlyDictionary<MicroVirusVariant, IFactory<MicroVirusController>> factories)
        {
            _cellConfig = cellConfig;
            _playerTargetProvider = playerTargetProvider;
            _waveScalingConfig = waveScalingConfig;
            _factories = factories;
        }

        /// <summary>Old: InCellViruses.SetVirusesCount(int) - see class doc comment's "SetVirusesCount" section.</summary>
        public void SetVirusesCount(int count)
        {
            for (int i = _registry.Count; i < count; i++)
                SpawnVirus();

            foreach (MicroVirusController virus in _registry.Viruses)
                SetRandomPosition(virus.transform);
        }

        /// <summary>Old: InCellViruses.SpawnVirus() - variant selection ported from InCellViruses.SpawnVirus()'s
        /// prefab-list weighting (`_playerStats.Time &gt;= 50` / `&gt;= 100`, now
        /// WaveScalingConfig.CucumberUnlockTimeSeconds/SpinerUnlockTimeSeconds).</summary>
        private void SpawnVirus()
        {
            if (_factories == null || _factories.Count == 0)
            {
                Debug.LogWarning($"{nameof(MicroVirusSpawner)}.{nameof(SpawnVirus)} called before pools were bound - no-op. See class doc comment's prefab seam.", this);
                return;
            }

            MicroVirusVariant variant = ChooseVariant();

            Transform container = _virusContainer != null ? _virusContainer : transform;
            MicroVirusController virus = _factories[variant].Create();
            virus.transform.SetParent(container, worldPositionStays: false);

            // Old: InCellViruses.SpawnVirus() -> `Instantiate(prefabs[...], transform);` (no explicit position -
            // SetVirusesCount's SetRandomPosition pass, run right after this loop, places every registered virus
            // including the one just spawned).

            _registry.Register(virus);

            virus.OnKilled += () => HandleVirusKilled(virus);
        }

        private MicroVirusVariant ChooseVariant()
        {
            // Old: `prefabs = [_orangePrefab]; if (Time >= 50) prefabs.Add(_cucumberPrefab); if (Time >= 100)
            // prefabs.Add(_spinerPrefab); Instantiate(prefabs[Random.Range(0, prefabs.Count)]);`
            int available = 1;
            bool cucumberUnlocked = _factories.ContainsKey(MicroVirusVariant.Cucumber) && _waveScalingConfig != null && ElapsedSeconds >= _waveScalingConfig.CucumberUnlockTimeSeconds;
            bool spinerUnlocked = _factories.ContainsKey(MicroVirusVariant.Spiner) && _waveScalingConfig != null && ElapsedSeconds >= _waveScalingConfig.SpinerUnlockTimeSeconds;

            if (cucumberUnlocked)
                available++;
            if (spinerUnlocked)
                available++;

            int choice = UnityEngine.Random.Range(0, available);
            if (choice == 0)
                return MicroVirusVariant.Orange;
            if (choice == 1)
                return cucumberUnlocked ? MicroVirusVariant.Cucumber : MicroVirusVariant.Spiner;
            return MicroVirusVariant.Spiner;
        }

        private void SetRandomPosition(Transform virusTransform)
        {
            // Old: InCellViruses.SetRandomPosition(Transform) - plain Random.Range within _bounds, no
            // rejection-sampling against existing positions (unlike CellSpawner). Routed through
            // SpawnPlacementUtility anyway (existingPositions: null, minSqrDistanceFromExisting: 0,
            // maxAttempts: 1) so there's one placement code path in the codebase instead of a second hand-rolled
            // Random.Range pair - with these arguments it always accepts the first candidate, reproducing the old
            // unconditional placement exactly.
            virusTransform.localPosition = SpawnPlacementUtility.FindPosition(_cellConfig.MicroVirusSpawnBounds, null, 0f, 1);
        }

        private void HandleVirusKilled(MicroVirusController virus)
        {
            _registry.Unregister(virus);

            // Old: InCellVirus.Damage()'s death branch -> `_virusManager.RemoveObject(transform);` triggered
            // GMTK.CellController.OnDestroyVirus() via `_inCellVirusManager.OnObjectRemoved += OnDestroyVirus;`
            // (wired in CellController.Start()). This spawner reproduces that direct call itself since it already
            // holds the sibling CellInfectionState reference - see class doc comment.
            if (_cellInfectionState != null)
                _cellInfectionState.RemoveVirus();

            OnVirusKilled?.Invoke();

            if (virus != null)
                virus.Despawn();
        }
    }
}
