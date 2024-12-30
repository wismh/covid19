using System.Collections.Generic;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Waves;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>
    /// Binds the macro-virus registry/config for GameplaySceneInstaller. WaveScalingConfig is bound here too -
    /// it originates from the macro-virus wave scaling, but CellSpawner (Mechanics/Cells) needs the same
    /// instance, which is fine: bindings are just container declarations, order between installers doesn't matter.
    /// Also binds the pooled Virus/Glist prefabs (one Zenject <see cref="IMemoryPool"/> per
    /// <see cref="MacroVirusVariant"/>, mirroring MicroVirusesInstaller's own per-variant pool shape) instead of
    /// the previous bare Instantiate/Destroy - see MacroVirusController/MacroVirusSpawner's own doc comments.
    /// </summary>
    public static class VirusesInstaller
    {
        private const string PoolRootName = "MacroVirusPool";
        private const int VirusInitialSize = 10;
        private const int VirusMaxSize = 40;

        public static void Install(DiContainer container, MacroVirusConfig macroVirusConfig, WaveScalingConfig waveScalingConfig, MacroVirusController virusPrefab, MacroVirusController glistPrefab)
        {
            container.Bind<MacroVirusConfig>().FromInstance(macroVirusConfig).AsSingle();
            container.Bind<WaveScalingConfig>().FromInstance(waveScalingConfig).AsSingle();
            container.Bind<MacroVirusRegistry>().AsSingle();

            BindVirusPools(container, virusPrefab, glistPrefab);
        }

        private static void BindVirusPools(DiContainer container, MacroVirusController virusPrefab, MacroVirusController glistPrefab)
        {
            var poolRoot = new GameObject(PoolRootName).transform;

            BindVirusPool(container, MacroVirusVariant.Virus, virusPrefab, poolRoot);
            BindVirusPool(container, MacroVirusVariant.Glist, glistPrefab, poolRoot);

            container
                .Bind<IReadOnlyDictionary<MacroVirusVariant, IFactory<Vector3, Quaternion, MacroVirusController>>>()
                .FromMethod(CreateFactoryMap)
                .AsSingle();
        }

        private static void BindVirusPool(DiContainer container, MacroVirusVariant variant, MacroVirusController prefab, Transform poolRoot)
        {
            if (prefab == null)
                return;

            container
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
