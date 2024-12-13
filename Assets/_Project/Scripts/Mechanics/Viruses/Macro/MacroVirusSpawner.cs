using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Waves;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>
    /// Phase 7 Stage 1 - replaces GMTK.VirusSpawner (Assets/GMTK/Scripts/Behaviors/MacroVirus/VirusSpawner.cs).
    /// Spawns the new MacroVirusController (Phase 3) instead of the old VirusController, and registers/
    /// unregisters with <see cref="MacroVirusRegistry"/> instead of the old ServiceLocator-registered VirusManager.
    ///
    /// --- Placement: ring formula ported unchanged, SpawnPlacementUtility does NOT apply here ---
    /// Old VirusSpawner.SpawnVirus() placed viruses on a ring around the origin (`_distanceFromCenter *
    /// sin/cos(randomAngle)`), never using rejection sampling against existing positions - a different algorithm
    /// from CellSpawner's, not a second copy of the same one. SpawnPlacementUtility unifies the latter (Phase 3
    /// item 5); it has no bearing on this formula, so it's ported as-is rather than forced through that utility
    /// for uniformity's sake.
    ///
    /// --- Re-trigger / wave growth: ported unchanged from VirusSpawner.Update() ---
    /// `if (_virusManager.Pool.Count == 0 &amp;&amp; _gameStatistics.SickCells() &lt; 2) { spawn _virusCount; _virusCount
    /// *= 1.5f; }` becomes `if (_registry.Count == 0 &amp;&amp; _cellRegistry.SickCellsCount &lt; 2) { spawn _waveSize;
    /// _waveSize *= WaveScalingConfig.WaveSizeGrowthMultiplier; }` - <see cref="CellRegistry"/> replaces the old
    /// GameStatistics sick-cell counter (see CellRegistry.SickCellsCount).
    ///
    /// --- Elapsed-time source: a real, documented gap (same one HudScreen's class doc comment already names) ---
    /// Old `_playerStats.Time` (incremented once/second by GMTK.UI.HUD.StartTime()'s coroutine) gated the glist
    /// unlock (`if (_playerStats.Time &gt;= 80) prefabs.Add(_glistPrefab);`). No `_Project` equivalent shared
    /// elapsed-time service exists yet - HudScreen.cs's own doc comment ("Timer" section) explicitly says its
    /// UniTask-driven MM:SS counter is a display-only copy with nothing else wired to feed from it, since no
    /// registry/composition root existed when Phase 5 was written. This spawner uses `Time.timeSinceLevelLoad`
    /// as a direct substitute - accurate as long as this component starts running at the same moment gameplay
    /// begins (true for a spawner placed in the gameplay scene), and avoids introducing a second, subtly
    /// different accumulator alongside HudScreen's. If a future phase promotes elapsed time to a proper shared
    /// service, this is the one line (<see cref="ElapsedSeconds"/>) to repoint.
    ///
    /// --- Registration/lifecycle: spawner owns both, mirroring CellSpawner ---
    /// This spawner registers every virus it spawns and ALSO subscribes to that virus's own OnKilled/
    /// OnPenetratedIntoCell events to unregister + destroy it - MacroVirusController deliberately holds no
    /// registry reference (Phase 3's decoupling decision, see its own doc comment), so whichever code spawned an
    /// instance is the natural owner of unwinding it. Old: VirusController.Damage()'s death branch did
    /// `_virusManager.RemoveObject(transform); Destroy(gameObject, 1);` itself (a ServiceLocator lookup this
    /// class no longer needs); PenetrationIntoCell.OnAnimationEnd's old
    /// `ServiceLocator...Get&lt;VirusManager&gt;().RemoveObject(_transform); Object.Destroy(...)` is reproduced the
    /// same way via <see cref="MacroVirusController.OnPenetratedIntoCell"/>.
    ///
    /// --- Cross-system wiring left to Stage 2 ---
    /// <see cref="SpawnReplacementVirusesAsync"/> (old: CellController.Die()'s `else
    /// StartCoroutine(SpawnVirusesByDelay())` branch) and <see cref="SetPaused"/> (old:
    /// `_virusManager.gameObject.SetActive(false/true)` from GamePlayState.State's setter - see
    /// WorldScaleController.cs's "Other systems coupled to world scale" item 3) are public entry points, not
    /// self-wired subscriptions: this phase builds registry+spawners in isolation (REFACTORING_PLAN.md Phase 7
    /// Stage 1), a future MainSceneComposition (Stage 2) connects `cellRegistry.OnCellDied +=
    /// (pos, count) =&gt; macroVirusSpawner.SpawnReplacementVirusesAsync(pos, count).Forget();` and
    /// `worldScaleController.OnEnteredMicroWorld += () =&gt; macroVirusSpawner.SetPaused(true);` /
    /// `OnExitedMicroWorld += () =&gt; macroVirusSpawner.SetPaused(false);`.
    /// </summary>
    public sealed class MacroVirusSpawner : MonoBehaviour
    {
        [Header("Prefabs - assigned by a later stage (prefab/scene surgery), see class doc comment")]
        [SerializeField] private MacroVirusController _virusPrefab;
        [SerializeField] private MacroVirusController _glistPrefab;

        [Header("Wave sizing (ported from GMTK.VirusSpawner, values from SampleScene.unity's VirusSpawner instance)")]
        [SerializeField] private float _initialWaveSize = 10f;
        [SerializeField] private float _distanceFromCenter = 30f;

        private float _waveSize;

        private MacroVirusConfig _config;
        private CellRegistry _cellRegistry;
        private MacroVirusRegistry _registry;
        private WaveScalingConfig _waveScalingConfig;

        private bool _isPaused;
        private CancellationTokenSource _lifetimeCts;

        /// <summary>See class doc comment's "Elapsed-time source" section.</summary>
        private float ElapsedSeconds => Time.timeSinceLevelLoad;

        [Inject]
        public void Construct(MacroVirusConfig config, CellRegistry cellRegistry, MacroVirusRegistry registry, WaveScalingConfig waveScalingConfig)
        {
            _config = config;
            _cellRegistry = cellRegistry;
            _registry = registry;
            _waveScalingConfig = waveScalingConfig;
        }

        private void Awake()
        {
            _lifetimeCts = new CancellationTokenSource();
            _waveSize = _initialWaveSize;
        }

        private void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        }

        /// <summary>Old: VirusSpawner.Update() - see class doc comment's "Re-trigger / wave growth" section.</summary>
        private void Update()
        {
            if (_isPaused || _registry == null || _cellRegistry == null)
                return;

            if (_registry.Count == 0 && _cellRegistry.SickCellsCount < 2)
            {
                int count = Mathf.RoundToInt(_waveSize);
                for (int i = 0; i < count; i++)
                    SpawnVirus();

                _waveSize *= _waveScalingConfig != null ? _waveScalingConfig.WaveSizeGrowthMultiplier : 1.5f;
            }
        }

        /// <summary>Old: VirusSpawner.SpawnVirus() (no-position overload) - ring placement around the origin.</summary>
        public void SpawnVirus()
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector3 position = new Vector3(
                _distanceFromCenter * Mathf.Sin(angle),
                _distanceFromCenter * Mathf.Cos(angle),
                0f);

            SpawnVirus(position);
        }

        /// <summary>Old: VirusSpawner.SpawnVirus(Vector3) - picks between the base virus and the glist variant
        /// once WaveScalingConfig.GlistUnlockTimeSeconds has elapsed, matching old `_playerStats.Time &gt;= 80`.</summary>
        public void SpawnVirus(Vector3 position)
        {
            if (_virusPrefab == null || _config == null || _registry == null)
            {
                Debug.LogWarning($"{nameof(MacroVirusSpawner)}.{nameof(SpawnVirus)} called before prefabs/config/registry were assigned - no-op. See class doc comment's prefab seam.", this);
                return;
            }

            bool glistUnlocked = _glistPrefab != null
                && _waveScalingConfig != null
                && ElapsedSeconds >= _waveScalingConfig.GlistUnlockTimeSeconds;

            MacroVirusController prefab = glistUnlocked && UnityEngine.Random.Range(0, 2) == 1 ? _glistPrefab : _virusPrefab;

            MacroVirusController virus = Instantiate(prefab, transform);
            virus.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0, 360)));

            virus.Construct(_config, _cellRegistry);
            _registry.Register(virus);

            virus.OnKilled += () => HandleVirusRemoved(virus);
            virus.OnPenetratedIntoCell += () => HandleVirusRemoved(virus);
        }

        private void HandleVirusRemoved(MacroVirusController virus)
        {
            _registry.Unregister(virus);

            if (virus != null)
                Destroy(virus.gameObject);
        }

        /// <summary>
        /// Old: CellController.Die()'s `else StartCoroutine(SpawnVirusesByDelay())` branch -
        /// `yield return new WaitForSeconds(1f); for (i = 0; i &lt; _virusCount + 1; ++i) _virusSpawner.SpawnVirus
        /// (transform.position);`. Public entry point for Stage 2 to wire to
        /// <see cref="_Project.Mechanics.Cells.CellRegistry.OnCellDied"/> - see class doc comment's "Cross-system
        /// wiring left to Stage 2" section.
        /// </summary>
        public async UniTask SpawnReplacementVirusesAsync(Vector3 position, int virusCountAtDeath, CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCts.Token);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1f), cancellationToken: linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            for (int i = 0; i < virusCountAtDeath + 1; i++)
                SpawnVirus(position);
        }

        /// <summary>
        /// See class doc comment's "Cross-system wiring left to Stage 2" section - old:
        /// `_virusManager.gameObject.SetActive(false/true)`.
        ///
        /// Phase 8 testing fix (2026-09-11): this only ever set <see cref="_isPaused"/>, gating this spawner's
        /// OWN Update() (new-wave spawning/re-triggering) - it never reproduced the GameObject.SetActive() half
        /// of the old call, which is what actually stopped EVERY already-spawned macro virus's Update()/
        /// FixedUpdate() (movement toward cells, attacking, penetrating). User-reported symptom: macro viruses
        /// kept flying around and infecting cells while the player was inside one, exactly as if nothing had
        /// changed. Fixed by also toggling this spawner's own GameObject active state - every spawned
        /// MacroVirusController is a child of this transform (see SpawnVirus()'s `Instantiate(prefab, transform)`
        /// calls), so deactivating the parent deactivates all of them at once, the same blunt-but-correct
        /// approach old code used. A virus mid-SpawnReplacementVirusesAsync delay when this fires still
        /// instantiates on schedule (that UniTaskVoid isn't tied to this GameObject's active state) but lands as
        /// an inactive-in-hierarchy child until reactivated - consistent with "everything pauses in here."
        /// </summary>
        public void SetPaused(bool isPaused)
        {
            _isPaused = isPaused;
            gameObject.SetActive(!isPaused);
        }
    }
}
