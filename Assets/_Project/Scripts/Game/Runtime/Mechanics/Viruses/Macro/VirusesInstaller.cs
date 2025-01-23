using System.Collections.Generic;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Waves;

namespace _Project.Mechanics.Viruses.Macro
{
    public class VirusesInstaller : Installer<MacroVirusConfig, WaveScalingConfig, MacroVirusController, MacroVirusController, VirusesInstaller>
    {
        private const string PoolRootName = "MacroVirusPool";
        private const int VirusInitialSize = 10;
        private const int VirusMaxSize = 40;

        private readonly MacroVirusConfig _macroVirusConfig;
        private readonly WaveScalingConfig _waveScalingConfig;
        private readonly MacroVirusController _virusPrefab;
        private readonly MacroVirusController _glistPrefab;

        public VirusesInstaller(MacroVirusConfig macroVirusConfig, WaveScalingConfig waveScalingConfig, MacroVirusController virusPrefab, MacroVirusController glistPrefab)
        {
            _macroVirusConfig = macroVirusConfig;
            _waveScalingConfig = waveScalingConfig;
            _virusPrefab = virusPrefab;
            _glistPrefab = glistPrefab;
        }

        public override void InstallBindings()
        {
            Container.Bind<MacroVirusConfig>().FromInstance(_macroVirusConfig).AsSingle();
            Container.Bind<WaveScalingConfig>().FromInstance(_waveScalingConfig).AsSingle();
            Container.Bind<MacroVirusRegistry>().AsSingle();
            Container.Bind<MacroVirusSpawner>().FromComponentInHierarchy().AsSingle();

            BindVirusPools();
        }

        private void BindVirusPools()
        {
            var poolRoot = new GameObject(PoolRootName).transform;

            BindVirusPool(MacroVirusVariant.Virus, _virusPrefab, poolRoot);
            BindVirusPool(MacroVirusVariant.Glist, _glistPrefab, poolRoot);

            Container
                .Bind<IReadOnlyDictionary<MacroVirusVariant, IFactory<Vector3, Quaternion, MacroVirusController>>>()
                .FromMethod(CreateFactoryMap)
                .AsSingle();
        }

        private void BindVirusPool(MacroVirusVariant variant, MacroVirusController prefab, Transform poolRoot)
        {
            Container
                .BindFactory<Vector3, Quaternion, MacroVirusController, MacroVirusController.Factory>()
                .WithId(variant)
                .FromMonoPoolableMemoryPool(pool => pool
                    .WithInitialSize(VirusInitialSize)
                    .WithMaxSize(VirusMaxSize)
                    .FromComponentInNewPrefab(prefab)
                    .UnderTransform(poolRoot));
        }

        private static IReadOnlyDictionary<MacroVirusVariant, IFactory<Vector3, Quaternion, MacroVirusController>> CreateFactoryMap(InjectContext context)
        {
            var factories = new Dictionary<MacroVirusVariant, IFactory<Vector3, Quaternion, MacroVirusController>>();

            foreach (MacroVirusVariant variant in new[] { MacroVirusVariant.Virus, MacroVirusVariant.Glist })
            {
                if (context.Container.HasBindingId<MacroVirusController.Factory>(variant))
                    factories[variant] = context.Container.ResolveId<MacroVirusController.Factory>(variant);
            }

            return factories;
        }
    }
}
