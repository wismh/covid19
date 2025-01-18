using UnityEngine;
using Zenject;
using _Project.Mechanics.Player;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Combat
{
    /// <summary>Player bullet projectile: flight, damage application, lifetime, and pool return. Scales speed
    /// and visual size for the Micro world. Pooled via a Zenject <see cref="IMemoryPool"/> -
    /// <see cref="Construct"/> resolves container singletons once at pool creation, while per-spawn data
    /// (position/direction/damage) goes through <see cref="OnSpawned"/> instead.</summary>
    public class Bullet : MonoBehaviour, IPoolable<Vector3, Vector3, int, IMemoryPool>
    {
        [SerializeField] private float _speed = 10f;
        [SerializeField] private float _bulletLifetime = 2f;
        [SerializeField] private int _damage = 1;

        private WorldScaleConfig _worldScaleConfig;
        private WorldScaleController _worldScaleController;
        private IMemoryPool _pool;
        private float _timeRemaining;
        private int _currentDamage;
        private bool _isReturned;

        [Inject]
        public void Construct(PlayerConfig playerConfig, WorldScaleConfig worldScaleConfig, WorldScaleController worldScaleController)
        {
            _worldScaleConfig = worldScaleConfig;
            _worldScaleController = worldScaleController;
            _speed = playerConfig.BulletSpeed;
            _bulletLifetime = playerConfig.BulletLifetimeSeconds;
        }

        private bool IsMicroWorld()
        {
            return _worldScaleController.CurrentScale == WorldScale.WorldScale.Micro;
        }

        public void OnSpawned(Vector3 position, Vector3 direction, int damage, IMemoryPool pool)
        {
            _pool = pool;

            _isReturned = false;
            transform.position = position;
            transform.right = direction;
            _currentDamage = damage > 0 ? damage : _damage;
            _timeRemaining = _bulletLifetime;

            UpdateScaleAndVelocity(0f);
            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            _pool = null;
            _isReturned = true;
            gameObject.SetActive(false);
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

            UpdateScaleAndVelocity(Time.deltaTime);
        }

        private void UpdateScaleAndVelocity(float deltaTime)
        {
            bool isMicro = IsMicroWorld();
            Vector3 microScale = _worldScaleConfig.MicroPlayerScale;
            Vector3 targetScale = isMicro ? microScale : Vector3.one;
            if (transform.localScale != targetScale)
            {
                transform.localScale = targetScale;
            }

            if (deltaTime > 0f)
            {
                Vector3 velocity = deltaTime * _speed * transform.right;
                if (isMicro)
                {
                    velocity *= microScale.x;
                }

                transform.position += velocity;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isReturned)
                return;

            if (other.gameObject.TryGetComponent(out IDamageable damageable))
            {
                damageable.Damage(_currentDamage, transform.right);
                ReturnToPool();
            }
        }

        private void ReturnToPool()
        {
            if (_isReturned)
                return;

            _isReturned = true;
            if (_pool != null)
            {
                _pool.Despawn(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            _isReturned = true;
        }

        public class Factory : PlaceholderFactory<Vector3, Vector3, int, Bullet>
        {
        }
    }
}
