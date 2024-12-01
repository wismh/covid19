using _Project.Mechanics.AppFlow;
using _Project.Save;
using Project.Core.ApplicationLifecycle;
using Project.Core.AssetLoaderModule;
using Project.Core.AudioSystem;
using Project.Core.SceneLoaderServiceModule;
using UnityEngine;
using Zenject;

namespace _Project.Bootstrap
{
    [CreateAssetMenu(
        menuName = "Configurations/Bootstrap/" + nameof(ProjectContextInstaller),
        fileName = nameof(ProjectContextInstaller) + "_Default")]
    public sealed class ProjectContextInstaller : ScriptableObjectInstaller<ProjectContextInstaller>
    {
        public override void InstallBindings()
        {
            SceneLoaderServiceModuleInstaller.Install(Container);
            AssetLoaderInstaller.Install(Container);
            AudioSystemInstaller.Install(Container);
            GameSaveModuleInstaller.Install(Container);

            Container.Bind<MenuState>().AsSingle();
            Container.Bind<GameplayState>().AsSingle();
            Container.Bind<GlobalGameStateMachine>().AsSingle();

            Container.Bind<LifeController>().FromComponentInHierarchy().AsSingle();
        }
    }
}
