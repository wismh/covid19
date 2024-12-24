using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Micro;

namespace _Project.Mechanics.Pickups
{
    /// <summary>
    /// Stage 4: Spawner for MedicalPickup pills. Replaces legacy GMTK.MedicalSpawner (Assets/GMTK/Scripts/Behaviors/Medicals/MedicalSpawner.cs).
    /// Spawns MedicalPickup pills at regular intervals within specified bounds, enforcing a maximum concurrent pill limit.
    /// Injected with PlayerStatsService and IPlayerTargetProvider via Construct().
    /// </summary>
    public class MedicalSpawner : MonoBehaviour
    {
        [Header("Prefab & Bounds")]
        [SerializeField] private MedicalPickup _medicalPrefab;
        [SerializeField] private Bounds _spawnBounds = new Bounds(Vector3.zero, new Vector3(24f, 54f, 0f));

        [Header("Spawn Settings")]
        [SerializeField] private float _spawnInterval = 15f;
        [SerializeField] private int _numToSpawn = 3;
        [SerializeField] private int _maxMedicals = 10;

        private PlayerStatsService _playerStatsService;
        private IPlayerTargetProvider _targetProvider;
        private Transform _playerTransform;

        private readonly List<MedicalPickup> _activeMedicals = new();
        private bool _isPaused;
        private Coroutine _spawnCoroutine;

        public int ActiveCount => CleanAndCount();

        public void Construct(PlayerStatsService playerStatsService, IPlayerTargetProvider targetProvider)
        {
            _playerStatsService = playerStatsService;
            _targetProvider = targetProvider;
        }

        public void Construct(PlayerStatsService playerStatsService, Transform playerTransform)
        {
            _playerStatsService = playerStatsService;
            _playerTransform = playerTransform;
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
                yield return new WaitForSeconds(_spawnInterval);

                if (_isPaused || _medicalPrefab == null)
                    continue;

                int activeCount = CleanAndCount();
                int available = _maxMedicals - activeCount;
                int toSpawn = Mathf.Clamp(_numToSpawn, 0, available);

                for (int i = 0; i < toSpawn; i++)
                {
                    SpawnMedical();
                }
            }
        }

        private void SpawnMedical()
        {
            if (_medicalPrefab == null)
                return;

            Vector3 position = new Vector3(
                UnityEngine.Random.Range(_spawnBounds.min.x, _spawnBounds.max.x),
                UnityEngine.Random.Range(_spawnBounds.min.y, _spawnBounds.max.y),
                0f
            );

            Quaternion rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            MedicalPickup medical = Instantiate(_medicalPrefab, position, rotation, transform);

            if (_targetProvider != null)
                medical.Construct(_playerStatsService, _targetProvider);
            else if (_playerTransform != null)
                medical.Construct(_playerStatsService, _playerTransform);

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
