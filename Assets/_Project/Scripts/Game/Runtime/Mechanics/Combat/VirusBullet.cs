using UnityEngine;
using Zenject;

namespace _Project.Mechanics.Combat
{
    /// <summary>Enemy virus projectile, fired by CucumberVirus. Inflicts damage on any <see cref="IDamageable"/>
    /// (typically PlayerHealth). Pooled via a Zenject <see cref="IMemoryPool"/>.</summary>
    public class VirusBullet : MonoBehaviour, IPoolable<Vector3, Vector3, Transform, IMemoryPool>
    {
        [SerializeField] private float _speed = 4f;
        [SerializeField] private float _bulletLifetime = 5f;
        [SerializeField] private int _damage = 1;

        private IMemoryPool _pool;
        private float _timeRemaining;
        private bool _isReturned;

        /// <summary>Overrides the prefab-default speed/lifetime/damage with CucumberVirusConfig's values. Left
        /// uncalled, the bullet still works off its own [SerializeField] defaults.</summary>
        public void Construct(float speed, float bulletLifetime, int damage)
        {
            _speed = speed;
            _bulletLifetime = bulletLifetime;
            _damage = damage;
        }

        public void OnSpawned(Vector3 position, Vector3 direction, Transform scaleReference, IMemoryPool pool)
        {
            _pool = pool;
            _isReturned = false;

            if (scaleReference != null)
                transform.SetParent(scaleReference, worldPositionStays: false);

            transform.position = position;
            transform.right = direction;
            _timeRemaining = _bulletLifetime;

            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            _pool = null;
            _isReturned = true;
        }

        private void Update()
        {
            if (_isReturned)
                return;

            _timeRemaining -= Time.deltaTime;
            if (_timeRemaining <= 0f)
            {
                ReturnToPool();
                return;
            }

            Vector3 velocity = Time.deltaTime * _speed * transform.right;
            transform.position += velocity;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isReturned)
                return;

            if (other.gameObject.TryGetComponent(out IDamageable damageable))
            {
                damageable.Damage(_damage, transform.right);
                ReturnToPool();
            }
        }

        private void ReturnToPool()
        {
            if (_isReturned)
                return;

            _isReturned = true;
            if (_pool != null)
                _pool.Despawn(this);
            else
                gameObject.SetActive(false);
        }

        public class Factory : PlaceholderFactory<Vector3, Vector3, Transform, VirusBullet>
        {
        }
    }
}
