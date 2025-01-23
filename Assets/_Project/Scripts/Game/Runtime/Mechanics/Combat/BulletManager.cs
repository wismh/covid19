using UnityEngine;
using _Project.Mechanics.Player;

namespace _Project.Mechanics.Combat
{
    /// <summary>Fires the player's bullets, pooled via a Zenject <see cref="Bullet.Factory"/>. Pulls effective
    /// damage from PlayerStatsService; world-scale-dependent speed/size lives on <see cref="Bullet"/> itself.
    /// </summary>
    public class BulletManager : MonoBehaviour, IProjectileSpawner
    {
        private PlayerStatsService _playerStats;
        private Bullet.Factory _bulletFactory;

        /// <summary>Called by <see cref="Composition.PlayerCombatSpawnerDriver"/>, which also wires this in as
        /// PlayerTurret's projectile spawner.</summary>
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
