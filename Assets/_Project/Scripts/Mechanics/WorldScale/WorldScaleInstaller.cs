using Zenject;

namespace _Project.Mechanics.WorldScale
{
    /// <summary>
    /// Binds the world-scale (Macro/Micro) config and controller for GameplaySceneInstaller. A real Zenject
    /// <see cref="Installer{TParam1, TDerived}"/> subclass (not a bare static class) - matches tactics-cards'
    /// own installers (e.g. PlaceablePoolsInstaller/ArenaMatchBindingsInstaller), which are all genuine
    /// Installer&lt;...&gt; types with constructor-injected params, called via the base class's inherited static
    /// `Install(Container, ...)` factory method - not a hand-rolled one.
    /// </summary>
    public class WorldScaleInstaller : Installer<WorldScaleConfig, WorldScaleInstaller>
    {
        private readonly WorldScaleConfig _worldScaleConfig;

        public WorldScaleInstaller(WorldScaleConfig worldScaleConfig)
        {
            _worldScaleConfig = worldScaleConfig;
        }

        public override void InstallBindings()
        {
            Container.Bind<WorldScaleConfig>().FromInstance(_worldScaleConfig).AsSingle();
            Container.Bind<IWorldScaleTransition>().To<WorldScaleTransition>().AsSingle();
            Container.Bind<WorldScaleController>().AsSingle();
        }
    }
}
