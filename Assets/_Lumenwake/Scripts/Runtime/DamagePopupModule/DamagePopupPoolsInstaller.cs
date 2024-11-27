using UnityEngine;
using Zenject;

namespace Project.Core.DamagePopupModule
{
    public class DamagePopupPoolsInstaller : Installer<DamagePopupView, int, DamagePopupPoolsInstaller>
    {
        private const string DefaultPoolRootName = "DamagePopupPool";

        private readonly DamagePopupView _prefab;
        private readonly int _initialSize;

        public DamagePopupPoolsInstaller(DamagePopupView prefab, int initialSize)
        {
            _prefab = prefab;
            _initialSize = Mathf.Max(0, initialSize);
        }

        public override void InstallBindings()
        {
            var poolRoot = ResolvePoolRoot();
            Container
                .BindFactory<Vector3, DamagePopupInfo, DamagePopupView, DamagePopupView.Factory>()
                .FromMonoPoolableMemoryPool(pool =>
                {
                    pool
                        .WithInitialSize(_initialSize)
                        .FromComponentInNewPrefab(_prefab)
                        .UnderTransform(poolRoot);
                });

            Container.Bind<IDamagePopupSpawner>().To<DamagePopupSpawner>().AsSingle();
        }

        private Transform ResolvePoolRoot()
        {
            var root = new GameObject(DefaultPoolRootName);
            return root.transform;
        }
    }
}
