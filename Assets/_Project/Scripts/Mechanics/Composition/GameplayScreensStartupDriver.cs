using Cysharp.Threading.Tasks;
using Lumenwake.UIModule;
using Zenject;
using _Project.Mechanics.Audio;
using _Project.Mechanics.Player;
using _Project.Mechanics.WorldScale;
using _Project.UI.GameOver;
using _Project.UI.Hud;
using _Project.UI.Upgrades;

namespace _Project.Mechanics.Composition
{
    public sealed class GameplayScreensStartupDriver : IInitializable
    {
        private readonly UpgradeScreen _upgradeScreen;
        private readonly GameOverScreen _gameOverScreen;
        private readonly CellProximityPrompt _cellProximityPrompt;
        private readonly WorldScaleTransitionContext _worldScaleTransitionContext;
        private readonly PlayerMovement _playerMovement;
        private readonly GameAudioService _gameAudioService;
        private readonly IScreenManager _screenManager;

        public GameplayScreensStartupDriver(
            UpgradeScreen upgradeScreen,
            GameOverScreen gameOverScreen,
            CellProximityPrompt cellProximityPrompt,
            WorldScaleTransitionContext worldScaleTransitionContext,
            PlayerMovement playerMovement,
            GameAudioService gameAudioService,
            IScreenManager screenManager)
        {
            _upgradeScreen = upgradeScreen;
            _gameOverScreen = gameOverScreen;
            _cellProximityPrompt = cellProximityPrompt;
            _worldScaleTransitionContext = worldScaleTransitionContext;
            _playerMovement = playerMovement;
            _gameAudioService = gameAudioService;
            _screenManager = screenManager;
        }

        public void Initialize()
        {
            _upgradeScreen.gameObject.SetActive(false);
            _gameOverScreen.gameObject.SetActive(false);

            _cellProximityPrompt.SetContext(_worldScaleTransitionContext);
            _cellProximityPrompt.SetPlayer(_playerMovement.transform);

            _gameAudioService.PlayGameplayMusic();
            _screenManager.OpenScreen<HudScreen>().Forget();
        }
    }
}
