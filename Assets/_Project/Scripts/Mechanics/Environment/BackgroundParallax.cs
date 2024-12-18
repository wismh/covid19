using UnityEngine;

namespace _Project.Mechanics.Environment
{
    /// <summary>
    /// Shifts background layer position based on target Transform (usually Main Camera) movement
    /// to create a parallax scrolling depth effect.
    /// Replaces legacy global BackgroundParallax.
    /// </summary>
    public sealed class BackgroundParallax : MonoBehaviour
    {
        [SerializeField] private Transform _targetObject;
        [SerializeField, Range(0f, 1f)] private float _scrollSpeed;

        public Transform TargetObject
        {
            get => _targetObject;
            set => _targetObject = value;
        }

        public float ScrollSpeed
        {
            get => _scrollSpeed;
            set => _scrollSpeed = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            if (_targetObject == null && UnityEngine.Camera.main != null)
            {
                _targetObject = UnityEngine.Camera.main.transform;
            }
        }

        private void Update()
        {
            if (_targetObject == null) return;

            float factor = 1f - _scrollSpeed;
            transform.position = new Vector3(
                _targetObject.position.x * factor,
                _targetObject.position.y * factor,
                transform.position.z
            );
        }
    }
}
