using System;

namespace Project.Core.ParticleFxModule
{
    public readonly struct ParticleFxPoolBinding<TId>
        where TId : struct, Enum
    {
        public ParticleFxPoolBinding(TId id, PooledParticleFxView prefab, int initialSize)
        {
            Id = id;
            Prefab = prefab;
            InitialSize = initialSize;
        }

        public TId Id { get; }

        public PooledParticleFxView Prefab { get; }

        public int InitialSize { get; }
    }
}
