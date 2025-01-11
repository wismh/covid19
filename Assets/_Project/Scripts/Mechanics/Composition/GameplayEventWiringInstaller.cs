using Zenject;

namespace _Project.Mechanics.Composition
{
    public class GameplayEventWiringInstaller : Installer<GameplayEventWiringInstaller>
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<MacroVirusReplacementDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<MacroVirusPauseDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<VirusExperienceDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<HudStatisticsDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<GameOverDriver>().AsSingle();
        }
    }
}
