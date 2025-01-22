using System;
using System.Collections;
using System.Collections.Generic;
using Project.Core.AudioSystem;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Combat;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Micro;

namespace _Project.Mechanics.Pickups
{
    /// <summary>Medical pill pickup. When taking lethal damage: plays explosion animation/audio, disables
    /// collision, and releases child VitaminPickups to fly toward the player, awarding one XP once all are
    /// collected (or a safety timeout elapses). Pooled via a Zenject <see cref="IMemoryPool"/> - each
    /// VitaminPickup child is a permanent, prefab-baked part of this instance rather than separately pooled, so
    /// <see cref="OnSpawned"/> reactivates and repositions them back to their cached original local position
    /// instead of re-instantiating.</summary>
    public class MedicalPickup : MonoBehaviour, IDamageable, IPoolable<Vector3, Quaternion, IMemoryPool>
    {
        [Header("Components")]
        [SerializeField] private Animator _animator;
        [SerializeField] private Collider2D _boxCollider;
        [SerializeField] private SoundData _openSound;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private List<VitaminPickup> _vitamins;

        private PickupsConfig _config;
        private int _health;
        private PlayerStatsService _playerStatsService;
        private IPlayerTargetProvider _targetProvider;
        private IAudioSystem _audioSystem;

        private bool _isBroken;
        private int _remainingVitamins;
        private bool _componentsResolved;
        private readonly List<Vector3> _vitaminLocalPositions = new();
        private IMemoryPool _pool;

        public event Action<MedicalPickup> OnDestroyed;

        [Inject]
        public void Construct(PickupsConfig config, PlayerStatsService playerStatsService, IPlayerTargetProvider targetProvider, IAudioSystem audioSystem)
        {
            _config = config;
            _playerStatsService = playerStatsService;
            _targetProvider = targetProvider;
            _audioSystem = audioSystem;
        }

        public void OnSpawned(Vector3 position, Quaternion rotation, IMemoryPool pool)
        {
            _pool = pool;

            if (!_componentsResolved)
            {
                ResolveComponents();
                CacheVitaminLocalPositions();
                _componentsResolved = true;
            }

            transform.SetPositionAndRotation(position, rotation);

            StopAllCoroutines();
            CancelInvoke();

            _health = _config.MedicalHealth;
            _isBroken = false;
            _remainingVitamins = 0;

            _boxCollider.enabled = true;

            _animator.enabled = true;

            // Resets a respawned pill back to its resting look after Break()'s "Explosion" state.
            _animator.Play("Base Layer.Appearance");

            if (_spriteRenderer != null)
                _spriteRenderer.enabled = true;

            ResetVitamins();
            PropagateDependenciesToVitamins();
        }

        public void OnDespawned()
        {
            _pool = null;
            OnDestroyed?.Invoke(this);
        }

        public void Despawn()
        {
            _pool?.Despawn(this);
        }

        private void ResolveComponents()
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_vitamins.Count == 0)
            {
                _vitamins = new List<VitaminPickup>(GetComponentsInChildren<VitaminPickup>(true));
            }
        }

        /// <summary>Captured once, at real construction - a pooled vitamin's transform.localPosition drifts away
        /// from its prefab-authored slot every time it flies toward the player, so OnSpawned needs the original
        /// value to restore it to (see ResetVitamins).</summary>
        private void CacheVitaminLocalPositions()
        {
            _vitaminLocalPositions.Clear();
            foreach (var vitamin in _vitamins)
                _vitaminLocalPositions.Add(vitamin != null ? vitamin.transform.localPosition : Vector3.zero);
        }

        private void ResetVitamins()
        {
            for (int i = 0; i < _vitamins.Count; i++)
            {
                if (_vitamins[i] != null)
                    _vitamins[i].ResetForPool(_vitaminLocalPositions[i]);
            }
        }

        private void PropagateDependenciesToVitamins()
        {
            if (_vitamins == null)
                return;

            foreach (var vitamin in _vitamins)
            {
                if (vitamin != null)
                    vitamin.Construct(_config, _targetProvider, HandleVitaminCollected);
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

            _animator.Play("Base Layer.Explosion");
            _audioSystem.PlaySfx(_openSound);
            _boxCollider.enabled = false;

            StartCoroutine(ReleaseVitaminsRoutine());
        }

        private IEnumerator ReleaseVitaminsRoutine()
        {
            if (_config.BreakDelaySeconds > 0f)
                yield return new WaitForSeconds(_config.BreakDelaySeconds);

            _animator.enabled = false;

            // Refresh list in case children changed
            if (_vitamins.Count == 0)
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

                vitamin.Construct(_config, _targetProvider, HandleVitaminCollected);

                Vector2 outwardDir = (vitamin.transform.position - transform.position);
                Vector2 dir = outwardDir.sqrMagnitude > 0.001f ? outwardDir.normalized : UnityEngine.Random.insideUnitCircle.normalized;
                vitamin.Launch(dir);
            }

            // Safety fallback: in case a vitamin gets stuck or missed, destroy after timeout
            float safetyTimer = _config.SafetyTimeoutSeconds;
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
            _playerStatsService.AddExperience();

            // Hide sprite renderer while audio finishes playing
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = false;

            Invoke(nameof(Despawn), _config.DestroyDelayWithAudioSeconds);
        }

        public class Factory : PlaceholderFactory<Vector3, Quaternion, MedicalPickup>
        {
        }
    }
}
