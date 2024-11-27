using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Project.Core.ParticleFxModule
{
    public sealed class ParticleFxSpawnService<TId> : IParticleFxSpawnService<TId>
        where TId : struct, System.Enum
    {
        private readonly Dictionary<TId, IFactory<Vector3, PooledParticleFxView>> _factories;

        public ParticleFxSpawnService(Dictionary<TId, IFactory<Vector3, PooledParticleFxView>> factories) =>
            _factories = factories;

        public PooledParticleFxView SpawnParticle(TId id, Vector3 position) =>
            _factories[id].Create(position);
    }
}
