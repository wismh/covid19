using Zenject;

namespace Project.Core.AudioSystem
{
    public sealed class AudioSystemInstaller : Installer<AudioSystemInstaller>
    {
        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<AudioSystem>()
                .FromComponentInHierarchy()
                .AsSingle();
        }
    }
}
