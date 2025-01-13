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
        [Header("Particle System")]
        [SerializeField] private ParticleSystem _particleSystem;
        [SerializeField] private PlayerMovement _playerMovement;

        // Emission-rate range and Macro/Micro speed/size/emission multipliers now live on
        // WorldScaleConfig instead of local [SerializeField]s - see that class's own "Player particle trail"
        // header.
        private WorldScaleConfig _worldScaleConfig;
        private WorldScaleController _worldScaleController;
        private ParticleSystem.EmissionModule _emissionModule;
        private ParticleSystem.MainModule _mainModule;

        private float _baseStartSpeed = 1f;
        private float _baseStartSize = 1f;
        private float _currentEmissionMultiplier = 1f;
        private bool _isInitialized;

        [Inject]
        public void Construct(WorldScaleConfig worldScaleConfig, WorldScaleController worldScaleController, PlayerMovement playerMovement = null)
        {
            _worldScaleConfig = worldScaleConfig;

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

            _emissionModule = _particleSystem.emission;
            _mainModule = _particleSystem.main;
            _baseStartSpeed = _mainModule.startSpeedMultiplier;
            _baseStartSize = _mainModule.startSizeMultiplier;

            _isInitialized = true;
        }

        private void HandleScaleChanged(WorldScale.WorldScale scale)
        {
            ApplyScale(scale);
        }

        private void ApplyScale(WorldScale.WorldScale scale)
        {
            InitializeIfNeeded();

            bool isMicro = scale == WorldScale.WorldScale.Micro;
            _currentEmissionMultiplier = isMicro ? _worldScaleConfig.MicroParticleEmissionMultiplier : _worldScaleConfig.MacroParticleEmissionMultiplier;

            float speedMult = isMicro ? _worldScaleConfig.MicroParticleStartSpeedMultiplier : _worldScaleConfig.MacroParticleStartSpeedMultiplier;
            float sizeMult = isMicro ? _worldScaleConfig.MicroParticleStartSizeMultiplier : _worldScaleConfig.MacroParticleStartSizeMultiplier;

            _mainModule.startSpeedMultiplier = _baseStartSpeed * speedMult;
            _mainModule.startSizeMultiplier = _baseStartSize * sizeMult;
        }

        private void UpdateEmission()
        {
            float velocityMagnitude = 0f;
            float maxSpeed = 3f;

            if (_playerMovement != null)
            {
                velocityMagnitude = _playerMovement.Velocity.magnitude;
                maxSpeed = _playerMovement.MaxSpeed;
            }

            if (maxSpeed <= 0.001f)
                maxSpeed = 3f;

            Vector2 minMaxEmissionRate = _worldScaleConfig.ParticleMinMaxEmissionRate;
            float speedRatio = Mathf.Clamp01(velocityMagnitude / maxSpeed);
            float targetRate = Mathf.Lerp(minMaxEmissionRate.x, minMaxEmissionRate.y, speedRatio * _currentEmissionMultiplier);

            _emissionModule.rateOverTime = targetRate;
        }
    }
}
