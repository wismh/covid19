using Zenject;

namespace _Project.Mechanics.Cells
{
    /// <summary>
    /// Binds the cell registry/config for GameplaySceneInstaller. CellRegistry also implements
    /// ICellTargetProvider (Mechanics/Viruses/Macro/ICellTargetProvider.cs) - BindInterfacesAndSelfTo covers
    /// both from the one singleton instance. Also binds the scene-placed CellSpawner itself (mirrors
    /// PlayerInstaller's PlayerMovement/PlayerTurret/PlayerHealth bindings) so GameplayEventWiringInstaller's
    /// VirusExperienceDriver can resolve it via constructor injection instead of a hand-built reference.
    /// </summary>
    public static class CellsInstaller
    {
        public static void Install(DiContainer container, CellConfig cellConfig)
        {
            container.Bind<CellConfig>().FromInstance(cellConfig).AsSingle();
            container.BindInterfacesAndSelfTo<CellRegistry>().AsSingle();
            container.Bind<CellSpawner>().FromComponentInHierarchy().AsSingle();
        }
    }
}
