using UnityEngine;

namespace _Project.Mechanics.Cells
{
    /// <summary>Base-tunable numbers for cell health, poison damage, and spawn placement.</summary>
    [CreateAssetMenu(
        menuName = "Configurations/Cells/" + nameof(CellConfig),
        fileName = nameof(CellConfig) + "_Default")]
    public sealed class CellConfig : ScriptableObject
    {
        [Header("Health & poison")]
        [SerializeField] private float _baseHealth = 100f;
        [SerializeField] private float _poisonTickIntervalSeconds = 1f;

        // Damage per active micro virus per tick; kept as its own field rather than folded into the formula
        // so a balance pass can change it without touching CellHealth.PoisonTickLoopAsync.
        [SerializeField] private float _poisonDamagePerVirusPerTick = 1f;

        [Header("Visuals")]
        [SerializeField] private Bounds _entrailsArea = new Bounds(Vector3.zero, new Vector3(0.8f, 0.8f, 0f));

        [Header("Spawning & lifecycle")]
        [SerializeField] private int _cellCount = 20;
        [SerializeField] private float _minSqrDistanceBetweenCells = 35f;
        [SerializeField] private float _minSqrDistanceFromOrigin = 10f;
        [SerializeField] private int _maxPlacementAttempts = 5000;
        [SerializeField] private Bounds _spawnBounds = new Bounds(Vector3.zero, new Vector3(20f, 50f, 0f));
        [SerializeField] private float _deathDestroyDelaySeconds = 2f;

        // Local-space area micro viruses are randomly positioned within on spawn/reposition.
        [SerializeField] private Bounds _microVirusSpawnBounds = new Bounds(Vector3.zero, new Vector3(4f, 4f, 0f));

        public float BaseHealth => _baseHealth;
        public float PoisonTickIntervalSeconds => _poisonTickIntervalSeconds;
        public float PoisonDamagePerVirusPerTick => _poisonDamagePerVirusPerTick;
        public Bounds EntrailsArea => _entrailsArea;

        public int CellCount => _cellCount;
        public float MinSqrDistanceBetweenCells => _minSqrDistanceBetweenCells;
        public float MinSqrDistanceFromOrigin => _minSqrDistanceFromOrigin;
        public int MaxPlacementAttempts => _maxPlacementAttempts;
        public Bounds SpawnBounds => _spawnBounds;
        public float DeathDestroyDelaySeconds => _deathDestroyDelaySeconds;
        public Bounds MicroVirusSpawnBounds => _microVirusSpawnBounds;
    }
}
