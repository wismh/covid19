using Zenject;

namespace _Project.Mechanics.WorldScale
{
    /// <summary>Binds the world-scale (Macro/Micro) config and controller for GameplaySceneInstaller.</summary>
    public static class WorldScaleInstaller
    {
        public static void Install(DiContainer container, WorldScaleConfig worldScaleConfig)
        {
            container.Bind<WorldScaleConfig>().FromInstance(worldScaleConfig).AsSingle();
            container.Bind<IWorldScaleTransition>().To<WorldScaleTransition>().AsSingle();
            container.Bind<WorldScaleController>().AsSingle();
        }
    }
}
