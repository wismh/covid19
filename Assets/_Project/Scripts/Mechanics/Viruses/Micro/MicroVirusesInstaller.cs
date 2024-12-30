using System.Collections.Generic;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Combat;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>
    /// Binds the pooled in-cell virus prefabs (Orange/Cucumber/Spiner - one Zenject <see cref="IMemoryPool"/>
    /// per <see cref="MicroVirusVariant"/>, keyed the same way ParticleFxPoolsInstaller&lt;TId&gt; keys its own
    /// multi-prefab pools) plus the Cucumber-only VirusBullet pool, for GameplaySceneInstaller. Both replace bare
    /// Instantiate/Destroy - see MicroVirusController/MicroVirusSpawner/VirusBullet/VirusBulletSpawner's own doc
    /// comments.
    /// </summary>
    public static class MicroVirusesInstaller
    {
        private const string PoolRootName = "MicroVirusPool";
        private const int VirusInitialSize = 10;
        private const int VirusMaxSize = 30;
        private const int VirusBulletInitialSize = 10;
        private const int VirusBulletMaxSize = 30;

        public static void Install(
            DiContainer container,
            MicroVirusController orangePrefab,
            MicroVirusController cucumberPrefab,
            MicroVirusController spinerPrefab,
            VirusBullet virusBulletPrefab)
        {
            var poolRoot = new GameObject(PoolRootName).transform;

            BindVirusPool(container, MicroVirusVariant.Orange, orangePrefab, poolRoot);
            BindVirusPool(container, MicroVirusVariant.Cucumber, cucumberPrefab, poolRoot);
            BindVirusPool(container, MicroVirusVariant.Spiner, spinerPrefab, poolRoot);

            container
                .Bind<IReadOnlyDictionary<MicroVirusVariant, IFactory<MicroVirusController>>>()
                .FromMethod(CreateFactoryMap)
                .AsSingle();

            BindVirusBulletPool(container, virusBulletPrefab, poolRoot);
        }

        private static void BindVirusPool(DiContainer container, MicroVirusVariant variant, MicroVirusController prefab, Transform poolRoot)
        {
            if (prefab == null)
                return;

            container
                .BindFactory<MicroVirusController, MicroVirusController.Factory>()
                .WithId(variant)
                .FromMonoPoolableMemoryPool(pool => pool
                    .WithInitialSize(VirusInitialSize)
                    .WithMaxSize(VirusMaxSize)
                    .FromComponentInNewPrefab(prefab)
                    .UnderTransform(poolRoot));
        }

        private static IReadOnlyDictionary<MicroVirusVariant, IFactory<MicroVirusController>> CreateFactoryMap(InjectContext context)
        {
            var factories = new Dictionary<MicroVirusVariant, IFactory<MicroVirusController>>();

            foreach (MicroVirusVariant variant in new[] { MicroVirusVariant.Orange, MicroVirusVariant.Cucumber, MicroVirusVariant.Spiner })
            {
                if (context.Container.HasBindingId<MicroVirusController.Factory>(variant))
                    factories[variant] = context.Container.ResolveId<MicroVirusController.Factory>(variant);
            }

            return factories;
        }

        // Bound here (not PlayerInstaller/CombatInstaller) since VirusBullet is exclusively fired by the Cucumber
        // micro-virus behavior - see VirusBullet/VirusBulletSpawner's own class doc comments.
        private static void BindVirusBulletPool(DiContainer container, VirusBullet prefab, Transform poolRoot)
        {
            if (prefab == null)
                return;

            container
                .BindFactory<Vector3, Vector3, Transform, VirusBullet, VirusBullet.Factory>()
                .FromMonoPoolableMemoryPool(pool => pool
                    .WithInitialSize(VirusBulletInitialSize)
                    .WithMaxSize(VirusBulletMaxSize)
                    .FromComponentInNewPrefab(prefab)
                    .UnderTransform(poolRoot));
        }
    }
}
