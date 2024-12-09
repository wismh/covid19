using System;
using System.Collections.Generic;
using UnityEngine;
using _Project.Mechanics.Shared;
using _Project.Mechanics.Viruses.Macro;

namespace _Project.Mechanics.Cells
{
    /// <summary>
    /// Phase 7 Stage 1 - replaces GMTK.CellManager + GMTK.VirusCellManager + the counting/eventing half of
    /// GMTK.Services.GameStatistics (Assets/GMTK/Scripts/Behaviors/Cell/CellSpawner.cs's two `ObjectManager`
    /// subclasses, Assets/GMTK/Scripts/Services/GameStatistics.cs). This is the registry every Phase 3-5 doc
    /// comment pointed at as "no cell/virus registry exists yet" (CellInfectionState.cs, CellVisuals.cs,
    /// WorldScaleController.cs item 3, HudScreen.cs's "Statistics text: documented gap", GameOverScreen.cs's
    /// "documented gap", CellProximityPrompt.cs's "which cell problem").
    ///
    /// --- Why a purpose-built class instead of a bare EntityRegistry&lt;CellHealth&gt; ---
    /// Unlike MacroVirusRegistry/InCellVirusRegistry (thin pass-through wrappers - see their doc comments), Cells
    /// need real domain logic beyond "list of components": living/sick/died counts and the two composed
    /// game-over/statistics rules old GameStatistics + CellController.Die() implemented by hand. This class
    /// composes one `EntityRegistry&lt;CellHealth&gt;` internally as its bookkeeping primitive (register/
    /// unregister/find-nearest) and layers the counting/eventing rules on top - same relationship as
    /// CellHealth/CellVisuals composing CellInfectionState by direct reference rather than duplicating its logic.
    ///
    /// --- Registration API shape: why Register takes BOTH CellHealth and CellInfectionState ---
    /// CellHealth has no public reference to its sibling CellInfectionState (by design - see CellHealth's own doc
    /// comment: the two communicate through a direct [SerializeField] reference private to CellHealth, and
    /// CellInfectionState stays "oblivious" to anything beyond its own counting). This registry needs to observe
    /// BOTH components' events (CellHealth.OnDied for the died/living counts, CellInfectionState.OnInfectionStarted/
    /// OnInfectionCleared for the sick count) to reproduce GameStatistics' three counters, so the caller that
    /// spawns a cell (CellSpawner, which naturally has both component references right after Instantiate) passes
    /// both in at once.
    ///
    /// --- Lifecycle ownership split (mirrors MacroVirusSpawner/InCellVirusSpawner's split) ---
    /// This registry owns bookkeeping ONLY (counts, events) - it subscribes to a registered cell's own events
    /// purely to keep its counters and <see cref="OnStatisticsChanged"/>/<see cref="OnAllCellsDied"/>/
    /// <see cref="OnCellDied"/> events accurate, and unsubscribes/removes the cell from its internal list on
    /// death. It does NOT destroy the cell's GameObject - that stays a CellSpawner responsibility (the thing that
    /// Instantiate'd it), matching REFACTORING_PLAN.md section 4's staged pooling roadmap ("Пулінг - крок за
    /// кроком... для клітин/вірусів почати з такого ж простого пулу активації").
    ///
    /// --- Statistics event shape: built to match HudScreen.SetStatistics/GameOverScreen.SetAllCellsDied exactly ---
    /// <see cref="OnStatisticsChanged"/>'s signature `(int livingCells, int sickCells, int diedCells)` is
    /// byte-for-byte HudScreen.SetStatistics' parameter list and GMTK.Services.GameStatistics.OnStatisticsChange's
    /// old signature - a future MainSceneComposition (Phase 7 Stage 2) only needs
    /// `cellRegistry.OnStatisticsChanged += hudScreen.SetStatistics;` with no adapter. Likewise
    /// <see cref="OnAllCellsDied"/> maps to `() => { gameOverScreen.SetAllCellsDied(true);
    /// screenManager.OpenScreen&lt;GameOverScreen&gt;(); }` and <see cref="FindNearestCell(Vector3, out float)"/>
    /// maps to CellProximityPrompt.SetTargetCell (old GMTK.UI.AdviceDialog.Update()'s
    /// `_cellManager.FindNearToPoint(player.position, out sqrDistance)` call, re-run periodically by whatever
    /// Stage 2/3 wires up - this registry doesn't decide the polling cadence itself, same "documented seam, not
    /// invented registry" boundary CellProximityPrompt's own doc comment already drew).
    ///
    /// --- OnCellDied vs OnAllCellsDied: reproduces CellController.Die()'s if/else exactly ---
    /// Old: `if (_cellManager.Pool.Count == 0) HUD.GameOverGroup(true); else StartCoroutine(SpawnVirusesByDelay());`
    /// (Assets/GMTK/Scripts/Behaviors/Cell/CellController.cs). The two branches are mutually exclusive here too:
    /// a cell death that leaves 0 living cells fires ONLY <see cref="OnAllCellsDied"/>; every other cell death
    /// fires ONLY <see cref="OnCellDied"/> (position + virus count at death, for a future MacroVirusSpawner.
    /// SpawnReplacementVirusesAsync wiring - see that method's doc comment).
    /// </summary>
    public sealed class CellRegistry : ICellTargetProvider
    {
        private readonly EntityRegistry<CellHealth> _cells = new();
        private readonly Dictionary<CellHealth, CellEntry> _entries = new();

