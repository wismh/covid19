using UnityEngine;
using Zenject;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Player
{
    /// <summary>
    /// Controls the player's engine bubble trail particle system emission, particle speed, and particle size
    /// according to player velocity and the current world scale (Macro vs Micro).
    /// Replaces the legacy GMTK.PlayerParticlesController (Assets/GMTK/Scripts/Behaviors/Player/PlayerParticlesController.cs).
    /// </summary>
    public sealed class PlayerParticlesController : MonoBehaviour
    {
        [Header("Particle System & Base Emission")]
        [SerializeField] private ParticleSystem _particleSystem;
        [SerializeField] private Vector2 _minMaxEmissionRate = new Vector2(0f, 15f);
        [SerializeField] private PlayerMovement _playerMovement;

        [Header("Macro World Scale Modifiers")]
        [SerializeField] private float _macroStartSpeedMultiplier = 1f;
        [SerializeField] private float _macroStartSizeMultiplier = 1f;
        [SerializeField] private float _macroEmissionMultiplier = 1f;

        [Header("Micro World Scale Modifiers")]
        [SerializeField] private float _microStartSpeedMultiplier = 0.3f;
        [SerializeField] private float _microStartSizeMultiplier = 0.3f;
        [SerializeField] private float _microEmissionMultiplier = 3f;

        private WorldScaleController _worldScaleController;
        private ParticleSystem.EmissionModule _emissionModule;
        private ParticleSystem.MainModule _mainModule;

        private float _baseStartSpeed = 1f;
        private float _baseStartSize = 1f;
        private float _currentEmissionMultiplier = 1f;
        private bool _isInitialized;

        [Inject]
        public void Construct(WorldScaleController worldScaleController, PlayerMovement playerMovement = null)
        {
            if (playerMovement != null)
                _playerMovement = playerMovement;

            SetWorldScaleController(worldScaleController);
        }

        public void SetWorldScaleController(WorldScaleController worldScaleController)
        {
            if (_worldScaleController != null)
            {
                _worldScaleController.OnScaleChanged -= HandleScaleChanged;
            }

            _worldScaleController = worldScaleController;

            if (_worldScaleController != null)
            {
                _worldScaleController.OnScaleChanged += HandleScaleChanged;
                ApplyScale(_worldScaleController.CurrentScale);
            }
        }

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void Start()
        {
            if (_playerMovement == null)
                _playerMovement = GetComponent<PlayerMovement>();

            if (_worldScaleController != null)
                ApplyScale(_worldScaleController.CurrentScale);
        }

        private void Update()
        {
            UpdateEmission();
        }

        private void OnDestroy()
        {
            if (_worldScaleController != null)
            {
                _worldScaleController.OnScaleChanged -= HandleScaleChanged;
            }
        }

        private void InitializeIfNeeded()
        {
            if (_isInitialized)
                return;

            if (_particleSystem == null)
                _particleSystem = GetComponentInChildren<ParticleSystem>();

            if (_particleSystem != null)
            {
                _emissionModule = _particleSystem.emission;
                _mainModule = _particleSystem.main;
                _baseStartSpeed = _mainModule.startSpeedMultiplier;
                _baseStartSize = _mainModule.startSizeMultiplier;
            }

            _isInitialized = true;
        }

        private void HandleScaleChanged(WorldScale.WorldScale scale)
        {
            ApplyScale(scale);
        }

        private void ApplyScale(WorldScale.WorldScale scale)
        {
            InitializeIfNeeded();

            if (_particleSystem == null)
                return;

            bool isMicro = scale == WorldScale.WorldScale.Micro;
            _currentEmissionMultiplier = isMicro ? _microEmissionMultiplier : _macroEmissionMultiplier;

            float speedMult = isMicro ? _microStartSpeedMultiplier : _macroStartSpeedMultiplier;
            float sizeMult = isMicro ? _microStartSizeMultiplier : _macroStartSizeMultiplier;

            _mainModule.startSpeedMultiplier = _baseStartSpeed * speedMult;
            _mainModule.startSizeMultiplier = _baseStartSize * sizeMult;
        }

        private void UpdateEmission()
        {
            if (_particleSystem == null)
                return;

            float velocityMagnitude = 0f;
            float maxSpeed = 3f;

            if (_playerMovement != null)
            {
                velocityMagnitude = _playerMovement.Velocity.magnitude;
                maxSpeed = _playerMovement.MaxSpeed;
            }

            if (maxSpeed <= 0.001f)
                maxSpeed = 3f;

            float speedRatio = Mathf.Clamp01(velocityMagnitude / maxSpeed);
            float targetRate = Mathf.Lerp(_minMaxEmissionRate.x, _minMaxEmissionRate.y, speedRatio * _currentEmissionMultiplier);

            _emissionModule.rateOverTime = targetRate;
        }
    }
}
