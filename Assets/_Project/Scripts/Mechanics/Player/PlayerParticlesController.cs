using UnityEngine;
using Zenject;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Player
{
    /// <summary>
    /// Controls the player's engine bubble trail particle system emission, particle speed, and particle size
    /// according to player velocity and the current world scale (Macro vs Micro).
    /// </summary>
    public sealed class PlayerParticlesController : MonoBehaviour
    {
        [Header("Particle System")]
        [SerializeField] private ParticleSystem _particleSystem;
        [SerializeField] private PlayerMovement _playerMovement;

        private WorldScaleConfig _worldScaleConfig;
        private WorldScaleController _worldScaleController;
        private ParticleSystem.EmissionModule _emissionModule;
        private ParticleSystem.MainModule _mainModule;

        private float _baseStartSpeed = 1f;
        private float _baseStartSize = 1f;
        private float _currentEmissionMultiplier = 1f;
        private bool _isInitialized;

        [Inject]
        public void Construct(WorldScaleConfig worldScaleConfig, WorldScaleController worldScaleController, PlayerMovement playerMovement)
        {
            _worldScaleConfig = worldScaleConfig;
            _playerMovement = playerMovement;
            _worldScaleController = worldScaleController;

            _worldScaleController.OnScaleChanged += HandleScaleChanged;
            ApplyScale(_worldScaleController.CurrentScale);
        }

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void Update()
        {
            UpdateEmission();
        }

        private void OnDestroy()
        {
            _worldScaleController.OnScaleChanged -= HandleScaleChanged;
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
            float velocityMagnitude = _playerMovement.Velocity.magnitude;
            float maxSpeed = _playerMovement.MaxSpeed;

            if (maxSpeed <= 0.001f)
                maxSpeed = 3f;

            Vector2 minMaxEmissionRate = _worldScaleConfig.ParticleMinMaxEmissionRate;
            float speedRatio = Mathf.Clamp01(velocityMagnitude / maxSpeed);
            float targetRate = Mathf.Lerp(minMaxEmissionRate.x, minMaxEmissionRate.y, speedRatio * _currentEmissionMultiplier);

            _emissionModule.rateOverTime = targetRate;
        }
    }
}
