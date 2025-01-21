using UnityEngine;
using Zenject;

namespace _Project.Mechanics.Composition
{
    public class GameplayEventWiringInstaller : Installer<AudioClip, AudioClip, GameplayEventWiringInstaller>
    {
        private readonly AudioClip _enterMicroWhooshClip;
        private readonly AudioClip _exitMicroWhooshClip;

        public GameplayEventWiringInstaller(AudioClip enterMicroWhooshClip, AudioClip exitMicroWhooshClip)
        {
            _enterMicroWhooshClip = enterMicroWhooshClip;
            _exitMicroWhooshClip = exitMicroWhooshClip;
        }

        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<MacroVirusReplacementDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<MacroVirusPauseDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<VirusExperienceDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<HudStatisticsDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<GameOverDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<WorldScaleWhooshSfxDriver>().AsSingle()
                .WithArguments(_enterMicroWhooshClip, _exitMicroWhooshClip);
        }
    }
}
