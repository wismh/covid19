using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Camera;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Macro;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Navigation
{
    /// <summary>
    /// Controls the navigation arrow indicator, finding the nearest macro virus or infected cell in the arena
    /// and pointing the arrow towards it when it is outside the camera view.
    /// Replaces the legacy GMTK.ArrowController (Assets/GMTK/Scripts/Behaviors/Arrow/ArrowController.cs).
    /// </summary>
    public sealed class ArrowController : MonoBehaviour
    {
        private const float k_findTargetInterval = 0.1f;

        [SerializeField] private Arrow _arrow;
        [SerializeField] private UnityEngine.Camera _camera;

        private MacroVirusRegistry _macroVirusRegistry;
        private CellRegistry _cellRegistry;
        private WorldScaleController _worldScaleController;
        private PlayerHealth _playerHealth;

        private CancellationTokenSource _lifetimeCts;
        private bool _isPlayerDead;

        [Inject]
        public void Construct(
            MacroVirusRegistry macroVirusRegistry,
            CellRegistry cellRegistry,
            WorldScaleController worldScaleController,
            PlayerHealth playerHealth,
            UnityEngine.Camera targetCamera = null)
        {
            _macroVirusRegistry = macroVirusRegistry;
            _cellRegistry = cellRegistry;
            _worldScaleController = worldScaleController;

            if (targetCamera != null)
                _camera = targetCamera;

            _playerHealth = playerHealth;
            _playerHealth.OnDied += HandlePlayerDied;
            _isPlayerDead = !_playerHealth.IsAlive;

            if (_worldScaleController != null)
            {
                _worldScaleController.OnEnteredMicroWorld -= HandleEnteredMicroWorld;
                _worldScaleController.OnEnteredMicroWorld += HandleEnteredMicroWorld;
            }
        }

        private void Awake()
        {
            if (_camera == null)
                _camera = UnityEngine.Camera.main;
        }

        private void Start()
        {
            _lifetimeCts = new CancellationTokenSource();
            StartFindTargetsAsync(_lifetimeCts.Token).Forget();
        }

        private void OnEnable()
        {
            FindTarget();
        }

        private void OnDisable()
        {
            HideArrow();
        }

        private void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();

            _playerHealth.OnDied -= HandlePlayerDied;

            if (_worldScaleController != null)
                _worldScaleController.OnEnteredMicroWorld -= HandleEnteredMicroWorld;
        }

        private async UniTaskVoid StartFindTargetsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                FindTarget();

                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(k_findTargetInterval), cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        public void FindTarget()
        {
            if (!isActiveAndEnabled)
                return;

            if (_isPlayerDead ||
                (_worldScaleController != null && _worldScaleController.CurrentScale == _Project.Mechanics.WorldScale.WorldScale.Micro))
            {
                HideArrow();
                return;
            }

            Transform target = FindNearestTarget();
            if (target == null)
            {
                HideArrow();
                return;
            }

            bool isTargetInCamera = CheckTargetPosition(target);
            if (isTargetInCamera)
            {
                HideArrow();
            }
            else
            {
                _arrow.SetTarget(target);
                _arrow.SetArrowState(true);
            }
        }

        public Transform FindNearestTarget()
        {
            Vector3 origin = transform.position;
            float virusSqrDist = Mathf.Infinity;
            float cellSqrDist = Mathf.Infinity;

            Transform nearestVirus = null;
            if (_macroVirusRegistry != null)
                nearestVirus = _macroVirusRegistry.FindNearestVirusTransform(origin, out virusSqrDist);

            Transform nearestCell = null;
            if (_cellRegistry != null)
                nearestCell = _cellRegistry.FindNearestInfectedCellTransform(origin, out cellSqrDist);

            if (nearestVirus != null && nearestCell != null)
                return virusSqrDist <= cellSqrDist ? nearestVirus : nearestCell;

            if (nearestVirus != null)
                return nearestVirus;

            if (nearestCell != null)
                return nearestCell;

            return null;
        }

        private bool CheckTargetPosition(Transform target)
        {
            if (target == null)
                return false;

            if (_camera == null)
                _camera = UnityEngine.Camera.main;

            if (_camera == null)
                return false;

            Bounds bounds = _camera.OrthographicBounds();

            return target.position.x >= bounds.min.x &&
                   target.position.x <= bounds.max.x &&
                   target.position.y >= bounds.min.y &&
                   target.position.y <= bounds.max.y;
        }

        private void HideArrow()
        {
            _arrow.SetArrowState(false);
            _arrow.SetTarget(null);
        }

        private void HandlePlayerDied()
        {
            _isPlayerDead = true;
            HideArrow();
        }

        private void HandleEnteredMicroWorld()
        {
            HideArrow();
        }
    }
}
