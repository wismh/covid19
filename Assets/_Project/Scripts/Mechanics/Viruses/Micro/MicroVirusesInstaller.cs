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
    ///
    /// A real Zenject <see cref="Installer{TParam1, TParam2, TParam3, TParam4, TDerived}"/> subclass (not a bare
    /// static class) - matches tactics-cards' own installers, which are all genuine Installer&lt;...&gt; types
    /// with constructor-injected params, called via the base class's inherited static `Install(Container, ...)`
    /// factory method.
    /// </summary>
    public class MicroVirusesInstaller : Installer<MicroVirusController, MicroVirusController, MicroVirusController, VirusBullet, MicroVirusesInstaller>
    {
        private const string PoolRootName = "MicroVirusPool";
        private const int VirusInitialSize = 10;
        private const int VirusMaxSize = 30;
        private const int VirusBulletInitialSize = 10;
        private const int VirusBulletMaxSize = 30;

        private readonly MicroVirusController _orangePrefab;
        private readonly MicroVirusController _cucumberPrefab;
        private readonly MicroVirusController _spinerPrefab;
        private readonly VirusBullet _virusBulletPrefab;

        public MicroVirusesInstaller(
            MicroVirusController orangePrefab,
            MicroVirusController cucumberPrefab,
            MicroVirusController spinerPrefab,
            VirusBullet virusBulletPrefab)
        {
            _orangePrefab = orangePrefab;
            _cucumberPrefab = cucumberPrefab;
            _spinerPrefab = spinerPrefab;
            _virusBulletPrefab = virusBulletPrefab;
        }

        public override void InstallBindings()
        {
            var poolRoot = new GameObject(PoolRootName).transform;

            BindVirusPool(MicroVirusVariant.Orange, _orangePrefab, poolRoot);
            BindVirusPool(MicroVirusVariant.Cucumber, _cucumberPrefab, poolRoot);
            BindVirusPool(MicroVirusVariant.Spiner, _spinerPrefab, poolRoot);

            Container
                .Bind<IReadOnlyDictionary<MicroVirusVariant, IFactory<MicroVirusController>>>()
                .FromMethod(CreateFactoryMap)
                .AsSingle();

            BindVirusBulletPool(_virusBulletPrefab, poolRoot);
        }

        private void BindVirusPool(MicroVirusVariant variant, MicroVirusController prefab, Transform poolRoot)
        {
            if (prefab == null)
                return;

            Container
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
        private void BindVirusBulletPool(VirusBullet prefab, Transform poolRoot)
        {
            if (prefab == null)
                return;

            Container
                .BindFactory<Vector3, Vector3, Transform, VirusBullet, VirusBullet.Factory>()
                .FromMonoPoolableMemoryPool(pool => pool
                    .WithInitialSize(VirusBulletInitialSize)
                    .WithMaxSize(VirusBulletMaxSize)
                    .FromComponentInNewPrefab(prefab)
                    .UnderTransform(poolRoot));
        }
    }
}
