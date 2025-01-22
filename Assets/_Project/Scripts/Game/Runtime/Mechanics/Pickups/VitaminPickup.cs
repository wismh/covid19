using System;
using Project.Core.AudioSystem;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Viruses.Micro;

namespace _Project.Mechanics.Pickups
{
    /// <summary>Vitamin pickup. Disperses with an initial impulse/scatter and flies toward the player; on
    /// arrival invokes its collected callback (XP is awarded by the owning MedicalPickup once all vitamins from
    /// the same pill arrive), plays a collection sound, and deactivates.</summary>
    public class VitaminPickup : MonoBehaviour
    {
        [SerializeField] private SoundData _collectSound;

        private IAudioSystem _audioSystem;
        private PickupsConfig _config;
        private IPlayerTargetProvider _targetProvider;
        private Transform _playerTransform;
        private Action _onCollected;

        private Vector2 _impulseDirection;
        private float _impulseTimer;
        private float _currentSpeed;
        private bool _isFlying;
        private bool _isCollected;

        [Inject]
        public void Construct(IAudioSystem audioSystem)
        {
            _audioSystem = audioSystem;
        }

        public void Construct(
            PickupsConfig config,
            IPlayerTargetProvider targetProvider,
            Action onCollected = null)
        {
            _config = config;
            _targetProvider = targetProvider;
            _onCollected = onCollected;
        }

        public void Construct(
            PickupsConfig config,
            Transform playerTransform,
            Action onCollected = null)
        {
            _config = config;
            _playerTransform = playerTransform;
            _onCollected = onCollected;
        }

        /// <param name="impulseDirection">Direction of initial outward scatter. If zero, calculates from parent center or random.</param>
        public void Launch(Vector2 impulseDirection = default)
        {
            gameObject.SetActive(true);

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

        /// <summary>Backwards-compatible hook for old callers or animation callbacks.</summary>
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

            if (_impulseTimer < _config.VitaminImpulseDurationSeconds)
            {
                _impulseTimer += Time.deltaTime;
                float decay = 1f - Mathf.Clamp01(_impulseTimer / _config.VitaminImpulseDurationSeconds);
                transform.position += (Vector3)(_impulseDirection * (_config.VitaminImpulseSpeed * decay * Time.deltaTime));
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _config.VitaminSpeed, _config.VitaminAcceleration * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, _currentSpeed * Time.deltaTime);

            float sqrDistance = (targetPos - transform.position).sqrMagnitude;
            if (sqrDistance < _config.VitaminDistanceToEnd * _config.VitaminDistanceToEnd || sqrDistance < _config.VitaminMinArrivalDistanceSqr)
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

            _audioSystem.PlaySfx(_collectSound);

            _onCollected?.Invoke();

            // Deactivated, not destroyed - a permanent baked-in child of the pooled MedicalPickup prefab;
            // MedicalPickup.OnSpawned reactivates and repositions every vitamin via ResetForPool.
            gameObject.SetActive(false);
        }

        /// <summary>Repositions this vitamin to <paramref name="localPosition"/> (its original, prefab-authored
        /// slot) and clears the flight/collected state Collect() left behind. Deactivates rather than reactivates
        /// - the pill hasn't broken yet at spawn time, so vitamins stay hidden until Launch() releases them.</summary>
        public void ResetForPool(Vector3 localPosition)
        {
            transform.localPosition = localPosition;
            gameObject.SetActive(false);

            _isCollected = false;
            _isFlying = false;
            _impulseTimer = 0f;
            _currentSpeed = 0f;
        }
    }
}
