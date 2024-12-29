using System.Collections.Generic;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Combat;

namespace _Project.Mechanics.Player
{
    /// <summary>
    /// Binds the player POCO/config and the statically-placed Player-prefab MonoBehaviours (so other
    /// installers/consumers can resolve them via [Inject] instead of a hand-built reference), for
    /// GameplaySceneInstaller. Follows the space-bac/tactics-cards convention: a plain static Install(Container, ...)
    /// helper, not a MonoInstaller of its own.
    /// </summary>
    public static class PlayerInstaller
    {
        private const string BulletPoolRootName = "BulletPool";
        private const int BulletPoolInitialSize = 50;
        private const int BulletPoolMaxSize = 100;

        public static void Install(DiContainer container, PlayerConfig playerConfig, IReadOnlyList<UpgradeDefinition> upgradeDefinitions, Bullet bulletPrefab)
        {
            container.Bind<PlayerConfig>().FromInstance(playerConfig).AsSingle();
            container.Bind<PlayerStatsService>().AsSingle().WithArguments(playerConfig, upgradeDefinitions);

            container.Bind<PlayerMovement>().FromComponentInHierarchy().AsSingle();
            container.Bind<PlayerTurret>().FromComponentInHierarchy().AsSingle();
            container.Bind<PlayerHealth>().FromComponentInHierarchy().AsSingle();

            BindBulletPool(container, bulletPrefab);
        }

        // Zenject-pooled replacement for BulletManager's previous UnityEngine.Pool.ObjectPool<Bullet> - see
        // Bullet's own class doc comment. MaxSize matches the old pool's own k_maxPoolSize.
        private static void BindBulletPool(DiContainer container, Bullet bulletPrefab)
        {
            var poolRoot = new GameObject(BulletPoolRootName).transform;

            container
                .BindFactory<Vector3, Vector3, int, Bullet, Bullet.Factory>()
                .FromMonoPoolableMemoryPool(pool => pool
                    .WithInitialSize(BulletPoolInitialSize)
                    .WithMaxSize(BulletPoolMaxSize)
                    .FromComponentInNewPrefab(bulletPrefab)
                    .UnderTransform(poolRoot));
        }
    }
}
