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
    /// legible from the field name alone, per this phase's explicit instructions. This is intentionally scoped
    /// to ONLY the four numbers called out above - it does not include VirusSpawner's other tunables
    /// (_virusCount=10, _distanceFromCenter=30) or InCellViruses._bounds, since those aren't difficulty-gating
    /// thresholds and weren't part of the audit's duplication complaint; they'd belong to a future
    /// VirusSpawner/InCellViruses port instead (out of scope for this phase - only the shared placement utility
    /// and this config were asked for, not spawner-manager classes).
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

        public float GlistUnlockTimeSeconds => _glistUnlockTimeSeconds;
        public float WaveSizeGrowthMultiplier => _waveSizeGrowthMultiplier;
        public float CucumberUnlockTimeSeconds => _cucumberUnlockTimeSeconds;
        public float SpinerUnlockTimeSeconds => _spinerUnlockTimeSeconds;
    }
}
