using Zenject;
using _Project.Mechanics.Waves;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>
    /// Binds the macro-virus registry/config for GameplaySceneInstaller. WaveScalingConfig is bound here too -
    /// it originates from the macro-virus wave scaling, but CellSpawner (Mechanics/Cells) needs the same
    /// instance, which is fine: bindings are just container declarations, order between installers doesn't matter.
    /// </summary>
    public static class VirusesInstaller
    {
        public static void Install(DiContainer container, MacroVirusConfig macroVirusConfig, WaveScalingConfig waveScalingConfig)
        {
            container.Bind<MacroVirusConfig>().FromInstance(macroVirusConfig).AsSingle();
            container.Bind<WaveScalingConfig>().FromInstance(waveScalingConfig).AsSingle();
            container.Bind<MacroVirusRegistry>().AsSingle();
        }
    }
}
