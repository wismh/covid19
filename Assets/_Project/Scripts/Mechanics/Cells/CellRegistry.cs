using System;
using System.Collections.Generic;
using UnityEngine;
using _Project.Mechanics.Shared;
using _Project.Mechanics.Viruses.Macro;

namespace _Project.Mechanics.Cells
{
    /// <summary>Tracks all living cells, their infection state, and the aggregate living/sick/died counts and
    /// game-over/statistics events derived from them. Register takes both CellHealth and CellInfectionState since
    /// it needs both components' events (OnDied for the death count, OnInfectionStarted/OnInfectionCleared for
    /// the sick count) and CellHealth has no public reference to its sibling by design.
    ///
    /// Bookkeeping only - does not destroy a cell's GameObject on death, that stays CellSpawner's responsibility
    /// (the thing that Instantiate'd it). A cell death that leaves 0 living cells fires only
    /// <see cref="OnAllCellsDied"/>; every other death fires only <see cref="OnCellDied"/> (position + virus
    /// count at death) - the two are mutually exclusive.</summary>
    public sealed class CellRegistry : ICellTargetProvider
    {
        private readonly EntityRegistry<CellHealth> _cells = new();
        private readonly EntityRegistry<CellHealth> _infectedCells = new();
        private readonly Dictionary<CellHealth, CellEntry> _entries = new();

        private int _sickCellsCount;
        private int _diedCellsCount;

        public int LivingCellsCount => _cells.Count;
        public int SickCellsCount => _sickCellsCount;
        public int DiedCellsCount => _diedCellsCount;
        public int InfectedCellsCount => _infectedCells.Count;
        public IReadOnlyList<CellHealth> InfectedCells => _infectedCells.Entities;

        public event Action<int, int, int> OnStatisticsChanged;

        public event Action OnAllCellsDied;

        /// <summary>Carries (deathPosition, virusCountAtDeath) for MacroVirusSpawner.SpawnReplacementVirusesAsync.</summary>
        public event Action<Vector3, int> OnCellDied;

        /// <summary>Per-cell infection edge events - exposes WHICH cell, on top of the aggregate SickCellsCount
        /// HandleInfectionStarted/HandleInfectionCleared already track.</summary>
        public event Action<CellHealth> OnCellInfected;

        /// <summary>Also raised (not just the started/cleared pair) when an infected cell dies.</summary>
        public event Action<CellHealth> OnCellRecovered;

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

            if (infectionState != null && infectionState.IsInfected)
                _infectedCells.Register(health);

            OnStatisticsChanged?.Invoke(LivingCellsCount, _sickCellsCount, _diedCellsCount);
        }

        /// <summary>Manual removal without death (e.g. a future pooling reset).</summary>
        public void Unregister(CellHealth health)
        {
            if (health == null || !_entries.TryGetValue(health, out CellEntry entry))
                return;

            Unsubscribe(health, entry);
            _entries.Remove(health);
            _cells.Unregister(health);
            _infectedCells.Unregister(health);

            OnStatisticsChanged?.Invoke(LivingCellsCount, _sickCellsCount, _diedCellsCount);
        }

        public Transform FindNearestCell(Vector3 point)
        {
            CellHealth nearest = _cells.FindNearest(point);
            return nearest != null ? nearest.transform : null;
        }

        public Transform FindNearestCell(Vector3 point, out float sqrDistance)
        {
            CellHealth nearest = _cells.FindNearest(point, out sqrDistance);
            return nearest != null ? nearest.transform : null;
        }

        public CellHealth FindNearestInfectedCell(Vector3 point)
        {
            return _infectedCells.FindNearest(point);
        }

        public CellHealth FindNearestInfectedCell(Vector3 point, out float sqrDistance)
        {
            return _infectedCells.FindNearest(point, out sqrDistance);
        }

        public Transform FindNearestInfectedCellTransform(Vector3 point, out float sqrDistance)
        {
            CellHealth nearest = _infectedCells.FindNearest(point, out sqrDistance);
            return nearest != null ? nearest.transform : null;
        }

        private void HandleInfectionStarted(CellHealth health)
        {
            _sickCellsCount += 1;
            _infectedCells.Register(health);
            OnStatisticsChanged?.Invoke(LivingCellsCount, _sickCellsCount, _diedCellsCount);
            OnCellInfected?.Invoke(health);
        }

        private void HandleInfectionCleared(CellHealth health)
        {
            _sickCellsCount = Mathf.Max(0, _sickCellsCount - 1);
            _infectedCells.Unregister(health);
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
            _infectedCells.Unregister(health);

            // A cell can only reach 0 health while infected (CellHealth only subtracts health while virusCount
            // > 0), so this decrement is always valid.
            _sickCellsCount = Mathf.Max(0, _sickCellsCount - 1);
            _diedCellsCount += 1;

            OnStatisticsChanged?.Invoke(LivingCellsCount, _sickCellsCount, _diedCellsCount);
            OnCellRecovered?.Invoke(health);

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