        private int _sickCellsCount;
        private int _diedCellsCount;

        public int LivingCellsCount => _cells.Count;
        public int SickCellsCount => _sickCellsCount;
        public int DiedCellsCount => _diedCellsCount;

        /// <summary>Old: GMTK.Services.GameStatistics.OnStatisticsChange - see class doc comment for the exact
        /// HudScreen.SetStatistics wiring this enables with no adapter.</summary>
        public event Action<int, int, int> OnStatisticsChanged;

        /// <summary>Old: the `if (_cellManager.Pool.Count == 0)` branch of CellController.Die() - see class doc
        /// comment's "OnCellDied vs OnAllCellsDied" section.</summary>
        public event Action OnAllCellsDied;

        /// <summary>Old: the `else StartCoroutine(SpawnVirusesByDelay())` branch of CellController.Die() -
        /// carries (deathPosition, virusCountAtDeath), the two pieces of data
        /// MacroVirusSpawner.SpawnReplacementVirusesAsync needs to reproduce
        /// `for (i = 0; i &lt; virusCount + 1; ++i) _virusSpawner.SpawnVirus(transform.position)`.</summary>
        public event Action<Vector3, int> OnCellDied;

        /// <summary>
        /// Phase 7 testing fix (2026-09-11): per-cell infection edge events, added so a composition root can
        /// mirror them into old GMTK.VirusCellManager (an ObjectManager the old GMTK.Arrow/ArrowController "nearest
        /// infected cell" indicator reads via FindNearToPoint) - see MainSceneComposition.Start()'s Arrow-bridging
        /// section. HandleInfectionStarted/HandleInfectionCleared already tracked this internally for the
        /// aggregate SickCellsCount; these just expose WHICH cell on top of that, the same way OnCellDied exposes
        /// more than OnAllCellsDied's plain signal.
        /// </summary>
        public event Action<CellHealth> OnCellInfected;

        /// <summary>See <see cref="OnCellInfected"/>'s doc comment. Also raised (not just the started/cleared
        /// pair) when an infected cell dies - see HandleCellDied, which always finds a cell sick per its own
        /// invariant note - so a mirrored old VirusCellManager entry never outlives its (about to be destroyed)
        /// Transform.</summary>
        public event Action<CellHealth> OnCellRecovered;

        /// <summary>Old: implicit pairing of CellManager.AddObject(transform) + VirusCellManager (added only once
        /// infected) - here both managers collapse into one Register call since this registry tracks both
        /// "is alive" and "is sick" state on the same entry.</summary>
        public void Register(CellHealth health, CellInfectionState infectionState)
        {
            if (health == null || _entries.ContainsKey(health))
                return;

            var entry = new CellEntry(infectionState);
            entry.OnDied = virusCount => HandleCellDied(health, virusCount);
            entry.OnInfectionStarted = () => HandleInfectionStarted(health);
            entry.OnInfectionCleared = () => HandleInfectionCleared(health);

            health.OnDied += entry.OnDied;
            if (infectionState != null)
            {
                infectionState.OnInfectionStarted += entry.OnInfectionStarted;
                infectionState.OnInfectionCleared += entry.OnInfectionCleared;
            }

            _entries[health] = entry;
            _cells.Register(health);

            OnStatisticsChanged?.Invoke(LivingCellsCount, _sickCellsCount, _diedCellsCount);
        }

