using UnityEngine;
using Zenject;

namespace Project.Core.ParticleFxModule
{
    public class PooledParticleFxView : MonoBehaviour, IPoolable<Vector3, IMemoryPool>
    {
        [SerializeField] private ParticleSystem _particleSystem;
        [SerializeField] private bool _despawnWhenParticlesFinish = true;
        [SerializeField] private float _maxLifetime = 5f;

        private IMemoryPool _pool;
        private float _spawnTime;

        private void Update()
        {
            if (_pool == null)
                return;

            if (_despawnWhenParticlesFinish && !_particleSystem.IsAlive(true))
            {
                _pool.Despawn(this);
                return;
            }

            if (Time.time - _spawnTime >= _maxLifetime)
                _pool.Despawn(this);
        }

        public void OnSpawned(Vector3 position, IMemoryPool pool)
        {
            _pool = pool;
            _spawnTime = Time.time;
            transform.position = position;

            _particleSystem.Clear(true);
            _particleSystem.Play(true);
        }

        public void OnDespawned()
        {
            _pool = null;
            
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public class Factory : PlaceholderFactory<Vector3, PooledParticleFxView>
        {
        }
    }
}
