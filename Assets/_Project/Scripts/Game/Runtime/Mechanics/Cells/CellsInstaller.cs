using Zenject;

namespace _Project.Mechanics.Cells
{
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
