using System;
using UnityEngine;
using UnityEngine.Pool;
using _Project.Mechanics.Player;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Combat
{
    /// <summary>
    /// Manages the player's bullet pool.
    /// Replaces legacy GMTK.BulletManager (Assets/GMTK/Scripts/Behaviors/Bullet/BulletManager.cs).
    /// Implements IProjectileSpawner for player firing, pulling effective damage from PlayerStatsService
    /// and world scale from WorldScaleController.
    /// </summary>
    public class BulletManager : MonoBehaviour, IProjectileSpawner
    {
        private const int k_defaultPoolCapacity = 50;
        private const int k_maxPoolSize = 100;

        [SerializeField] private Bullet _bulletPrefab;

        private IObjectPool<Bullet> _bulletPool;
        private PlayerConfig _playerConfig;
        private PlayerStatsService _playerStats;
        private WorldScaleConfig _worldScaleConfig;
        private WorldScaleController _worldScaleController;

        public void Construct(PlayerConfig playerConfig, PlayerStatsService playerStats, WorldScaleConfig worldScaleConfig, WorldScaleController worldScaleController)
        {
            _playerConfig = playerConfig;
            _playerStats = playerStats;
            _worldScaleConfig = worldScaleConfig;
            _worldScaleController = worldScaleController;
        }

        private void Awake()
        {
            InitializePool();
        }

        private void InitializePool()
        {
            if (_bulletPool != null)
                return;

            _bulletPool = new ObjectPool<Bullet>(
                createFunc: CreateBullet,
                actionOnGet: bullet => { },
                actionOnRelease: bullet => bullet.gameObject.SetActive(false),
                actionOnDestroy: bullet =>
                {
                    if (bullet != null)
                        Destroy(bullet.gameObject);
                },
                collectionCheck: false,
                defaultCapacity: k_defaultPoolCapacity,
                maxSize: k_maxPoolSize
            );
        }

        private Bullet CreateBullet()
        {
            Bullet bullet = Instantiate(_bulletPrefab, transform);
            bullet.Init(DestroyBullet, IsMicroWorld, _worldScaleConfig, _playerConfig.BulletSpeed, _playerConfig.BulletLifetimeSeconds);
            bullet.gameObject.SetActive(false);
            return bullet;
        }

        private bool IsMicroWorld()
        {
            return _worldScaleController != null && _worldScaleController.CurrentScale == WorldScale.WorldScale.Micro;
        }

        public void SpawnProjectile(Vector3 position, Vector3 direction)
        {
            Fire(position, direction);
        }

        public void SpawnBullet(Vector3 position, Vector3 direction)
        {
            Fire(position, direction);
        }

        public void Fire(Vector3 position, Vector3 direction)
        {
            if (_bulletPool == null)
                InitializePool();

            Bullet bullet = _bulletPool.Get();
            int damage = _playerStats != null ? _playerStats.EffectiveDamage : 1;
            bullet.Fire(position, direction, damage);
        }

        public void DestroyBullet(Bullet bullet)
        {
            if (_bulletPool != null && bullet != null)
            {
                _bulletPool.Release(bullet);
            }
        }

        public void DestroyBullet(Transform bulletTransform)
        {
            if (bulletTransform != null && bulletTransform.TryGetComponent(out Bullet bullet))
            {
                DestroyBullet(bullet);
            }
        }
    }
}
