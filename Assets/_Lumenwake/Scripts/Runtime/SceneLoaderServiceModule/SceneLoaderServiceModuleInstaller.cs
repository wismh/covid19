using Project.Generated;
using Zenject;

namespace Project.Core.SceneLoaderServiceModule
{
    public class SceneLoaderServiceModuleInstaller : Installer<SceneLoaderServiceModuleInstaller> 
    {
        public override void InstallBindings() 
        {
            Container.Bind<ISceneLoaderService>()
                .FromInstance(new SceneLoaderServiceFacade(
                    new BuildInSceneLoaderService(SceneInBuild.BootstrapScene),
                    new AddressablesSceneLoaderService()))
                .AsSingle();

            Container.Bind<IGameplayLevelSceneService>()
                .To<GameplayLevelSceneService>()
                .AsSingle();
        }
    }
}