using UnityEngine;
using _Project.Mechanics.Player;

namespace _Project.Mechanics.Combat
{
    /// <summary>
    /// Fires the player's bullets, pooled via a Zenject <see cref="Bullet.Factory"/> (bound by
    /// PlayerInstaller.Install - see its own doc comment) instead of the previous hand-rolled
    /// <see cref="UnityEngine.Pool.ObjectPool{T}"/>. Implements IProjectileSpawner for player firing,
    /// pulling effective damage from PlayerStatsService. World-scale-dependent speed/size now lives on
    /// <see cref="Bullet"/> itself (it injects WorldScaleController directly) rather than being threaded through
    /// here, since that concern belongs to the bullet's own flight, not to who fired it.
    /// </summary>
    public class BulletManager : MonoBehaviour, IProjectileSpawner
    {
        private PlayerStatsService _playerStats;
        private Bullet.Factory _bulletFactory;

        /// <summary>Called by <see cref="Composition.PlayerCombatSpawnerDriver"/> rather than [Inject]-tagged
        /// directly, since it also needs to wire itself into PlayerTurret as the projectile spawner.</summary>
        public void Construct(PlayerStatsService playerStats, Bullet.Factory bulletFactory)
        {
            _playerStats = playerStats;
            _bulletFactory = bulletFactory;
        }

        public void SpawnProjectile(Vector3 position, Vector3 direction)
        {
            Fire(position, direction);
        }

        private void Fire(Vector3 position, Vector3 direction)
        {
            _bulletFactory.Create(position, direction, _playerStats.EffectiveDamage);
        }
    }
}
