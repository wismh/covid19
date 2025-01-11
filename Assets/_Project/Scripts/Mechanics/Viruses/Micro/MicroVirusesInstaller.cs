using System.Collections.Generic;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Combat;

namespace _Project.Mechanics.Viruses.Micro
{
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
