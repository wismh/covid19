using UnityEngine;
using Zenject;

namespace _Project.Mechanics.Pickups
{
    /// <summary>
    /// Binds PickupsConfig and the pooled MedicalPickup prefab (a Zenject <see cref="IMemoryPool"/>, see
    /// MedicalPickup/MedicalSpawner's own "Pooling migration" doc comments) for GameplaySceneInstaller.
    /// </summary>
    public static class PickupsInstaller
    {
        private const string PoolRootName = "MedicalPool";
        private const int InitialSize = 10;
        private const int MaxSize = 20;

        public static void Install(DiContainer container, PickupsConfig pickupsConfig, MedicalPickup medicalPrefab)
        {
            container.Bind<PickupsConfig>().FromInstance(pickupsConfig).AsSingle();

            if (medicalPrefab == null)
                return;

            var poolRoot = new GameObject(PoolRootName).transform;

            container
                .BindFactory<Vector3, Quaternion, MedicalPickup, MedicalPickup.Factory>()
                .FromMonoPoolableMemoryPool(pool => pool
                    .WithInitialSize(InitialSize)
                    .WithMaxSize(MaxSize)
                    .FromComponentInNewPrefab(medicalPrefab)
                    .UnderTransform(poolRoot));
        }
    }
}
