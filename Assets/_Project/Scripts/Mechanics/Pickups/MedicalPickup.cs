using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Combat;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Micro;

namespace _Project.Mechanics.Pickups
{
    /// <summary>
    /// Stage 4: Medical pill pickup component. Replaces legacy GMTK.Medical (Assets/GMTK/Scripts/Behaviors/Medicals/Medical.cs).
    /// Implements _Project.Mechanics.Combat.IDamageable. When taking lethal damage:
    /// plays explosion animation and audio, disables collision, and releases child VitaminPickups to fly towards the player.
    /// Awards one XP via PlayerStatsService when the pill finishes (all vitamins collected, or the
    /// safety-timeout fallback), matching the original GMTK.Medical's "+1 XP per pill" behavior.
    /// Passes player target dependencies down to vitamins.
    ///
    /// --- Pooling migration ---
    /// Now spawned via a Zenject <see cref="IMemoryPool"/> (bound by PickupsInstaller) instead of bare
    /// Instantiate/Destroy - see <see cref="OnSpawned"/>. The trickiest part: each <see cref="VitaminPickup"/>
    /// child is a permanent, prefab-baked part of THIS instance (not separately spawned/pooled), and used to
    /// destroy itself on collection - which would permanently empty that slot the next time this same pooled
    /// instance respawns. VitaminPickup.Collect() now deactivates instead (see its own doc comment), and
    /// OnSpawned reactivates + repositions every vitamin back to its original, cached local position via
    /// VitaminPickup.ResetForPool. <see cref="Construct"/> is now real `[Inject]`-tagged DI (called once, at this
    /// pooled instance's one-time real construction) - the old second, <c>Transform playerTransform</c> overload
    /// was a manual/test fallback per its own prior doc comment, now dead now that IPlayerTargetProvider is
    /// always available from the container, so it (and the <c>_playerTransform</c> field) is removed.
    /// </summary>
    public class MedicalPickup : MonoBehaviour, IDamageable, IPoolable<Vector3, Quaternion, IMemoryPool>
    {
        [Header("Components")]
        [SerializeField] private Animator _animator;
        [SerializeField] private Collider2D _boxCollider;
        [SerializeField] private AudioSource _openAudioSource;
        [SerializeField] private AudioSource _collectAudioSource;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private List<VitaminPickup> _vitamins;

        // Health/timing tunables now live on PickupsConfig instead of local [SerializeField]s.
        private PickupsConfig _config;
        private int _health;
        private PlayerStatsService _playerStatsService;
        private IPlayerTargetProvider _targetProvider;

        private bool _isBroken;
        private int _remainingVitamins;
        private bool _componentsResolved;
        private readonly List<Vector3> _vitaminLocalPositions = new();
        private IMemoryPool _pool;

        public event Action<MedicalPickup> OnDestroyed;

        [Inject]
        public void Construct(PickupsConfig config, PlayerStatsService playerStatsService, IPlayerTargetProvider targetProvider)
        {
            _config = config;
            _playerStatsService = playerStatsService;
            _targetProvider = targetProvider;
        }

        /// <summary>Runs on every (re)spawn from the pool, not just the first one - see class doc comment's
        /// "Pooling migration" section.</summary>
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

            // "Appearance" is this controller's default/idle state (confirmed via its serialized
            // m_DefaultState) - resets a respawned pill back to its resting look after Break()'s "Explosion".
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

        /// <summary>Replaces <c>Destroy(gameObject)</c> for this pooled type - see MedicalSpawner.SpawnMedical/HandleMedicalDestroyed.</summary>
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
                    vitamin.Construct(_config, _targetProvider, _collectAudioSource, null, HandleVitaminCollected);
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
            _openAudioSource.Play();
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

                vitamin.Construct(_config, _targetProvider, _collectAudioSource, null, HandleVitaminCollected);

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

            // If collect audio is playing, give it a moment to finish before despawning back into the pool.
            // Invoke, not Destroy(gameObject, delay) - see class doc comment's "Pooling migration" section; a
            // real Destroy would permanently remove this pooled instance instead of recycling it.
            float destroyDelay = _collectAudioSource.isPlaying
                ? _config.DestroyDelayWithAudioSeconds
                : _config.DestroyDelayWithoutAudioSeconds;
            Invoke(nameof(Despawn), destroyDelay);
        }

        public class Factory : PlaceholderFactory<Vector3, Quaternion, MedicalPickup>
        {
        }
    }
}
