using Project.Core.AudioSystem;
using Zenject;

namespace _Project.Mechanics.Composition
{
    public class GameplayEventWiringInstaller : Installer<SoundData, SoundData, GameplayEventWiringInstaller>
    {
        private readonly SoundData _enterMicroWhooshSound;
        private readonly SoundData _exitMicroWhooshSound;

        public GameplayEventWiringInstaller(SoundData enterMicroWhooshSound, SoundData exitMicroWhooshSound)
        {
            _enterMicroWhooshSound = enterMicroWhooshSound;
            _exitMicroWhooshSound = exitMicroWhooshSound;
        }

        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<MacroVirusReplacementDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<MacroVirusPauseDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<VirusExperienceDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<HudStatisticsDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<GameOverDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<WorldScaleWhooshSfxDriver>().AsSingle()
                .WithArguments(_enterMicroWhooshSound, _exitMicroWhooshSound);
        }
    }
}
