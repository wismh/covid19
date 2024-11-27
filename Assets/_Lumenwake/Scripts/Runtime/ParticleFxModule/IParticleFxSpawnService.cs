using UnityEngine;

namespace Project.Core.ParticleFxModule
{
    public interface IParticleFxSpawnService<TId>
        where TId : struct, System.Enum
    {
        PooledParticleFxView SpawnParticle(TId id, Vector3 position);
    }
}
