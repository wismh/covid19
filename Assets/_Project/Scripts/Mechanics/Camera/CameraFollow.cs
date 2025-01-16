using UnityEngine;

namespace _Project.Mechanics.Camera
{
    /// <summary>
    /// Smoothly follows a target Transform with offset, speed, optional smoothing modes
    /// (MoveTowards, Lerp, SmoothDamp), and optional movement boundary clamping.
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        public enum MovementType
        {
            MoveTowards = 0,
            Lerp = 1,
            SmoothDamp = 2
        }

        [Header("Target & Movement")]
        [SerializeField] private Transform _target;
        [SerializeField] private float _cameraSpeed = 5f;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);
        [SerializeField] private MovementType _movementType = MovementType.MoveTowards;
        [SerializeField] private float _smoothTime = 0.2f;

        [Header("Bounds (Optional)")]
        [SerializeField] private bool _useBounds = false;
        [SerializeField] private Vector2 _minBounds = new Vector2(-50f, -50f);
        [SerializeField] private Vector2 _maxBounds = new Vector2(50f, 50f);
        [SerializeField] private bool _clampByViewport = false;
        [SerializeField] private UnityEngine.Camera _camera;

        private Vector3 _currentVelocity;

        public Transform Target
        {
            get => _target;
            set => SetTarget(value);
        }

        public Vector3 Offset
        {
            get => _offset;
            set => _offset = value;
        }

        public float CameraSpeed
        {
            get => _cameraSpeed;
            set => _cameraSpeed = value;
        }

        public MovementType MovementMode
        {
            get => _movementType;
            set => _movementType = value;
        }

        public bool UseBounds
        {
            get => _useBounds;
            set => _useBounds = value;
        }

        public Vector2 MinBounds
        {
            get => _minBounds;
            set => _minBounds = value;
        }

        public Vector2 MaxBounds
        {
            get => _maxBounds;
            set => _maxBounds = value;
        }

        private void LateUpdate()
        {
            if (_target == null)
                return;

            UpdatePosition(Time.deltaTime);
        }

        /// <summary>
        /// Updates target transform to follow.
        /// </summary>
        public void SetTarget(Transform target)
        {
            _target = target;
        }

        /// <summary>
        /// Immediately sets camera position to target position plus offset (clamped to bounds if enabled).
        /// </summary>
        public void SnapToTarget()
        {
            if (_target == null)
                return;

            Vector3 targetPosition = _target.position + _offset;
            if (_useBounds)
                targetPosition = ClampToBounds(targetPosition);

            transform.position = targetPosition;
            _currentVelocity = Vector3.zero;
        }

        /// <summary>
        /// Configures camera movement bounds.
        /// </summary>
        public void SetBounds(Vector2 minBounds, Vector2 maxBounds, bool clampByViewport = false)
        {
            _minBounds = minBounds;
            _maxBounds = maxBounds;
            _clampByViewport = clampByViewport;
            _useBounds = true;
        }

        /// <summary>
        /// Enables or disables bounds clamping.
        /// </summary>
        public void SetBoundsEnabled(bool enabled)
        {
            _useBounds = enabled;
        }

        public void UpdatePosition(float deltaTime)
        {
            if (_target == null)
                return;

            Vector3 desiredPosition = _target.position + _offset;
            if (_useBounds)
                desiredPosition = ClampToBounds(desiredPosition);

            switch (_movementType)
            {
                case MovementType.MoveTowards:
                    transform.position = Vector3.MoveTowards(transform.position, desiredPosition, _cameraSpeed * deltaTime);
                    break;
                case MovementType.Lerp:
                    transform.position = Vector3.Lerp(transform.position, desiredPosition, _cameraSpeed * deltaTime);
                    break;
                case MovementType.SmoothDamp:
                    transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _currentVelocity, _smoothTime);
                    break;
            }
        }

        private Vector3 ClampToBounds(Vector3 position)
        {
            float minX = _minBounds.x;
            float maxX = _maxBounds.x;
            float minY = _minBounds.y;
            float maxY = _maxBounds.y;

            if (_clampByViewport && _camera.orthographic)
            {
                float halfHeight = _camera.orthographicSize;
                float halfWidth = halfHeight * _camera.aspect;

                if (minX + halfWidth <= maxX - halfWidth)
                {
                    minX += halfWidth;
                    maxX -= halfWidth;
                }

                if (minY + halfHeight <= maxY - halfHeight)
                {
                    minY += halfHeight;
                    maxY -= halfHeight;
                }
            }

            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.y = Mathf.Clamp(position.y, minY, maxY);
            return position;
        }
    }
}
