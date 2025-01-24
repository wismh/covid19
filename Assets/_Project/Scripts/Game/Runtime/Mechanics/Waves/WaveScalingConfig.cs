using UnityEngine;

namespace _Project.Mechanics.Waves
{
    /// <summary>Difficulty-gating thresholds and wave sizing/placement numbers for virus spawning.</summary>
    [CreateAssetMenu(
        menuName = "Configurations/Waves/" + nameof(WaveScalingConfig),
        fileName = nameof(WaveScalingConfig) + "_Default")]
    public sealed class WaveScalingConfig : ScriptableObject
    {
        [Header("Macro virus")]
        [SerializeField] private float _glistUnlockTimeSeconds = 80f;
        [SerializeField] private float _waveSizeGrowthMultiplier = 1.5f;

        [Header("Micro virus")]
        [SerializeField] private float _cucumberUnlockTimeSeconds = 50f;
        [SerializeField] private float _spinerUnlockTimeSeconds = 100f;

        [Header("Macro virus wave sizing/placement")]
        [SerializeField] private float _initialWaveSize = 10f;
        [SerializeField] private float _distanceFromCenter = 30f;
        [SerializeField] private float _eliteSpawnChance = 0.5f;
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
