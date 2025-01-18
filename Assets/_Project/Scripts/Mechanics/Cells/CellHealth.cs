using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace _Project.Mechanics.Cells
{
    /// <summary>Health and poison-over-time damage for a cell, scaled by <see cref="CellInfectionState"/>'s
    /// current virus count. Only raises <see cref="OnDied"/>/<see cref="OnHealed"/> with the relevant data (e.g.
    /// virus count at death) - forcing the player out of the cell, game-over, and spawning replacement viruses
    /// are all left to whatever listens to those events, not decided here.</summary>
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

        /// <summary>Raised every poison tick with the new health value, including ticks where health didn't
        /// change (e.g. a currently-uninfected cell) - CellVisuals needs that same cadence for its shader tween.</summary>
        public event Action<float> OnHealthChanged;

        /// <summary>Raised once health drops to 0 or below, carrying the virus count at time of death.</summary>
        public event Action<int> OnDied;

        /// <summary>Raised when infection clears and health resets to max.</summary>
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

            _infectionState.OnInfectionCleared += HandleInfectionCleared;

            PoisonTickLoopAsync(_lifetimeCts.Token).Forget();
        }

        private void OnDestroy()
        {
            _infectionState.OnInfectionCleared -= HandleInfectionCleared;

            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        }

        private void HandleInfectionCleared()
        {
            _health = _config.BaseHealth;
            OnHealthChanged?.Invoke(_health);
            OnHealed?.Invoke();
        }

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

                int virusCount = _infectionState.VirusCount;
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
