using UnityEngine;

namespace _Project.Mechanics.WorldScale
{
    /// <summary>Tunable durations/scales/sizes for the Macro/Micro world-scale transition and its coupled
    /// player-particle-trail and cell-proximity-prompt effects.</summary>
    [CreateAssetMenu(
        menuName = "Configurations/WorldScale/" + nameof(WorldScaleConfig),
        fileName = nameof(WorldScaleConfig) + "_Default")]
    public sealed class WorldScaleConfig : ScriptableObject
    {
        [Header("Transition timing")]
        [SerializeField] private float _transitionDurationSeconds = 1f;

        [Header("Player scale")]
        [SerializeField] private Vector3 _macroPlayerScale = new(1f, 1f, 1f);
        [SerializeField] private Vector3 _microPlayerScale = new(0.3f, 0.3f, 1f);

        [Header("Camera")]
        [SerializeField] private float _macroCameraOrthographicSize = 5f;
        [SerializeField] private float _microCameraOrthographicSize = 1.9f;

        [Header("Macro exit placement")]
        [SerializeField] private float _macroExitOffsetDistance = 5f;

        [Header("Player audio")]
        [SerializeField] private float _microPlayerAudioVolume = 0.4f;
        [SerializeField] private float _restoredPlayerAudioVolume = 1f;

        [Header("Cell physics")]
        [SerializeField] private Vector2 _cellMacroMaxVelocity = new(0.6f, 0.6f);

        [Header("Player speed scale")]
        [SerializeField] private float _macroSpeedScale = 1f;
        [SerializeField] private float _microSpeedScale = 0.01f;

        [Header("Player particle trail")]
        [SerializeField] private Vector2 _particleMinMaxEmissionRate = new(0f, 15f);

        [SerializeField] private float _macroParticleStartSpeedMultiplier = 1f;
        [SerializeField] private float _macroParticleStartSizeMultiplier = 1f;
        [SerializeField] private float _macroParticleEmissionMultiplier = 1f;

        [SerializeField] private float _microParticleStartSpeedMultiplier = 0.3f;
        [SerializeField] private float _microParticleStartSizeMultiplier = 0.3f;
        [SerializeField] private float _microParticleEmissionMultiplier = 3f;

        [Header("Cell proximity prompt")]
        // Squared distance, not linear - measured live via Physics2D.Distance that the player/cell colliders
        // touch at sqrMagnitude ~5.1-6.9 depending on approach angle, so this needs comfortable margin above that.
        [SerializeField] private float _cellProximityActivationSqrDistance = 10f;

        public float TransitionDurationSeconds => _transitionDurationSeconds;

        public Vector3 MacroPlayerScale => _macroPlayerScale;
        public Vector3 MicroPlayerScale => _microPlayerScale;

        public float MacroCameraOrthographicSize => _macroCameraOrthographicSize;
        public float MicroCameraOrthographicSize => _microCameraOrthographicSize;

        public float MacroExitOffsetDistance => _macroExitOffsetDistance;

        public float MicroPlayerAudioVolume => _microPlayerAudioVolume;
        public float RestoredPlayerAudioVolume => _restoredPlayerAudioVolume;

        public Vector2 CellMacroMaxVelocity => _cellMacroMaxVelocity;

        public float MacroSpeedScale => _macroSpeedScale;
        public float MicroSpeedScale => _microSpeedScale;

        public Vector2 ParticleMinMaxEmissionRate => _particleMinMaxEmissionRate;

        public float MacroParticleStartSpeedMultiplier => _macroParticleStartSpeedMultiplier;
        public float MacroParticleStartSizeMultiplier => _macroParticleStartSizeMultiplier;
        public float MacroParticleEmissionMultiplier => _macroParticleEmissionMultiplier;

        public float MicroParticleStartSpeedMultiplier => _microParticleStartSpeedMultiplier;
        public float MicroParticleStartSizeMultiplier => _microParticleStartSizeMultiplier;
        public float MicroParticleEmissionMultiplier => _microParticleEmissionMultiplier;

        public float CellProximityActivationSqrDistance => _cellProximityActivationSqrDistance;
    }
}
