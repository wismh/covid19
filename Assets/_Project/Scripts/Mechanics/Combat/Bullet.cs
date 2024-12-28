using System;
using UnityEngine;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Combat
{
    /// <summary>
    /// Player bullet projectile.
    /// Replaces legacy GMTK.Bullet (Assets/GMTK/Scripts/Behaviors/Bullet/Bullet.cs).
    /// Handles flight, damage application to GMTK.IDamageable, lifetime tracking, and pool return.
    /// Supports dynamic world scale (Macro 1x scale/speed vs Micro WorldScaleConfig.MicroPlayerScale-x speed).
    /// </summary>
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private float _speed = 10f;
        [SerializeField] private float _bulletLifetime = 2f;
        [SerializeField] private int _damage = 1;

        private Action<Bullet> _returnToPool;
        private Func<bool> _isMicroWorld;
        private WorldScaleConfig _worldScaleConfig;
        private float _timeRemaining;
        private int _currentDamage;
        private bool _isReturned;

        public void Init(Action<Bullet> returnToPool, Func<bool> isMicroWorld, WorldScaleConfig worldScaleConfig, float speed, float bulletLifetime)
        {
            _returnToPool = returnToPool;
            _isMicroWorld = isMicroWorld;
            _worldScaleConfig = worldScaleConfig;
            _speed = speed;
            _bulletLifetime = bulletLifetime;
        }

        public void Fire(Vector3 position, Vector3 direction, int damage = -1)
        {
            _isReturned = false;
            transform.position = position;
            transform.right = direction;
            _currentDamage = damage > 0 ? damage : _damage;
            _timeRemaining = _bulletLifetime;

            UpdateScaleAndVelocity(0f);
            gameObject.SetActive(true);
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
            bool isMicro = _isMicroWorld != null && _isMicroWorld();
            Vector3 microScale = _worldScaleConfig != null ? _worldScaleConfig.MicroPlayerScale : new Vector3(0.3f, 0.3f, 1f);
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
            if (_returnToPool != null)
            {
                _returnToPool.Invoke(this);
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
    }
}
