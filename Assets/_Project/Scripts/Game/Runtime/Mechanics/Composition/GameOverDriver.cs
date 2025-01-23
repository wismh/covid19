using Cysharp.Threading.Tasks;
using Lumenwake.UIModule;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Player;
using _Project.Gateways.SaveService;
using _Project.UI.GameOver;

namespace _Project.Mechanics.Composition
{
    public sealed class GameOverDriver : IInitializable, System.IDisposable
    {
        private readonly CellRegistry _cellRegistry;
        private readonly PlayerHealth _playerHealth;
        private readonly PlayerStatsService _playerStats;
        private readonly IGameSaveService _gameSaveService;
        private readonly GameOverScreen _gameOverScreen;
        private readonly IScreenManager _screenManager;

        public GameOverDriver(
            CellRegistry cellRegistry,
            PlayerHealth playerHealth,
            PlayerStatsService playerStats,
            IGameSaveService gameSaveService,
            GameOverScreen gameOverScreen,
            IScreenManager screenManager)
        {
            _cellRegistry = cellRegistry;
            _playerHealth = playerHealth;
            _playerStats = playerStats;
            _gameSaveService = gameSaveService;
            _gameOverScreen = gameOverScreen;
            _screenManager = screenManager;
        }

        public void Initialize()
        {
            _cellRegistry.OnAllCellsDied += HandleAllCellsDied;
            _playerHealth.OnDied += HandlePlayerDied;
        }

        public void Dispose()
        {
            _cellRegistry.OnAllCellsDied -= HandleAllCellsDied;
            _playerHealth.OnDied -= HandlePlayerDied;
        }

        private void HandleAllCellsDied()
        {
            _gameOverScreen.SetAllCellsDied(true);
            _screenManager.OpenScreen<GameOverScreen>().Forget();
        }

        // HudScreen opens GameOverScreen itself on player death; this only handles the save side effects.
        private void HandlePlayerDied()
        {
            float elapsedSeconds = Time.timeSinceLevelLoad;

            _gameSaveService.RecordSurvivalTime(elapsedSeconds);
            _gameSaveService.RecordPlayerLevel(_playerStats.Level);
            _gameSaveService.SaveImmediate();
        }
    }
}
