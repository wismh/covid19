using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Project.Core.ParticleFxModule
{
    public sealed class ParticleFxPoolsInstaller<TId> :
        Installer<IReadOnlyList<ParticleFxPoolBinding<TId>>, Transform, ParticleFxPoolsInstaller<TId>>
        where TId : struct, Enum
    {
        private readonly IReadOnlyList<ParticleFxPoolBinding<TId>> _bindings;
        private readonly Transform _poolRootParent;

        public ParticleFxPoolsInstaller(
            IReadOnlyList<ParticleFxPoolBinding<TId>> bindings,
            Transform poolRootParent)
        {
            _bindings = bindings;
            _poolRootParent = poolRootParent;
        }

        public override void InstallBindings()
        {
            foreach (ParticleFxPoolBinding<TId> binding in _bindings)
                BindPool(binding, _poolRootParent);

            Container
                .Bind<Dictionary<TId, IFactory<Vector3, PooledParticleFxView>>>()
                .FromMethod(CreateFactoryMap)
                .AsSingle();

            Container
                .Bind<IParticleFxSpawnService<TId>>()
                .To<ParticleFxSpawnService<TId>>()
                .AsSingle();
        }

        private Dictionary<TId, IFactory<Vector3, PooledParticleFxView>> CreateFactoryMap(InjectContext context)
        {
            var factories = new Dictionary<TId, IFactory<Vector3, PooledParticleFxView>>(_bindings.Count);

            foreach (ParticleFxPoolBinding<TId> binding in _bindings)
                factories[binding.Id] = context.Container.ResolveId<PooledParticleFxView.Factory>(binding.Id);

            return factories;
        }

        private void BindPool(ParticleFxPoolBinding<TId> binding, Transform poolRoot)
        {
            Container
                .BindFactory<Vector3, PooledParticleFxView, PooledParticleFxView.Factory>()
                .WithId(binding.Id)
                .FromMonoPoolableMemoryPool(pool =>
                {
                    pool
                        .WithInitialSize(Mathf.Max(0, binding.InitialSize))
                        .FromComponentInNewPrefab(binding.Prefab)
                        .UnderTransform(poolRoot);
                });
        }
    }
}
