using UnityEngine;

namespace _Project.Mechanics.Cells
{
    /// <summary>
    /// Every base-tunable number found while porting GMTK.CellController (Assets/GMTK/Scripts/Behaviors/Cell/
    /// CellController.cs) - see TECHNICAL_AUDIT.md section 2.2 ("Магічні числа") for why these used to be
    /// scattered literals/serialized fields on the monolithic component instead of a single config asset.
    /// Concrete numbers below were read directly from Assets/GMTK/Prefabs/Cell.prefab's serialized component
    /// values, not guessed.
    ///
    /// Note: there is no separate "poison damage curve" - old CellController.PoisonCoroutine subtracts the raw
    /// infection count directly (`_health -= _virusCount`), i.e. 1 HP/second per active in-cell virus. The only
    /// actually-tunable number in that formula is the tick interval, so that's what's exposed here
    /// (PoisonDamagePerVirusPerTick exists purely to name the "1" that was an implicit unit in the old subtraction,
    /// in case future balancing wants viruses to deal something other than 1 HP/tick each).
    /// </summary>
    [CreateAssetMenu(
        menuName = "Configurations/Cells/" + nameof(CellConfig),
        fileName = nameof(CellConfig) + "_Default")]
    public sealed class CellConfig : ScriptableObject
    {
        [Header("Health & poison (ported from GMTK.CellController.PoisonCoroutine)")]
        [SerializeField] private float _baseHealth = 100f;

        // Old: CellController.k_poisonCoroutineTimeStep (private const, 1f) - also the DOTween duration used to
        // tween the "_Blend" shader parameter toward the new health ratio each tick.
        [SerializeField] private float _poisonTickIntervalSeconds = 1f;

        // Old: CellController.PoisonCoroutine -> `_health -= _virusCount;` - each active virus was an implicit,
        // unnamed "1 HP/tick" of damage. Named here so a future balance pass has a single place to change it
        // without altering the health-computation formula itself (see CellHealth.PoisonTickLoopAsync).
        [SerializeField] private float _poisonDamagePerVirusPerTick = 1f;

        [Header("Visuals (values from Cell.prefab CellController component)")]
        // Old: CellController._entrailsArea, used by ShuffleEntrails() to randomize entrail piece positions.
        [SerializeField] private Bounds _entrailsArea = new Bounds(Vector3.zero, new Vector3(0.8f, 0.8f, 0f));

        [Header("Spawning & lifecycle (ported from GMTK.CellSpawner/CellController, values from GameplayScene.unity's " +
                "CellSpawner instance and CellController.Die()'s hardcoded destroy delay)")]
        [SerializeField] private int _cellCount = 20;
        [SerializeField] private float _minSqrDistanceBetweenCells = 35f;
        [SerializeField] private float _minSqrDistanceFromOrigin = 10f;
        [SerializeField] private int _maxPlacementAttempts = 5000;
        [SerializeField] private Bounds _spawnBounds = new Bounds(Vector3.zero, new Vector3(20f, 50f, 0f));

        // Old: CellController.Die() -> `Destroy(gameObject, 2f);`
        [SerializeField] private float _deathDestroyDelaySeconds = 2f;

        // Old: InCellViruses._bounds (values from Cell.prefab's InCellViruses component: center (0,0,0), extent
        // (2,2,0)) - local-space area in-cell viruses are randomly positioned within on spawn/reposition.
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
