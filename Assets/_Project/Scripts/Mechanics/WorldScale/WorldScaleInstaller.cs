using Zenject;

namespace _Project.Mechanics.WorldScale
{
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
