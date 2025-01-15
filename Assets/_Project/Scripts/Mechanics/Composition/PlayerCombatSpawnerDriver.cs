using Zenject;
using _Project.Mechanics.Combat;
using _Project.Mechanics.Player;

namespace _Project.Mechanics.Composition
{
    public sealed class PlayerCombatSpawnerDriver : IInitializable
    {
        private readonly BulletManager _bulletManager;
        private readonly PlayerTurret _playerTurret;
        private readonly PlayerStatsService _playerStats;
        private readonly Bullet.Factory _bulletFactory;

        public PlayerCombatSpawnerDriver(
            BulletManager bulletManager,
            PlayerTurret playerTurret,
            PlayerStatsService playerStats,
            Bullet.Factory bulletFactory)
        {
            _bulletManager = bulletManager;
            _playerTurret = playerTurret;
            _playerStats = playerStats;
            _bulletFactory = bulletFactory;
        }

        public void Initialize()
        {
            _bulletManager.Construct(_playerStats, _bulletFactory);
            _playerTurret.SetProjectileSpawner(_bulletManager);
        }
    }
}
