using Project.Core.AudioSystem;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Player;
using _Project.Mechanics.WorldScale;
using _Project.UI.Hud;
using CameraFollow = _Project.Mechanics.Camera.CameraFollow;

namespace _Project.Mechanics.Composition
{
    public class WorldAndMetaInstaller : MonoInstaller
    {
        [Header("World scale")]
        [SerializeField] private WorldScaleConfig _worldScaleConfig;
        [SerializeField] private UnityEngine.Camera _worldCamera;
        [SerializeField] private CameraFollow _cameraFollow;
        [SerializeField] private Transform _playerVisualRoot;
        [SerializeField] private BoxCollider2D _playerCollider;

        [Header("Audio")]
        [SerializeField] private SoundData _gameplayMusic;
        [SerializeField] private SoundData _enterMicroWhooshSound;
        [SerializeField] private SoundData _exitMicroWhooshSound;

        [Header("Cell-proximity polling")]
        [SerializeField] private CellProximityPrompt _cellProximityPrompt;
        [SerializeField] private float _cellProximityPollIntervalSeconds = 0.2f;

        public override void InstallBindings()
        {
            WorldScaleInstaller.Install(Container, _worldScaleConfig);

            Container.Bind<CellProximityPrompt>().FromInstance(_cellProximityPrompt).AsSingle();

            Container.Bind<WorldScaleTransitionContext>()
                .FromMethod(CreateWorldScaleTransitionContext)
                .AsSingle();

            GameplayEventWiringInstaller.Install(Container, _enterMicroWhooshSound, _exitMicroWhooshSound);

            Container.BindInterfacesAndSelfTo<ForcedMacroExitOnCellDeathDriver>().AsSingle();
            Container.BindInterfacesAndSelfTo<NearestCellTrackingDriver>().AsSingle().WithArguments(_cellProximityPollIntervalSeconds);
            Container.BindInterfacesAndSelfTo<GameplayScreensStartupDriver>().AsSingle().WithArguments(_gameplayMusic);
        }

        private WorldScaleTransitionContext CreateWorldScaleTransitionContext(InjectContext ctx)
        {
            var playerMovement = ctx.Container.Resolve<PlayerMovement>();

            var context = new WorldScaleTransitionContext
            {
                Player = playerMovement.transform,
                PlayerVisualRoot = _playerVisualRoot,
                PlayerCollider = _playerCollider,
                PlayerMovement = playerMovement,
                Camera = _worldCamera,
                SetCameraTarget = t => _cameraFollow.SetTarget(t),
                SetPlayerSpeedScale = playerMovement.SetSpeedScale,
            };

            // Sets the camera's initial follow target immediately, matching the old composition root's
            // Start()-time call (Awake() only registered the delegate, didn't invoke it).
            context.SetCameraTarget(playerMovement.transform);

            return context;
        }
    }
}
