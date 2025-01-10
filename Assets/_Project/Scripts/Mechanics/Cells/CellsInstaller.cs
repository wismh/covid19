using Zenject;

namespace _Project.Mechanics.Cells
{
    /// <summary>
    /// Binds the cell registry/config for GameplaySceneInstaller. CellRegistry also implements
    /// ICellTargetProvider (Mechanics/Viruses/Macro/ICellTargetProvider.cs) - BindInterfacesAndSelfTo covers
    /// both from the one singleton instance. Also binds the scene-placed CellSpawner itself (mirrors
    /// PlayerInstaller's PlayerMovement/PlayerTurret/PlayerHealth bindings) so GameplayEventWiringInstaller's
    /// VirusExperienceDriver can resolve it via constructor injection instead of a hand-built reference.
    ///
    /// A real Zenject <see cref="Installer{TParam1, TDerived}"/> subclass (not a bare static class) - matches
    /// tactics-cards' own installers, which are all genuine Installer&lt;...&gt; types with constructor-injected
    /// params, called via the base class's inherited static `Install(Container, ...)` factory method.
    /// </summary>
    public class CellsInstaller : Installer<CellConfig, CellsInstaller>
    {
        private readonly CellConfig _cellConfig;

        public CellsInstaller(CellConfig cellConfig)
        {
            _cellConfig = cellConfig;
        }

        public override void InstallBindings()
        {
            Container.Bind<CellConfig>().FromInstance(_cellConfig).AsSingle();
            Container.BindInterfacesAndSelfTo<CellRegistry>().AsSingle();
            Container.Bind<CellSpawner>().FromComponentInHierarchy().AsSingle();
        }
    }
}
