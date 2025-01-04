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
        [Header("Audio")]
        [SerializeField] private AudioSource _collectAudioSource;
        [SerializeField] private AudioClip _collectClip;

        // Flight/impulse tunables now live on PickupsConfig instead of local [SerializeField]s.
        private PickupsConfig _config;
        private IPlayerTargetProvider _targetProvider;
        private Transform _playerTransform;
        private Action _onCollected;

        private Vector2 _impulseDirection;
        private float _impulseTimer;
        private float _currentSpeed;
        private bool _isFlying;
        private bool _isCollected;

        public void Construct(
            PickupsConfig config,
            IPlayerTargetProvider targetProvider,
            AudioSource collectAudioSource = null,
            AudioClip collectClip = null,
            Action onCollected = null)
        {
            _config = config;
            _targetProvider = targetProvider;
            _collectAudioSource = collectAudioSource != null ? collectAudioSource : _collectAudioSource;
            _collectClip = collectClip != null ? collectClip : _collectClip;
            _onCollected = onCollected;
        }

        public void Construct(
            PickupsConfig config,
            Transform playerTransform,
            AudioSource collectAudioSource = null,
            AudioClip collectClip = null,
            Action onCollected = null)
        {
            _config = config;
            _playerTransform = playerTransform;
            _collectAudioSource = collectAudioSource != null ? collectAudioSource : _collectAudioSource;
            _collectClip = collectClip != null ? collectClip : _collectClip;
            _onCollected = onCollected;
        }

        /// <summary>
        /// Initiates the vitamin's impulse and flight towards the player. Reactivates the GameObject - see
        /// ResetForPool's doc comment for why vitamins now start hidden and only become visible here.
        /// </summary>
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
            if (_impulseTimer < _config.VitaminImpulseDurationSeconds)
            {
                _impulseTimer += Time.deltaTime;
                float decay = 1f - Mathf.Clamp01(_impulseTimer / _config.VitaminImpulseDurationSeconds);
                transform.position += (Vector3)(_impulseDirection * (_config.VitaminImpulseSpeed * decay * Time.deltaTime));
            }

            // Magnetic homing towards the player
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _config.VitaminSpeed, _config.VitaminAcceleration * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, _currentSpeed * Time.deltaTime);

            // Arrival check
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

            PlayCollectSound();

            _onCollected?.Invoke();

            // Deactivated, not destroyed - see MedicalPickup's own class doc comment ("Pooling migration"): a
            // VitaminPickup is a permanent baked-in child of the pooled MedicalPickup prefab, so destroying it
            // here would leave that slot permanently empty the next time the parent is respawned from the pool.
            // MedicalPickup.OnSpawned reactivates and repositions every vitamin via ResetForPool.
            gameObject.SetActive(false);
        }

        /// <summary>Repositions this vitamin to <paramref name="localPosition"/> (its original, prefab-authored
        /// slot) and clears the flight/collected state Collect() left behind - called by MedicalPickup.OnSpawned
        /// on every respawn, see that method's own doc comment. Deactivates rather than reactivates - the pill
        /// hasn't broken yet at spawn time, so its vitamins should stay hidden until <see cref="Launch"/>
        /// actually releases them (user-reported bug: with the pooled instance always reactivating all 4
        /// vitamins immediately on spawn, an interrupted/edge-case life could leave one visibly detached-looking
        /// near an otherwise-intact, unbroken pill - "Medicals_N" siblings sitting outside any "Medical" parent
        /// in the Hierarchy). Reactivation now happens in Launch() instead, at the one moment a vitamin is
        /// actually meant to be seen.</summary>
        public void ResetForPool(Vector3 localPosition)
        {
            transform.localPosition = localPosition;
            gameObject.SetActive(false);

            _isCollected = false;
            _isFlying = false;
            _impulseTimer = 0f;
            _currentSpeed = 0f;
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
