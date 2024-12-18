using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using _Project.Mechanics.Combat;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.InCell;

namespace _Project.Mechanics.Pickups
{
    /// <summary>
    /// Stage 4: Medical pill pickup component. Replaces legacy GMTK.Medical (Assets/GMTK/Scripts/Behaviors/Medicals/Medical.cs).
    /// Implements _Project.Mechanics.Combat.IDamageable. When taking lethal damage:
    /// plays explosion animation and audio, disables collision, and releases child VitaminPickups to fly towards the player.
    /// Manages destruction and passes PlayerStatsService and player target dependencies down to vitamins.
    /// </summary>
    public class MedicalPickup : MonoBehaviour, IDamageable
    {
        [Header("Health & Timing")]
        [SerializeField] private int _health = 1;
        [SerializeField] private float _breakDelay = 1f;

        [Header("Components")]
        [SerializeField] private Animator _animator;
        [SerializeField] private Collider2D _boxCollider;
        [SerializeField] private AudioSource _openAudioSource;
        [SerializeField] private AudioSource _collectAudioSource;
        [SerializeField] private List<VitaminPickup> _vitamins;

        private PlayerStatsService _playerStatsService;
        private IPlayerTargetProvider _targetProvider;
        private Transform _playerTransform;

        private bool _isBroken;
        private int _remainingVitamins;

        public event Action<MedicalPickup> OnDestroyed;

        public void Construct(PlayerStatsService playerStatsService, IPlayerTargetProvider targetProvider)
        {
            _playerStatsService = playerStatsService;
            _targetProvider = targetProvider;

            ResolveComponents();
            PropagateDependenciesToVitamins();
        }

        public void Construct(PlayerStatsService playerStatsService, Transform playerTransform)
        {
            _playerStatsService = playerStatsService;
            _playerTransform = playerTransform;

            ResolveComponents();
            PropagateDependenciesToVitamins();
        }

        private void Awake()
        {
            ResolveComponents();
        }

        private void ResolveComponents()
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();

            if (_boxCollider == null)
                _boxCollider = GetComponent<BoxCollider2D>();

            var audioSources = GetComponents<AudioSource>();
            if (audioSources.Length > 0 && _openAudioSource == null)
                _openAudioSource = audioSources[0];
            if (audioSources.Length > 1 && _collectAudioSource == null)
                _collectAudioSource = audioSources[1];

            if (_vitamins == null || _vitamins.Count == 0)
            {
                _vitamins = new List<VitaminPickup>(GetComponentsInChildren<VitaminPickup>(true));
            }
        }

        private void PropagateDependenciesToVitamins()
        {
            if (_vitamins == null)
                return;

            foreach (var vitamin in _vitamins)
            {
                if (vitamin == null)
                    continue;

                if (_targetProvider != null)
                {
                    vitamin.Construct(_playerStatsService, _targetProvider, _collectAudioSource, null, HandleVitaminCollected);
                }
                else
                {
                    vitamin.Construct(_playerStatsService, _playerTransform, _collectAudioSource, null, HandleVitaminCollected);
                }
            }
        }

        public void Damage(int damage, Vector3 attackDirection)
        {
            if (_isBroken)
                return;

            _health -= damage;
            if (_health <= 0)
            {
                Break();
            }
        }

        private void Break()
        {
            _isBroken = true;

            if (_animator != null)
                _animator.Play("Base Layer.Explosion");

            if (_openAudioSource != null)
                _openAudioSource.Play();

            if (_boxCollider != null)
                _boxCollider.enabled = false;

            StartCoroutine(ReleaseVitaminsRoutine());
        }

        private IEnumerator ReleaseVitaminsRoutine()
        {
            if (_breakDelay > 0f)
                yield return new WaitForSeconds(_breakDelay);

            if (_animator != null)
                _animator.enabled = false;

            // Refresh list in case children changed
            if (_vitamins == null || _vitamins.Count == 0)
                _vitamins = new List<VitaminPickup>(GetComponentsInChildren<VitaminPickup>(true));

            _remainingVitamins = _vitamins.Count;

            if (_remainingVitamins == 0)
            {
                FinishAndDestroy();
                yield break;
            }

            foreach (var vitamin in _vitamins)
            {
                if (vitamin == null)
                {
                    _remainingVitamins--;
                    continue;
                }

                if (_targetProvider != null)
                    vitamin.Construct(_playerStatsService, _targetProvider, _collectAudioSource, null, HandleVitaminCollected);
                else
                    vitamin.Construct(_playerStatsService, _playerTransform, _collectAudioSource, null, HandleVitaminCollected);

                Vector2 outwardDir = (vitamin.transform.position - transform.position);
                Vector2 dir = outwardDir.sqrMagnitude > 0.001f ? outwardDir.normalized : UnityEngine.Random.insideUnitCircle.normalized;
                vitamin.Launch(dir);
            }

            // Safety fallback: in case a vitamin gets stuck or missed, destroy after timeout
            float safetyTimer = 20f;
            while (_remainingVitamins > 0 && safetyTimer > 0f)
            {
                safetyTimer -= Time.deltaTime;
                yield return null;
            }

            if (_remainingVitamins > 0)
            {
                FinishAndDestroy();
            }
        }

        private void HandleVitaminCollected()
        {
            _remainingVitamins--;
            if (_remainingVitamins <= 0)
            {
                FinishAndDestroy();
            }
        }

        private void FinishAndDestroy()
        {
            // Hide sprite renderer while audio finishes playing
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.enabled = false;

            // If collect audio is playing, give it a moment to finish before destroying the GameObject
            float destroyDelay = (_collectAudioSource != null && _collectAudioSource.isPlaying) ? 0.5f : 0.1f;
            Destroy(gameObject, destroyDelay);
        }

        private void OnDestroy()
        {
            OnDestroyed?.Invoke(this);
        }
    }
}
