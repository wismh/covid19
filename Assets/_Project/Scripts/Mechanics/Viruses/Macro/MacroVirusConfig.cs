using UnityEngine;

namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>
    /// Every base-tunable number found while porting the old GMTK macro-virus systems (Assets/GMTK/Scripts/
    /// Behaviors/MacroVirus/*, Assets/GMTK/Scripts/Behaviors/DriftMovableObject.cs). Movement numbers were read
    /// directly from Assets/GMTK/Prefabs/BigBob.prefab's DriftMovableObject component (PinkGlist.prefab has the
    /// identical DriftMovableObject values - both variants move the same; only health and penetration delay
    /// differ per-variant, see MacroVirusController's per-instance fields for why those stay outside this shared
    /// config).
    /// </summary>
    [CreateAssetMenu(
        menuName = "Configurations/Viruses/" + nameof(MacroVirusConfig),
        fileName = nameof(MacroVirusConfig) + "_Default")]
    public sealed class MacroVirusConfig : ScriptableObject
    {
        [Header("Movement (ported from GMTK.DriftMovableObject, values from BigBob.prefab / PinkGlist.prefab)")]
        [SerializeField] private float _baseMoveSpeed = 2f;
        [SerializeField] private float _acceleration = 0.5f;
        [SerializeField] private float _friction = 0.02f;
        [SerializeField] private float _angularSpeed = 80f;
        [SerializeField] private float _angularAcceleration = 5f;
        [SerializeField] private float _angularFriction = 0.8f;

        [Header("Behavior throttles (ported from GMTK.VirusBehaviors.MoveToCell/AttackCell)")]
        // Old: MoveToCell.FixedUpdate -> `_body.Move(1f);` (full thrust while chasing a cell).
        [SerializeField] private float _moveToCellThrottle = 1f;

        // Old: AttackCell.FixedUpdate -> `_body.Move(0.05f);` (slow orbit/ram once touching a cell).
        [SerializeField] private float _attackOrbitThrottle = 0.05f;

        [Header("Penetration (ported from GMTK.VirusBehaviors.PenetrationIntoCell)")]
        // Old: PenetrationIntoCell.k_penetrationDuration (private const, 0.5f).
        [SerializeField] private float _penetrationDurationSeconds = 0.5f;

        // Old: PenetrationIntoCell.k_scaleFactor (private const, 0.5f) - DOScale target = originalScale * this.
        [SerializeField] private float _penetrationScaleFactor = 0.5f;

        public float BaseMoveSpeed => _baseMoveSpeed;
        public float Acceleration => _acceleration;
        public float Friction => _friction;
        public float AngularSpeed => _angularSpeed;
        public float AngularAcceleration => _angularAcceleration;
        public float AngularFriction => _angularFriction;

        public float MoveToCellThrottle => _moveToCellThrottle;
        public float AttackOrbitThrottle => _attackOrbitThrottle;

        public float PenetrationDurationSeconds => _penetrationDurationSeconds;
        public float PenetrationScaleFactor => _penetrationScaleFactor;
    }
}
