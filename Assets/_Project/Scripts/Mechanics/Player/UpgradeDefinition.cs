using UnityEngine;

namespace _Project.Mechanics.Player
{
    /// <summary>
    /// One ScriptableObject per <see cref="UpgradeStat"/>, replacing the old inline enum-indexed literals
    /// scattered across PlayerBodyController.OnUpgrade / PlayerDamageable.OnUpgrade / PlayerTurretController -
    /// see REFACTORING_PLAN.md section 3.1 ("UpgradeDefinition_&lt;UpgradeType&gt; (6 асетів замість inline
    /// switch/enum-констант)").
    /// </summary>
    [CreateAssetMenu(
        menuName = "Configurations/Player/" + nameof(UpgradeDefinition),
        fileName = nameof(UpgradeDefinition) + "_New")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        [SerializeField] private UpgradeStat _stat;
        [SerializeField] private string _displayName;

        // How much the stat's effective value moves per upgrade level - read PlayerStatsService's
        // "Effective*" properties for the exact formula each stat plugs this into. Mapping to the old formulas:
        //   MoveSpeed  (0.5)  - old PlayerBodyController.OnUpgrade: `_body.SetSpeed(_body.GetSpeed() + 0.5f)`.
        //   Mobility   (10)   - old PlayerBodyController.OnUpgrade: `_body.SetAngularSpeed(... + 10)`.
        //   AttackSpeed(0.25) - old PlayerTurretController: `_baseReloadTime - level * _scaleByLevelUpgrade`
        //                       (linear in the raw upgrade level, not level-1 - the one stat whose old formula
        //                       already had a named per-level field).
        //   Armor      (1)    - old PlayerDamageable.OnUpgrade: `if (Armor < 8) Armor += 1`.
        //   Damage     (0.5)  - old Bullet.cs: `_damage * (level / 2)` using C# INTEGER division. Setting this to
        //                       0.5 and computing `Mathf.FloorToInt(level * EffectPerLevel)` reproduces that
        //                       integer division exactly, so one generic formula covers it without a special case.
        //   Health     (1)    - old PlayerDamageable.OnUpgrade: `if (upgrade == PLAYER_HP && health < max) health += 1`.
        [SerializeField] private float _effectPerLevel;

        public UpgradeStat Stat => _stat;
        public string DisplayName => _displayName;
        public float EffectPerLevel => _effectPerLevel;
    }
}
