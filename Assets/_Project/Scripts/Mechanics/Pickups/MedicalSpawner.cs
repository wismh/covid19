using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Micro;

namespace _Project.Mechanics.Pickups
{
    /// <summary>
    /// Spawner for MedicalPickup pills. Spawns them at regular intervals within specified bounds, enforcing a
    /// maximum concurrent pill limit. Injected with PlayerStatsService and IPlayerTargetProvider via Construct().
    /// Pools MedicalPickup instances via <see cref="MedicalPickup.Factory"/> (bound by PickupsInstaller) instead
    /// of a [SerializeField] prefab + Instantiate - see that class's own "Pooling migration" doc comment.
    /// </summary>
    public class MedicalSpawner : MonoBehaviour
    {
        // Spawn bounds/interval/count tunables now live on PickupsConfig instead of local [SerializeField]s.
        private PickupsConfig _config;
        private PlayerStatsService _playerStatsService;
        private IPlayerTargetProvider _targetProvider;
        private MedicalPickup.Factory _medicalFactory;

        private readonly List<MedicalPickup> _activeMedicals = new();
        private bool _isPaused;
        private Coroutine _spawnCoroutine;

        public int ActiveCount => CleanAndCount();

        [Inject]
        public void Construct(PickupsConfig config, PlayerStatsService playerStatsService, IPlayerTargetProvider targetProvider, MedicalPickup.Factory medicalFactory)
        {
            _config = config;
            _playerStatsService = playerStatsService;
            _targetProvider = targetProvider;
            _medicalFactory = medicalFactory;
        }

        public void SetPaused(bool isPaused)
        {
            _isPaused = isPaused;
        }

        private void Start()
        {
            _spawnCoroutine = StartCoroutine(SpawnRoutine());
        }

        private void OnDestroy()
        {
            if (_spawnCoroutine != null)
                StopCoroutine(_spawnCoroutine);
        }

        private IEnumerator SpawnRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(_config.SpawnIntervalSeconds);

                if (_isPaused)
                    continue;

                int activeCount = CleanAndCount();
                int available = _config.MaxActiveMedicals - activeCount;
                int toSpawn = Mathf.Clamp(_config.NumToSpawnPerInterval, 0, available);

                for (int i = 0; i < toSpawn; i++)
                {
                    SpawnMedical();
                }
            }
        }

        private void SpawnMedical()
        {
            Bounds spawnBounds = _config.SpawnBounds;
            Vector3 position = new Vector3(
                UnityEngine.Random.Range(spawnBounds.min.x, spawnBounds.max.x),
                UnityEngine.Random.Range(spawnBounds.min.y, spawnBounds.max.y),
                0f
            );

            Quaternion rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            MedicalPickup medical = _medicalFactory.Create(position, rotation);
            medical.transform.SetParent(transform, worldPositionStays: true);

            medical.OnDestroyed += HandleMedicalDestroyed;
            _activeMedicals.Add(medical);
        }

        private void HandleMedicalDestroyed(MedicalPickup medical)
        {
            if (medical != null)
            {
                medical.OnDestroyed -= HandleMedicalDestroyed;
                _activeMedicals.Remove(medical);
            }
        }

        private int CleanAndCount()
        {
            _activeMedicals.RemoveAll(m => m == null);
            return _activeMedicals.Count;
        }
    }
}
