using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace _Project.Mechanics.Environment
{
    /// <summary>
    /// Background bacteria visual drifting behavior.
    /// Moves continuously along a cyclic sequence of waypoints and rotates to face movement direction.
    /// Replaces legacy GMTK.BackgroundBacteria.
    /// </summary>
    public sealed class BackgroundBacteria : MonoBehaviour
    {
        [SerializeField] private Vector2 _currentDestination;
        [SerializeField] private Vector2 _previousDestination;
        [SerializeField] private float _maxProgress = 5f;

        private ReadOnlyCollection<Vector2> _destinationsList;
        private bool _isInitialized;
        private float _currentProgress;

        public Vector2 CurrentDestination => _currentDestination;
        public Vector2 PreviousDestination => _previousDestination;
        public float MaxProgress => _maxProgress;
        public bool IsInitialized => _isInitialized;

        private void Update()
        {
            if (!_isInitialized) return;

            float t = _maxProgress > 0f ? Mathf.Clamp01(_currentProgress / _maxProgress) : 1f;
            Vector2 interpolated = Vector2.Lerp(_previousDestination, _currentDestination, t);
            transform.position = new Vector3(interpolated.x, interpolated.y, transform.position.z);

            Vector2 direction = _currentDestination - _previousDestination;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.right = direction.normalized;
            }

            if (_currentProgress >= _maxProgress)
            {
                SetNewDestination(_currentDestination);
                _currentProgress = 0f;
            }
            else
            {
                _currentProgress += Time.deltaTime;
            }
        }

        private void SetNewDestination(Vector2 previousDest)
        {
            if (_destinationsList == null || _destinationsList.Count == 0) return;

            int index = _destinationsList.IndexOf(previousDest);
            if (index < 0) index = 0;

            _previousDestination = _destinationsList[index];
            _currentDestination = _destinationsList[index < _destinationsList.Count - 1 ? index + 1 : 0];
        }

        public void Init(List<Vector2> destinationLists)
        {
            if (destinationLists == null || destinationLists.Count == 0) return;

            _currentProgress = 0f;
            _destinationsList = destinationLists.AsReadOnly();
            _previousDestination = _destinationsList[0];
            transform.position = new Vector3(_destinationsList[0].x, _destinationsList[0].y, transform.position.z);
            SetNewDestination(_destinationsList[0]);
            _isInitialized = true;
        }

        public void Init(IReadOnlyList<Vector2> destinationLists)
        {
            if (destinationLists == null || destinationLists.Count == 0) return;

            List<Vector2> copy = new List<Vector2>(destinationLists);
            Init(copy);
        }
    }
}
