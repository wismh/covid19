using Cysharp.Threading.Tasks;
using Lumenwake.UIModule;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Player;
using _Project.Save;
using _Project.UI.GameOver;

namespace _Project.Mechanics.Composition
{
    /// <summary>
    /// Split out of MainSceneComposition.WireCrossSystemEvents/HandleAllCellsDied/HandleGameOver (see
    /// MacroVirusReplacementDriver's doc comment for the "MainCompositionRoot split" context). Bound by
    /// <see cref="GameplayEventWiringInstaller"/>.
    ///
    /// The forced-macro-exit half of the old combined OnAllCellsDied handler stays on MainSceneComposition (it
    /// needs the locally-owned, per-poll-tick-mutated WorldScaleTransitionContext, which is deliberately not a
    /// container binding - see that class's own doc comment) - this driver owns only the two game-over-flow
    /// concerns that don't need it: opening the screen, and the death save-side-effects.
    ///
    /// --- OnAllCellsDied -&gt; show screen ---
    /// Old: CellController.Die()'s `if (_cellManager.Pool.Count == 0)` branch, reproduced via
    /// <see cref="CellRegistry.OnAllCellsDied"/> (see CellRegistry's own "OnCellDied vs OnAllCellsDied" doc
    /// comment).
    ///
    /// --- PlayerHealth.OnDied -&gt; save ---
    /// Old: the player-death branch of HUD.GameOverGroup(true). HudScreen's own OnDied subscription opens the
    /// GameOverScreen panel itself for the player-death case (see HudScreen's "Auto-opening" doc comment) - this
    /// handler only owns the SAVE side of game-over, which no screen has a reason to know about. Elapsed-time
    /// source: `Time.timeSinceLevelLoad`, the same de-facto shared elapsed-time substitute every other system in
    /// this phase uses (see MainSceneComposition's original HandleGameOver doc comment for the full rationale) -
    /// accurate here for the same reason: this driver's Initialize() runs at gameplay start.
    /// </summary>
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

        private void HandlePlayerDied()
        {
            float elapsedSeconds = Time.timeSinceLevelLoad;

            _gameSaveService.RecordSurvivalTime(elapsedSeconds);
            _gameSaveService.RecordPlayerLevel(_playerStats.Level);

            // See IGameSaveService.SaveImmediate's own doc comment: right after game-over is exactly the case
            // where losing the debounce window to a crash/force-quit would be most noticeable to the player, so
            // this flushes immediately rather than relying on RecordSurvivalTime/RecordPlayerLevel's own
            // debounced RequestSave.
            _gameSaveService.SaveImmediate();
        }
    }
}
