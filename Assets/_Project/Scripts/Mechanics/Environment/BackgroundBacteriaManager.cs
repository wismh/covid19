using System.Collections.Generic;
using UnityEngine;
using _Project.Mechanics.Shared;

namespace _Project.Mechanics.Environment
{
    /// <summary>
    /// Manages spawning and waypoint path distribution for background bacteria.
    /// Replaces legacy GMTK.BackgroundBacteriaManager.
    /// Uses <see cref="SpawnPlacementUtility"/> for point distribution across bounds.
    /// </summary>
    public sealed class BackgroundBacteriaManager : MonoBehaviour
    {
        public const int MAX_CHOICES_VALUE = 500;

        [Header("Prefabs & Count")]
        [SerializeField] private List<BackgroundBacteria> _prefabs;
        [SerializeField] private int _startBacteriaCount;

        [Header("Bounds & Destinations")]
        [SerializeField] private Bounds _bounds;
        [SerializeField] private int _destinationsCount = 3;
        [SerializeField] private float _distanceThreshold = 25f;

        private readonly List<Vector3> _allDestinations = new();

        public IReadOnlyList<BackgroundBacteria> Prefabs => _prefabs;
        public int StartBacteriaCount => _startBacteriaCount;
        public Bounds Bounds => _bounds;
        public int DestinationsCount => _destinationsCount;
        public float DistanceThreshold => _distanceThreshold;
        public IReadOnlyList<Vector3> AllDestinations => _allDestinations;

        private void Start()
        {
            InitList();
        }

        public void InitList()
        {
            if (_prefabs == null || _prefabs.Count == 0)
            {
                Debug.LogWarning("[BackgroundBacteriaManager] No bacteria prefabs assigned.", this);
                return;
            }

            _allDestinations.Clear();

            for (int i = 0; i < _startBacteriaCount; ++i)
            {
                int randomBacteriaIndex = Random.Range(0, _prefabs.Count);
                BackgroundBacteria prefab = _prefabs[randomBacteriaIndex];
                if (prefab == null) continue;

                BackgroundBacteria backgroundBacteria = Instantiate(prefab, transform);
                backgroundBacteria.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                backgroundBacteria.Init(GetRandomDestinationList());
            }
        }

        private List<Vector2> GetRandomDestinationList()
        {
            List<Vector2> destinationList = new List<Vector2>(_destinationsCount);

            for (int i = 0; i < _destinationsCount; i++)
            {
                Vector3 pos = SpawnPlacementUtility.FindPosition(
                    _bounds,
                    _allDestinations,
                    _distanceThreshold,
                    MAX_CHOICES_VALUE
                );

                _allDestinations.Add(pos);
                destinationList.Add(pos);
            }

            return destinationList;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.3f);
            Gizmos.DrawWireCube(_bounds.center, _bounds.size);
        }
    }
}
