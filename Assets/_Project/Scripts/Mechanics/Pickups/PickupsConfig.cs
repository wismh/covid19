using UnityEngine;

namespace _Project.Mechanics.Pickups
{
    /// <summary>
    /// Every base-tunable number found across MedicalPickup/MedicalSpawner/VitaminPickup (ports of GMTK.Medical/
    /// MedicalSpawner/Vitamin, Assets/GMTK/Scripts/Behaviors/Medicals/*) - previously scattered as local
    /// [SerializeField]s on each of the three prefab components instead of a single config asset (2026-09-19
    /// hardcode audit). Values below match those components' own pre-existing field defaults exactly, not
    /// guessed.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Configurations/Pickups/" + nameof(PickupsConfig),
        fileName = nameof(PickupsConfig) + "_Default")]
    public sealed class PickupsConfig : ScriptableObject
    {
        [Header("Medical pill (ported from GMTK.Medical)")]
        [SerializeField] private int _medicalHealth = 1;
        [SerializeField] private float _breakDelaySeconds = 1f;

        // Safety fallback in MedicalPickup.ReleaseVitaminsRoutine: destroys the pill even if a vitamin never
        // arrives (stuck/missed), so a pill can't stay orphaned forever.
        [SerializeField] private float _safetyTimeoutSeconds = 20f;

        [SerializeField] private float _destroyDelayWithAudioSeconds = 0.5f;
        [SerializeField] private float _destroyDelayWithoutAudioSeconds = 0.1f;

        [Header("Medical spawner (ported from GMTK.MedicalSpawner)")]
        [SerializeField] private Bounds _spawnBounds = new Bounds(Vector3.zero, new Vector3(24f, 54f, 0f));
        [SerializeField] private float _spawnIntervalSeconds = 15f;
        [SerializeField] private int _numToSpawnPerInterval = 3;
        [SerializeField] private int _maxActiveMedicals = 10;

        [Header("Vitamin flight (ported from GMTK.Vitamin)")]
        [SerializeField] private float _vitaminDistanceToEnd = 0.25f;
        [SerializeField] private float _vitaminSpeed = 9f;
        [SerializeField] private float _vitaminAcceleration = 12f;
        [SerializeField] private float _vitaminImpulseDurationSeconds = 0.25f;
        [SerializeField] private float _vitaminImpulseSpeed = 3.5f;

        // Fallback minimum arrival-distance-squared, independent of _vitaminDistanceToEnd - a safety net for a
        // degenerate config value rather than a real design knob.
        [SerializeField] private float _vitaminMinArrivalDistanceSqr = 0.05f;

        public int MedicalHealth => _medicalHealth;
        public float BreakDelaySeconds => _breakDelaySeconds;
        public float SafetyTimeoutSeconds => _safetyTimeoutSeconds;
        public float DestroyDelayWithAudioSeconds => _destroyDelayWithAudioSeconds;
        public float DestroyDelayWithoutAudioSeconds => _destroyDelayWithoutAudioSeconds;

        public Bounds SpawnBounds => _spawnBounds;
        public float SpawnIntervalSeconds => _spawnIntervalSeconds;
        public int NumToSpawnPerInterval => _numToSpawnPerInterval;
        public int MaxActiveMedicals => _maxActiveMedicals;

        public float VitaminDistanceToEnd => _vitaminDistanceToEnd;
        public float VitaminSpeed => _vitaminSpeed;
        public float VitaminAcceleration => _vitaminAcceleration;
        public float VitaminImpulseDurationSeconds => _vitaminImpulseDurationSeconds;
        public float VitaminImpulseSpeed => _vitaminImpulseSpeed;
        public float VitaminMinArrivalDistanceSqr => _vitaminMinArrivalDistanceSqr;
    }
}
