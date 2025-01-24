using System;
using UnityEngine;

namespace _Project.Mechanics.Cells
{
    /// <summary>Tracks a cell's micro-virus count and raises start/clear/changed events for it. Health reset on
    /// cure and any registry/stats bookkeeping are left to subscribers (see <see cref="CellHealth"/>), not handled
    /// here.</summary>
    public sealed class CellInfectionState : MonoBehaviour
    {
        private int _virusCount;

        public int VirusCount => _virusCount;
        public bool IsInfected => _virusCount > 0;

        /// <summary>Raised on the 0 -> 1 transition only.</summary>
        public event Action OnInfectionStarted;

        /// <summary>Raised on the 1 -> 0 transition only.</summary>
        public event Action OnInfectionCleared;

        public event Action<int> OnVirusCountChanged;

        /// <summary>Called by PenetrationIntoCellBehavior when a macro virus finishes penetrating a cell.</summary>
        public void AddVirus()
        {
            _virusCount += 1;

            if (_virusCount == 1)
                OnInfectionStarted?.Invoke();

            OnVirusCountChanged?.Invoke(_virusCount);
        }

        /// <summary>Called when a micro virus dies. Clamped at 0 as a defensive guard against unpaired calls.</summary>
        public void RemoveVirus()
        {
            _virusCount = Mathf.Max(0, _virusCount - 1);

            if (_virusCount == 0)
                OnInfectionCleared?.Invoke();

            OnVirusCountChanged?.Invoke(_virusCount);
        }
    }
}
