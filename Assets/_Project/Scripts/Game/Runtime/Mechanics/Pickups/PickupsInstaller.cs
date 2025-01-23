using UnityEngine;
using Zenject;

namespace _Project.Mechanics.Pickups
{
    public class PickupsInstaller : Installer<PickupsConfig, MedicalPickup, PickupsInstaller>
    {
        private const string PoolRootName = "MedicalPool";
        private const int InitialSize = 10;
        private const int MaxSize = 20;

        private readonly PickupsConfig _pickupsConfig;
        private readonly MedicalPickup _medicalPrefab;

        public PickupsInstaller(PickupsConfig pickupsConfig, MedicalPickup medicalPrefab)
        {
            _pickupsConfig = pickupsConfig;
            _medicalPrefab = medicalPrefab;
        }

        public override void InstallBindings()
        {
            Container.Bind<PickupsConfig>().FromInstance(_pickupsConfig).AsSingle();

            var poolRoot = new GameObject(PoolRootName).transform;

            Container
                .BindFactory<Vector3, Quaternion, MedicalPickup, MedicalPickup.Factory>()
                .FromMonoPoolableMemoryPool(pool => pool
                    .WithInitialSize(InitialSize)
                    .WithMaxSize(MaxSize)
                    .FromComponentInNewPrefab(_medicalPrefab)
                    .UnderTransform(poolRoot));
        }
    }
}
