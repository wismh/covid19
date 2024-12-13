using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace _Project.Mechanics.Cells
{
    /// <summary>
    /// Ports the health/poison-over-time half of GMTK.CellController (Assets/GMTK/Scripts/Behaviors/Cell/
    /// CellController.cs - PoisonCoroutine + the health-reset branch of OnDestroyVirus). See
    /// CellInfectionState's doc comment for the full split rationale (REFACTORING_PLAN.md section 3.4).
    ///
    /// Composition: holds a direct [SerializeField] reference to <see cref="CellInfectionState"/> - the same
    /// sibling-reference idiom Phase 2's PlayerHealth used for PlayerMovement (`[SerializeField] private
    /// PlayerMovement _movement`) rather than inventing a new orchestrator/"CellController" class just to wire
    /// two components together. CellHealth reads CellInfectionState.VirusCount every poison tick (replacing old
    /// CellController's own `_virusCount` field) and subscribes to CellInfectionState.OnInfectionCleared to
    /// reproduce the old `if (_virusCount == 0) _health = 100;` reset - CellInfectionState itself stays
    /// oblivious to health entirely.
    ///
    /// Corutine -> UniTask: old CellController.PoisonCoroutine (`StartCoroutine(PoisonCoroutine())`) becomes a
    /// UniTask loop with a CancellationTokenSource disposed in OnDestroy, matching PlayerHealth's pattern from
    /// Phase 2 (REFACTORING_PLAN.md section 4 / section 5 "Корутини -> UniTask").
    ///
    /// Scope note: old CellController.Die() additionally forced the player out of macro world if they were
    /// inside the dying cell, removed the cell from GMTK.CellManager/VirusCellManager, played a death
    /// animation/audio, and either triggered game-over (no cells left) or spawned `virusCount + 1` replacement
    /// macro viruses after a 1s delay. All of that is now reproduced elsewhere, once the systems it depends on
    /// existed: MainSceneComposition.WireCrossSystemEvents (force-macro-exit, game-over, replacement viruses -
    /// Phase 8 testing fix, 2026-09-11) and CellVisuals.HandleDied (death animation/audio, same round). CellHealth
    /// itself still only raises <see cref="OnDied"/> with the virus count at time of death, exactly like
    /// MacroVirusController.OnKilled/InCellVirusController.OnKilled defer XP-granting to a future composition
    /// root instead of reaching into old ServiceLocator-resolved managers directly - it stays the single source
    /// of truth every listener above hangs off of.
    /// </summary>
    public sealed class CellHealth : MonoBehaviour
    {
        [SerializeField] private CellInfectionState _infectionState;

        private CellConfig _config;
        private CancellationTokenSource _lifetimeCts;

        private float _health;
        private bool _isAlive = true;

        public float Health => _health;
        public float MaxHealth => _config.BaseHealth;
        public bool IsAlive => _isAlive;

        /// <summary>Raised every poison tick with the new health value (including ticks where health didn't
        /// change, e.g. a currently-uninfected cell) - old CellController tweened its shader "_Blend" parameter
        /// on every tick regardless of whether health actually moved, so CellVisuals needs the same cadence.</summary>
        public event Action<float> OnHealthChanged;

        /// <summary>Raised once health drops to 0 or below. Carries the infection count at time of death so a
        /// future listener can reproduce the old "spawn virusCount + 1 replacement macro viruses" rule without
        /// CellHealth needing to know about virus spawning itself.</summary>
        public event Action<int> OnDied;

        /// <summary>Raised when infection clears and health resets to max (old: OnDestroyVirus -> `_health = 100`).</summary>
        public event Action OnHealed;

        [Inject]
        public void Construct(CellConfig config)
        {
            _config = config;
        }

        private void Awake()
        {
            _lifetimeCts = new CancellationTokenSource();
        }

        private void Start()
        {
            _health = _config.BaseHealth;

            if (_infectionState != null)
                _infectionState.OnInfectionCleared += HandleInfectionCleared;

            PoisonTickLoopAsync(_lifetimeCts.Token).Forget();
        }

        private void OnDestroy()
        {
            if (_infectionState != null)
                _infectionState.OnInfectionCleared -= HandleInfectionCleared;

            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        }

        private void HandleInfectionCleared()
        {
            // Old: CellController.OnDestroyVirus -> `_health = 100;`
            _health = _config.BaseHealth;
            OnHealthChanged?.Invoke(_health);
            OnHealed?.Invoke();
        }

        /// <summary>Old: CellController.PoisonCoroutine - `while (true) { yield return new
        /// WaitForSeconds(k_poisonCoroutineTimeStep); _health -= _virusCount; if (_health &lt;= 0) { Die(); yield
        /// break; } DOTween.To(... _health / 100 ...); }`.</summary>
        private async UniTaskVoid PoisonTickLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_config.PoisonTickIntervalSeconds), cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!_isAlive)
                    return;

                int virusCount = _infectionState != null ? _infectionState.VirusCount : 0;
                _health -= virusCount * _config.PoisonDamagePerVirusPerTick;

                OnHealthChanged?.Invoke(_health);

                if (_health <= 0f)
                {
                    _isAlive = false;
                    OnDied?.Invoke(virusCount);
                    return;
                }
            }
        }
    }
}
