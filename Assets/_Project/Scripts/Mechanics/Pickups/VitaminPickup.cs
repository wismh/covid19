using System;
using UnityEngine;
using _Project.Mechanics.Viruses.Micro;

namespace _Project.Mechanics.Pickups
{
    /// <summary>
    /// Stage 4: Vitamin pickup component. Replaces legacy GMTK.Vitamin (Assets/GMTK/Scripts/Behaviors/Medicals/Vitamin.cs).
    /// Disperses with an initial impulse/scatter and smoothly flies toward the player.
    /// When reaching the player, invokes its collected callback (XP is awarded by the owning
    /// MedicalPickup once all vitamins from the same pill arrive), plays a collection sound, and destroys itself.
    /// </summary>
    public class VitaminPickup : MonoBehaviour
    {
        [Header("Flight Settings")]
        [SerializeField] private float _distanceToEnd = 0.25f;
        [SerializeField] private float _vitaminSpeed = 9f;
        [SerializeField] private float _acceleration = 12f;

        [Header("Impulse Settings")]
        [SerializeField] private float _impulseDuration = 0.25f;
        [SerializeField] private float _impulseSpeed = 3.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource _collectAudioSource;
        [SerializeField] private AudioClip _collectClip;

        private IPlayerTargetProvider _targetProvider;
        private Transform _playerTransform;
        private Action _onCollected;

        private Vector2 _impulseDirection;
        private float _impulseTimer;
        private float _currentSpeed;
        private bool _isFlying;
        private bool _isCollected;

        public void Construct(
            IPlayerTargetProvider targetProvider,
            AudioSource collectAudioSource = null,
            AudioClip collectClip = null,
            Action onCollected = null)
        {
            _targetProvider = targetProvider;
            _collectAudioSource = collectAudioSource != null ? collectAudioSource : _collectAudioSource;
            _collectClip = collectClip != null ? collectClip : _collectClip;
            _onCollected = onCollected;
        }

        public void Construct(
            Transform playerTransform,
            AudioSource collectAudioSource = null,
            AudioClip collectClip = null,
            Action onCollected = null)
        {
            _playerTransform = playerTransform;
            _collectAudioSource = collectAudioSource != null ? collectAudioSource : _collectAudioSource;
            _collectClip = collectClip != null ? collectClip : _collectClip;
            _onCollected = onCollected;
        }

        /// <summary>
        /// Initiates the vitamin's impulse and flight towards the player.
        /// </summary>
        /// <param name="impulseDirection">Direction of initial outward scatter. If zero, calculates from parent center or random.</param>
        public void Launch(Vector2 impulseDirection = default)
        {
            if (impulseDirection == default)
            {
                if (transform.parent != null)
                {
                    Vector2 fromCenter = (transform.position - transform.parent.position);
                    impulseDirection = fromCenter.sqrMagnitude > 0.001f ? fromCenter.normalized : UnityEngine.Random.insideUnitCircle.normalized;
                }
                else
                {
                    impulseDirection = UnityEngine.Random.insideUnitCircle.normalized;
                }
            }

            _impulseDirection = impulseDirection;
            _impulseTimer = 0f;
            _currentSpeed = 0f;
            _isFlying = true;
        }

        /// <summary>
        /// Backwards-compatible hook for old callers or animation callbacks.
        /// </summary>
        public void PlayAnimation(Action finishCallback = null)
        {
            if (finishCallback != null)
                _onCollected += finishCallback;

            Launch();
        }

        private void Update()
        {
            if (!_isFlying || _isCollected)
                return;

            Vector3 targetPos = GetTargetPosition();

            // Initial impulse phase
            if (_impulseTimer < _impulseDuration)
            {
                _impulseTimer += Time.deltaTime;
                float decay = 1f - Mathf.Clamp01(_impulseTimer / _impulseDuration);
                transform.position += (Vector3)(_impulseDirection * (_impulseSpeed * decay * Time.deltaTime));
            }

            // Magnetic homing towards the player
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _vitaminSpeed, _acceleration * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, _currentSpeed * Time.deltaTime);

            // Arrival check
            float sqrDistance = (targetPos - transform.position).sqrMagnitude;
            if (sqrDistance < _distanceToEnd * _distanceToEnd || sqrDistance < 0.05f)
            {
                Collect();
            }
        }

        private Vector3 GetTargetPosition()
        {
            if (_targetProvider != null && _targetProvider.PlayerTransform != null)
                return _targetProvider.PlayerTransform.position;

            if (_playerTransform != null)
                return _playerTransform.position;

            return transform.position;
        }

        private void Collect()
        {
            if (_isCollected)
                return;

            _isCollected = true;
            _isFlying = false;

            PlayCollectSound();

            _onCollected?.Invoke();
            Destroy(gameObject);
        }

        private void PlayCollectSound()
        {
            if (_collectAudioSource != null)
            {
                if (!_collectAudioSource.isPlaying)
                    _collectAudioSource.Play();
            }
            else if (_collectClip != null)
            {
                AudioSource.PlayClipAtPoint(_collectClip, transform.position);
            }
        }
    }
}