        /// <summary>Manual removal without death (e.g. a future pooling reset). Symmetric counterpart to
        /// <see cref="Register"/> - old code never needed this since GMTK.ObjectManager.RemoveObject was only
        /// ever called from CellController.Die(), but this registry's Register does more (health + infection
        /// subscriptions) so an explicit unwind path is provided even though nothing calls it yet.</summary>
        public void Unregister(CellHealth health)
        {
            if (health == null || !_entries.TryGetValue(health, out CellEntry entry))
                return;

            Unsubscribe(health, entry);
            _entries.Remove(health);
            _cells.Unregister(health);

            OnStatisticsChanged?.Invoke(LivingCellsCount, _sickCellsCount, _diedCellsCount);
        }

        /// <summary>ICellTargetProvider - what MacroVirusSpawner passes to each spawned MacroVirusController's
        /// Construct call in place of a mocked/absent provider (see MoveToCellBehavior's doc comment). Old:
        /// `ServiceLocator.Instance.Get&lt;CellManager&gt;().FindNearToPoint(...)` (single-return overload).</summary>
        public Transform FindNearestCell(Vector3 point)
        {
            // Explicit null check rather than `?.` - Unity's null-conditional operator bypasses
            // UnityEngine.Object's overloaded equality (the "destroyed but not C#-null" gotcha), so this codebase
            // consistently checks Unity references with `!= null`/`== null` instead (see CellHealth/CellVisuals/
            // MacroVirusController elsewhere in this phase).
            CellHealth nearest = _cells.FindNearest(point);
            return nearest != null ? nearest.transform : null;
        }

        /// <summary>Old: GMTK.UI.AdviceDialog.Update()'s `_cellManager.FindNearToPoint(player.position, out
        /// sqrDistance)` - see class doc comment for how this feeds CellProximityPrompt.SetTargetCell.</summary>
        public Transform FindNearestCell(Vector3 point, out float sqrDistance)
        {
            CellHealth nearest = _cells.FindNearest(point, out sqrDistance);
            return nearest != null ? nearest.transform : null;
        }

        private void HandleInfectionStarted(CellHealth health)
        {
            // Old: CellController.OnAddVirus() -> `if (_virusCount == 0) { _gameStatistics.CellSick(); ... }`
            _sickCellsCount += 1;
            OnStatisticsChanged?.Invoke(LivingCellsCount, _sickCellsCount, _diedCellsCount);
            OnCellInfected?.Invoke(health);
        }

        private void HandleInfectionCleared(CellHealth health)
        {
            // Old: CellController.OnDestroyVirus() -> `if (_virusCount == 0) { ...; _gameStatistics.CellRecovered(); }`
            _sickCellsCount = Mathf.Max(0, _sickCellsCount - 1);
            OnStatisticsChanged?.Invoke(LivingCellsCount, _sickCellsCount, _diedCellsCount);
            OnCellRecovered?.Invoke(health);
        }

        private void HandleCellDied(CellHealth health, int virusCountAtDeath)
        {
            if (!_entries.TryGetValue(health, out CellEntry entry))
                return;

            Vector3 deathPosition = health.transform.position;

            Unsubscribe(health, entry);
            _entries.Remove(health);
            _cells.Unregister(health);

            // Old: GameStatistics.CellDied() -> `_livingCells -= 1; _sickCells -= 1; _cellsDied += 1;`. A cell can
            // only reach 0 health while infected (CellHealth.PoisonTickLoopAsync only subtracts when
            // virusCount &gt; 0), so the sick count decrement here is always valid - same invariant old code relied on.
            _sickCellsCount = Mathf.Max(0, _sickCellsCount - 1);
            _diedCellsCount += 1;

            OnStatisticsChanged?.Invoke(LivingCellsCount, _sickCellsCount, _diedCellsCount);
            OnCellRecovered?.Invoke(health);

            // Old: CellController.Die()'s `if (_cellManager.Pool.Count == 0) ... else ...` - see class doc
            // comment's "OnCellDied vs OnAllCellsDied" section.
            if (LivingCellsCount == 0)
                OnAllCellsDied?.Invoke();
            else
                OnCellDied?.Invoke(deathPosition, virusCountAtDeath);
        }

        private static void Unsubscribe(CellHealth health, CellEntry entry)
        {
            health.OnDied -= entry.OnDied;
            if (entry.InfectionState != null)
            {
                entry.InfectionState.OnInfectionStarted -= entry.OnInfectionStarted;
                entry.InfectionState.OnInfectionCleared -= entry.OnInfectionCleared;
            }
        }

        private sealed class CellEntry
        {
            public readonly CellInfectionState InfectionState;
            public Action<int> OnDied;
            public Action OnInfectionStarted;
            public Action OnInfectionCleared;

            public CellEntry(CellInfectionState infectionState)
            {
                InfectionState = infectionState;
            }
        }
    }
}
