using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.AudioSystem;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Shared;
using _Project.Mechanics.Viruses.Micro;
using _Project.Mechanics.Waves;

namespace _Project.Mechanics.Cells
{
    /// <summary>Spawns cells (CellHealth+CellInfectionState+CellVisuals) at placement time and registers each
    /// with <see cref="CellRegistry"/>. Owns their GameObject lifecycle - destroys the instance a delay after
    /// death, cancellable via <see cref="_lifetimeCts"/> if the scene tears down mid-delay.</summary>
    public sealed class CellSpawner : MonoBehaviour
    {
        [SerializeField] private Transform _cellPrefab;

        private CellConfig _cellConfig;
        private CellRegistry _registry;
        private IPlayerTargetProvider _playerTargetProvider;
        private WaveScalingConfig _waveScalingConfig;
        private IReadOnlyDictionary<MicroVirusVariant, IFactory<MicroVirusController>> _microVirusFactories;
        private IAudioSystem _audioSystem;

        private readonly List<Vector3> _spawnedPositions = new();
        private CancellationTokenSource _lifetimeCts;

        /// <summary>Raised each time a spawned cell's MicroVirusSpawner is constructed - lets listeners (e.g.
        /// XP-granting on in-cell virus kills) reach it without CellSpawner needing to know why.</summary>
        public event Action<MicroVirusSpawner> OnMicroVirusSpawnerCreated;

        [Inject]
        public void Construct(CellConfig cellConfig, CellRegistry registry, IPlayerTargetProvider playerTargetProvider, WaveScalingConfig waveScalingConfig, IReadOnlyDictionary<MicroVirusVariant, IFactory<MicroVirusController>> microVirusFactories, IAudioSystem audioSystem)
        {
            _cellConfig = cellConfig;
            _registry = registry;
            _playerTargetProvider = playerTargetProvider;
            _waveScalingConfig = waveScalingConfig;
            _microVirusFactories = microVirusFactories;
            _audioSystem = audioSystem;
        }

        private void Awake()
        {
            _lifetimeCts = new CancellationTokenSource();
        }

        private void Start()
        {
            for (int i = 0; i < _cellConfig.CellCount; i++)
                SpawnCell();
        }

        private void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        }

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

            health.Construct(_cellConfig);
            if (instance.TryGetComponent(out CellVisuals visuals))
                visuals.Construct(_cellConfig, _audioSystem);

            _registry.Register(health, infectionState);

            health.OnDied += _ => DestroyCellAfterDelayAsync(instance).Forget();

            // Only Construct() happens here, deliberately not a CellInfectionState.OnVirusCountChanged
            // subscription - that produced full-size in-cell viruses wandering the macro world. SetVirusesCount
            // is instead called lazily, once, by WorldScaleTransition.EnterMicroWorldAsync when the player
            // actually enters this cell.
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
