using UnityEngine;

namespace _Project.Mechanics.Navigation
{
    /// <summary>
    /// Visual presentation component for the navigation indicator.
    /// Rotates to point towards the active target and controls the visibility of its SpriteRenderer.
    /// </summary>
    public sealed class Arrow : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private Transform _target;
        private bool _isActive;

        public bool IsActive => _isActive;
        public Transform Target => _target;

        private void Awake()
        {
            SetArrowState(false);
        }

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        public void SetArrowState(bool state)
        {
            _isActive = state;
            _spriteRenderer.enabled = state;
        }

        private void Update()
        {
            if (!_isActive || _target == null)
                return;

            Vector3 direction = _target.position - transform.position;
            direction.z = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.right = direction.normalized;
            }
        }
    }
}
