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
    /// Also binds the scene-placed MacroVirusSpawner itself (mirrors PlayerInstaller's PlayerMovement/
    /// PlayerTurret/PlayerHealth bindings) so GameplayEventWiringInstaller's cell/virus drivers can resolve it
    /// via constructor injection instead of a hand-built reference.
    ///
    /// A real Zenject <see cref="Installer{TParam1, TParam2, TParam3, TParam4, TDerived}"/> subclass (not a bare
    /// static class) - matches tactics-cards' own installers, which are all genuine Installer&lt;...&gt; types
    /// with constructor-injected params, called via the base class's inherited static `Install(Container, ...)`
    /// factory method.
    /// </summary>
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
            if (prefab == null)
                return;

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
