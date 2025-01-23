using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Shared;
using _Project.Mechanics.Waves;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>Spawns/tracks the in-cell micro viruses for one cell instance - privately owned per-cell (not a
    /// shared singleton like CellRegistry/MacroVirusRegistry), meant to live alongside
    /// CellHealth/CellInfectionState/CellVisuals on the Cell prefab. <see cref="SetVirusesCount"/> is called
    /// lazily, once, when the player actually enters this cell (not reactively on every infection change) -
    /// spawning eagerly on infection produced full-size in-cell viruses visibly wandering the macro world.</summary>
    public sealed class MicroVirusSpawner : MonoBehaviour
    {
        [SerializeField] private CellInfectionState _cellInfectionState;

        // Cell.prefab's root is scaled 3x; this container (a child scaled 0.15x to net 0.45x) keeps spawned
        // viruses hidden and correctly sized until the player is actually inside. Falls back to this component's
        // own transform if left unwired.
        [SerializeField] private Transform _virusContainer;

        private readonly MicroVirusRegistry _registry = new();

        private CellConfig _cellConfig;
        private IPlayerTargetProvider _playerTargetProvider;
        private WaveScalingConfig _waveScalingConfig;

        private IReadOnlyDictionary<MicroVirusVariant, IFactory<MicroVirusController>> _factories;

        /// <summary>Raised once per killed virus - VirusExperienceDriver subscribes to grant XP without this
        /// class needing to know about PlayerStatsService.</summary>
        public event Action OnVirusKilled;

        // No shared elapsed-time service exists yet to gate variant unlocks on - see MacroVirusSpawner.
        private float ElapsedSeconds => Time.timeSinceLevelLoad;

        [Inject]
        public void Construct(CellConfig cellConfig, IPlayerTargetProvider playerTargetProvider, WaveScalingConfig waveScalingConfig, IReadOnlyDictionary<MicroVirusVariant, IFactory<MicroVirusController>> factories)
        {
            _cellConfig = cellConfig;
            _playerTargetProvider = playerTargetProvider;
            _waveScalingConfig = waveScalingConfig;
            _factories = factories;
        }

        public void SetVirusesCount(int count)
        {
            for (int i = _registry.Count; i < count; i++)
                SpawnVirus();

            foreach (MicroVirusController virus in _registry.Viruses)
                SetRandomPosition(virus.transform);
        }

        private void SpawnVirus()
        {
            if (_factories == null || _factories.Count == 0)
            {
                Debug.LogWarning($"{nameof(MicroVirusSpawner)}.{nameof(SpawnVirus)} called before pools were bound - no-op.", this);
                return;
            }

            MicroVirusVariant variant = ChooseVariant();

            Transform container = _virusContainer != null ? _virusContainer : transform;
            MicroVirusController virus = _factories[variant].Create();
            virus.transform.SetParent(container, worldPositionStays: false);

            _registry.Register(virus);

            virus.OnKilled += () => HandleVirusKilled(virus);
        }

        private MicroVirusVariant ChooseVariant()
        {
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
            // No rejection-sampling needed here (unlike CellSpawner) - maxAttempts: 1 always accepts the first
            // candidate, routed through SpawnPlacementUtility just to keep one placement code path in the codebase.
            virusTransform.localPosition = SpawnPlacementUtility.FindPosition(_cellConfig.MicroVirusSpawnBounds, null, 0f, 1);
        }

        private void HandleVirusKilled(MicroVirusController virus)
        {
            _registry.Unregister(virus);
            _cellInfectionState.RemoveVirus();

            OnVirusKilled?.Invoke();

            if (virus != null)
                virus.Despawn();
        }
    }
}
