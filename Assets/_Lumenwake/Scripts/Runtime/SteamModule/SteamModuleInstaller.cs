using Zenject;

namespace Project.Core.SteamModule
{
    public sealed class SteamModuleInstaller : Installer<SteamModuleInstaller>
    {
        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<SteamService>()
                .AsSingle()
                .NonLazy();
        }
    }
}
