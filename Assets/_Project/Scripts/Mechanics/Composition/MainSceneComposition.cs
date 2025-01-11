using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lumenwake.UIModule;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Audio;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Combat;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Micro;
using _Project.Mechanics.WorldScale;
using _Project.UI.GameOver;
using _Project.UI.Hud;
using _Project.UI.Upgrades;
using CameraFollow = _Project.Mechanics.Camera.CameraFollow;

namespace _Project.Mechanics.Composition
{
    public sealed class MainSceneComposition : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerTurret _playerTurret;
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private _Project.Mechanics.Combat.BulletManager _bulletManager;

        [Header("World scale")]
        [SerializeField] private UnityEngine.Camera _worldCamera;
        [SerializeField] private _Project.Mechanics.Camera.CameraFollow _cameraFollow;
        [SerializeField] private Transform _playerVisualRoot;
        [SerializeField] private BoxCollider2D _playerCollider;

        [Header("UI screens")]
        [SerializeField] private HudScreen _hudScreen;
        [SerializeField] private UpgradeScreen _upgradeScreen;
        [SerializeField] private GameOverScreen _gameOverScreen;
        [SerializeField] private CellProximityPrompt _cellProximityPrompt;

        [Header("Cell-proximity polling")]
        [SerializeField] private float _cellProximityPollIntervalSeconds = 0.2f;

        private IScreenManager _screenManager;
        private PlayerStatsService _playerStats;
        private CellRegistry _cellRegistry;
        private WorldScaleController _worldScaleController;
        private GameAudioService _gameAudioService;
        private Bullet.Factory _bulletFactory;

        private WorldScaleTransitionContext _worldScaleTransitionContext;
        private Action<Vector3, int> _handleCellDiedForceMacroExit;
        private CancellationTokenSource _lifetimeCts;

        [Inject]
        public void Construct(
            PlayerStatsService playerStats,
            CellRegistry cellRegistry,
            WorldScaleController worldScaleController,
            GameAudioService gameAudioService,
            IScreenManager screenManager,
            Bullet.Factory bulletFactory)
        {
            _playerStats = playerStats;
            _cellRegistry = cellRegistry;
            _worldScaleController = worldScaleController;
            _gameAudioService = gameAudioService;
            _screenManager = screenManager;
            _bulletFactory = bulletFactory;
        }

        private void Awake()
        {
            _lifetimeCts = new CancellationTokenSource();

            ResolvePlayerComponentReferences();

            HideOverlayScreensOnStart();
            BuildWorldScaleTransitionContext();
            ConstructSpawners();
            ConstructScreens();
            WireForcedMacroExitOnCellDeath();

            _gameAudioService.PlayGameplayMusic();
            _screenManager.OpenScreen<HudScreen>().Forget();

            PollNearestCellAsync(_lifetimeCts.Token).Forget();
        }

        private void Start()
        {
            if (_playerMovement != null)
                _cameraFollow.SetTarget(_playerMovement.transform);

            _worldScaleTransitionContext.SetCameraTarget = t => _cameraFollow.SetTarget(t);
        }

        private void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();

            _cellRegistry.OnAllCellsDied -= HandleAllCellsDiedForceMacroExit;
            _cellRegistry.OnCellDied -= _handleCellDiedForceMacroExit;

