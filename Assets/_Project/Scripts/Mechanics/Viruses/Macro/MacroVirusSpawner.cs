using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Waves;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>Spawns macro viruses in waves on a ring around the origin, re-triggering a growing wave whenever
    /// the registry empties out and few cells are sick. Owns registration/unregistration for every virus it
    /// spawns, since MacroVirusController itself holds no registry reference. <see cref="SpawnReplacementVirusesAsync"/>/
    /// <see cref="SetPaused"/> are public entry points other systems wire to (cell death, world-scale
    /// transitions) rather than self-wired subscriptions.</summary>
    public sealed class MacroVirusSpawner : MonoBehaviour
    {
        private float _waveSize;

        private MacroVirusConfig _config;
        private CellRegistry _cellRegistry;
        private MacroVirusRegistry _registry;
        private WaveScalingConfig _waveScalingConfig;

        private IReadOnlyDictionary<MacroVirusVariant, IFactory<Vector3, Quaternion, MacroVirusController>> _factories;

        private bool _isPaused;
        private CancellationTokenSource _lifetimeCts;

        // No shared elapsed-time service exists yet to gate the Glist unlock on - timeSinceLevelLoad is accurate
        // enough since this spawner starts running at the same moment gameplay begins.
        private float ElapsedSeconds => Time.timeSinceLevelLoad;

        [Inject]
        public void Construct(MacroVirusConfig config, CellRegistry cellRegistry, MacroVirusRegistry registry, WaveScalingConfig waveScalingConfig, IReadOnlyDictionary<MacroVirusVariant, IFactory<Vector3, Quaternion, MacroVirusController>> factories)
        {
            _config = config;
            _cellRegistry = cellRegistry;
            _registry = registry;
            _waveScalingConfig = waveScalingConfig;
            _factories = factories;
            _waveSize = waveScalingConfig.InitialWaveSize;
        }

        private void Awake()
        {
            _lifetimeCts = new CancellationTokenSource();
        }

        private void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        }

        private void Update()
        {
            if (_isPaused || _registry == null || _cellRegistry == null)
                return;

            if (_registry.Count == 0 && _cellRegistry.SickCellsCount < 2)
            {
                int count = Mathf.RoundToInt(_waveSize);
                for (int i = 0; i < count; i++)
                    SpawnVirus();

                _waveSize *= _waveScalingConfig.WaveSizeGrowthMultiplier;
            }
        }

        public void SpawnVirus()
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float distanceFromCenter = _waveScalingConfig.DistanceFromCenter;
            Vector3 position = new Vector3(
                distanceFromCenter * Mathf.Sin(angle),
                distanceFromCenter * Mathf.Cos(angle),
                0f);

            SpawnVirus(position);
        }

        public void SpawnVirus(Vector3 position)
        {
            if (_factories == null || _factories.Count == 0 || _config == null || _registry == null)
            {
                Debug.LogWarning($"{nameof(MacroVirusSpawner)}.{nameof(SpawnVirus)} called before pools/config/registry were bound - no-op.", this);
                return;
            }

            bool glistUnlocked = _factories.ContainsKey(MacroVirusVariant.Glist)
                && ElapsedSeconds >= _waveScalingConfig.GlistUnlockTimeSeconds;

            MacroVirusVariant variant = glistUnlocked && UnityEngine.Random.value < _waveScalingConfig.EliteSpawnChance ? MacroVirusVariant.Glist : MacroVirusVariant.Virus;

            Quaternion rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0, 360));
            MacroVirusController virus = _factories[variant].Create(position, rotation);
            virus.transform.SetParent(transform, worldPositionStays: true);

            _registry.Register(virus);

            virus.OnKilled += () => HandleVirusRemoved(virus);
            virus.OnPenetratedIntoCell += () => HandleVirusRemoved(virus);
        }

        private void HandleVirusRemoved(MacroVirusController virus)
        {
            _registry.Unregister(virus);

            if (virus != null)
                virus.Despawn();
        }

        /// <summary>Wired to <see cref="_Project.Mechanics.Cells.CellRegistry.OnCellDied"/> - spawns
        /// virusCountAtDeath + 1 replacement viruses after a delay.</summary>
        public async UniTask SpawnReplacementVirusesAsync(Vector3 position, int virusCountAtDeath, CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCts.Token);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_waveScalingConfig.ReplacementSpawnDelaySeconds), cancellationToken: linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            for (int i = 0; i < virusCountAtDeath + 1; i++)
                SpawnVirus(position);
        }

        /// <summary>Deactivates this spawner's GameObject (parent of every spawned virus) to stop their
        /// Update()/FixedUpdate() - e.g. while the player is inside a cell. Also explicitly pauses/resumes each
        /// registered virus's DOTween tweens by target: a virus mid-PenetrationIntoCellBehavior reparents onto
        /// its target cell (leaving this spawner's hierarchy) and its shrink animation keeps ticking regardless
        /// of GameObject active state, since DOTween has its own persistent update loop.</summary>
        public void SetPaused(bool isPaused)
        {
            _isPaused = isPaused;
            gameObject.SetActive(!isPaused);

            if (_registry == null)
                return;

            foreach (MacroVirusController virus in _registry.Viruses)
            {
                if (virus == null)
                    continue;

                if (isPaused)
                    DOTween.Pause(virus.transform);
                else
                    DOTween.Play(virus.transform);
            }
        }
    }
}
