using UnityEngine;
using Zenject;

namespace _Project.Mechanics.Pickups
{
    /// <summary>
    /// Binds PickupsConfig and the pooled MedicalPickup prefab (a Zenject <see cref="IMemoryPool"/>, see
    /// MedicalPickup/MedicalSpawner's own "Pooling migration" doc comments) for GameplaySceneInstaller.
    ///
    /// A real Zenject <see cref="Installer{TParam1, TParam2, TDerived}"/> subclass (not a bare static class) -
    /// matches tactics-cards' own installers, which are all genuine Installer&lt;...&gt; types with
    /// constructor-injected params, called via the base class's inherited static `Install(Container, ...)`
    /// factory method.
    /// </summary>
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

            if (_medicalPrefab == null)
                return;

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