            _gameAudioService.Dispose();
        }

        // A runtime scene load (as opposed to opening the scene directly in the Editor) can deserialize
        // custom-MonoBehaviour SerializeField references on this component as null while built-in-type
        // references on the same GameObject resolve fine. Falls back to GetComponent off the reliably-resolving
        // collider reference.
        private void ResolvePlayerComponentReferences()
        {
            if (_playerCollider == null)
                return;

            if (_playerMovement == null)
                _playerMovement = _playerCollider.GetComponent<PlayerMovement>();
            if (_playerTurret == null)
                _playerTurret = _playerCollider.GetComponent<PlayerTurret>();
            if (_playerHealth == null)
                _playerHealth = _playerCollider.GetComponent<PlayerHealth>();
            if (_bulletManager == null)
                _bulletManager = FindFirstObjectByType<_Project.Mechanics.Combat.BulletManager>();
        }

        private void HideOverlayScreensOnStart()
        {
            _upgradeScreen.gameObject.SetActive(false);
            _gameOverScreen.gameObject.SetActive(false);
        }

        private void BuildWorldScaleTransitionContext()
        {
            _worldScaleTransitionContext = new WorldScaleTransitionContext
            {
                Player = _playerMovement != null ? _playerMovement.transform : null,
                PlayerVisualRoot = _playerVisualRoot,
                PlayerCollider = _playerCollider,
                PlayerMovement = _playerMovement,
                Camera = _worldCamera,
                SetCameraTarget = t => _cameraFollow.SetTarget(t),
                SetPlayerSpeedScale = _playerMovement != null ? _playerMovement.SetSpeedScale : (Action<float>)null,
            };
        }

        private void ConstructSpawners()
        {
            if (_bulletManager != null)
                _bulletManager.Construct(_playerStats, _bulletFactory);

            if (_playerTurret != null)
                _playerTurret.SetProjectileSpawner(_bulletManager);
        }

        private void ConstructScreens()
        {
            _cellProximityPrompt.SetContext(_worldScaleTransitionContext);

            if (_playerMovement != null)
                _cellProximityPrompt.SetPlayer(_playerMovement.transform);
        }

        private void WireForcedMacroExitOnCellDeath()
        {
            _handleCellDiedForceMacroExit = (position, _) =>
            {
                if (_worldScaleController.CurrentScale != _Project.Mechanics.WorldScale.WorldScale.Micro)
                    return;

                if (_worldScaleTransitionContext.Cell == null || _worldScaleTransitionContext.Cell.position != position)
                    return;

                EnterMacroWorldSafeAsync().Forget();
            };
            _cellRegistry.OnCellDied += _handleCellDiedForceMacroExit;

            _cellRegistry.OnAllCellsDied += HandleAllCellsDiedForceMacroExit;
        }

        // GameOverDriver independently handles showing the game-over screen on this same event.
        private void HandleAllCellsDiedForceMacroExit()
        {
            if (_worldScaleController.CurrentScale == _Project.Mechanics.WorldScale.WorldScale.Micro)
                EnterMacroWorldSafeAsync().Forget();
        }

        private async UniTaskVoid EnterMacroWorldSafeAsync()
        {
            try
            {
                await _worldScaleController.EnterMacroWorldAsync(_worldScaleTransitionContext, _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTaskVoid PollNearestCellAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                UpdateNearestCellTarget();

                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_cellProximityPollIntervalSeconds), cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void UpdateNearestCellTarget()
        {
            if (_playerMovement == null)
                return;

            Transform nearestCell = _cellRegistry.FindNearestCell(_playerMovement.transform.position, out _);

            _cellProximityPrompt.SetTargetCell(nearestCell);

            _worldScaleTransitionContext.Cell = nearestCell;
            _worldScaleTransitionContext.CellVisuals = nearestCell != null ? nearestCell.GetComponent<CellVisuals>() : null;
            _worldScaleTransitionContext.CellRigidbody2D = nearestCell != null ? nearestCell.GetComponent<Rigidbody2D>() : null;
            _worldScaleTransitionContext.CellAnimator = nearestCell != null ? nearestCell.GetComponent<Animator>() : null;
            _worldScaleTransitionContext.CellMicroVirusSpawner = nearestCell != null ? nearestCell.GetComponent<MicroVirusSpawner>() : null;
            _worldScaleTransitionContext.CellInfectionState = nearestCell != null ? nearestCell.GetComponent<CellInfectionState>() : null;
        }
    }
}
