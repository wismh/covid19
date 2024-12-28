using UnityEngine;

namespace _Project.Mechanics.Waves
{
    /// <summary>
    /// Replaces the inline time-threshold magic numbers TECHNICAL_AUDIT.md section 2.2 calls out by name:
    /// "Часові гейти складності задубльовані в двох різних файлах з різними магічними порогами (80, 50, 100) без
    /// спільного конфігу кривої складності." The two files in question:
    ///
    /// - Assets/GMTK/Scripts/Behaviors/MacroVirus/VirusSpawner.cs -
    ///   `if (_playerStats.Time >= 80) prefabs.Add(_glistPrefab);` (harder macro-virus variant unlock)
    ///   and `Update()` -> `_virusCount *= 1.5f;` each time a new wave spawns (wave-size growth).
    /// - Assets/GMTK/Scripts/Behaviors/MicroVirus/InCellViruses.cs -
    ///   `if (_playerStats.Time >= 50) prefabs.Add(_cucumberPrefab);` and
    ///   `if (_playerStats.Time >= 100) prefabs.Add(_spinerPrefab);` (in-cell virus variant unlocks).
    ///
    /// Every field name below deliberately echoes its old-code origin so the source of each number stays
    /// legible from the field name alone, per this phase's explicit instructions. Originally scoped to ONLY the
    /// four difficulty-gating thresholds above; extended later (2026-09-19 hardcode audit) to also cover
    /// MacroVirusSpawner's wave-size/placement/elite-chance/respawn-delay numbers, which this class's own doc
    /// comment used to explicitly call "out of scope for this phase" - that gap is now closed.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Configurations/Waves/" + nameof(WaveScalingConfig),
        fileName = nameof(WaveScalingConfig) + "_Default")]
    public sealed class WaveScalingConfig : ScriptableObject
    {
        [Header("Macro virus (ported from GMTK.VirusSpawner)")]
        // Old: VirusSpawner.SpawnVirus() -> `if (_playerStats.Time >= 80) prefabs.Add(_glistPrefab);`
        [SerializeField] private float _glistUnlockTimeSeconds = 80f;

        // Old: VirusSpawner.Update() -> `_virusCount *= 1.5f;` (applied each time a new wave is triggered).
        [SerializeField] private float _waveSizeGrowthMultiplier = 1.5f;

        [Header("In-cell virus (ported from GMTK.MicroViruses.InCellViruses)")]
        // Old: InCellViruses.SpawnVirus() -> `if (_playerStats.Time >= 50) prefabs.Add(_cucumberPrefab);`
        [SerializeField] private float _cucumberUnlockTimeSeconds = 50f;

        // Old: InCellViruses.SpawnVirus() -> `if (_playerStats.Time >= 100) prefabs.Add(_spinerPrefab);`
        [SerializeField] private float _spinerUnlockTimeSeconds = 100f;

        [Header("Macro virus wave sizing/placement (ported from GMTK.VirusSpawner, values from GameplayScene.unity's " +
                "VirusSpawner instance)")]
        [SerializeField] private float _initialWaveSize = 10f;
        [SerializeField] private float _distanceFromCenter = 30f;

        // Old: VirusSpawner.SpawnVirus(Vector3) -> `Random.Range(0, 2) == 1` - a coin-flip once the glist variant
        // is unlocked (GlistUnlockTimeSeconds elapsed).
        [SerializeField] private float _eliteSpawnChance = 0.5f;

        // Old: CellController.Die()'s `else StartCoroutine(SpawnVirusesByDelay())` -> `yield return new
        // WaitForSeconds(1f);` before spawning replacement viruses.
        [SerializeField] private float _replacementSpawnDelaySeconds = 1f;

        public float GlistUnlockTimeSeconds => _glistUnlockTimeSeconds;
        public float WaveSizeGrowthMultiplier => _waveSizeGrowthMultiplier;
        public float CucumberUnlockTimeSeconds => _cucumberUnlockTimeSeconds;
        public float SpinerUnlockTimeSeconds => _spinerUnlockTimeSeconds;

        public float InitialWaveSize => _initialWaveSize;
        public float DistanceFromCenter => _distanceFromCenter;
        public float EliteSpawnChance => _eliteSpawnChance;
        public float ReplacementSpawnDelaySeconds => _replacementSpawnDelaySeconds;
    }
}
