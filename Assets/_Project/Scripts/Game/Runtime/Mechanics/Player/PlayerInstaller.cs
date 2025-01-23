using System.Collections.Generic;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Combat;

namespace _Project.Mechanics.Player
{
    public class PlayerInstaller : Installer<PlayerConfig, IReadOnlyList<UpgradeDefinition>, Bullet, PlayerInstaller>
    {
        private const string BulletPoolRootName = "BulletPool";
        private const int BulletPoolInitialSize = 50;
        private const int BulletPoolMaxSize = 100;

        private readonly PlayerConfig _playerConfig;
        private readonly IReadOnlyList<UpgradeDefinition> _upgradeDefinitions;
        private readonly Bullet _bulletPrefab;

        public PlayerInstaller(PlayerConfig playerConfig, IReadOnlyList<UpgradeDefinition> upgradeDefinitions, Bullet bulletPrefab)
        {
            _playerConfig = playerConfig;
            _upgradeDefinitions = upgradeDefinitions;
            _bulletPrefab = bulletPrefab;
        }

        public override void InstallBindings()
        {
            Container.Bind<PlayerConfig>().FromInstance(_playerConfig).AsSingle();
            Container.Bind<PlayerStatsService>().AsSingle().WithArguments(_playerConfig, _upgradeDefinitions);

            Container.Bind<PlayerMovement>().FromComponentInHierarchy().AsSingle();
            Container.Bind<PlayerTurret>().FromComponentInHierarchy().AsSingle();
            Container.Bind<PlayerHealth>().FromComponentInHierarchy().AsSingle();

            BindBulletPool(_bulletPrefab);
        }

        private void BindBulletPool(Bullet bulletPrefab)
        {
            var poolRoot = new GameObject(BulletPoolRootName).transform;

            Container
                .BindFactory<Vector3, Vector3, int, Bullet, Bullet.Factory>()
                .FromMonoPoolableMemoryPool(pool => pool
                    .WithInitialSize(BulletPoolInitialSize)
                    .WithMaxSize(BulletPoolMaxSize)
                    .FromComponentInNewPrefab(bulletPrefab)
                    .UnderTransform(poolRoot));
        }
    }
}
