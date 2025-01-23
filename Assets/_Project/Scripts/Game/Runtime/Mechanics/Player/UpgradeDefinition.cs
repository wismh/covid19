using UnityEngine;

namespace _Project.Mechanics.Player
{
    /// <summary>One ScriptableObject per <see cref="UpgradeStat"/>.</summary>
    [CreateAssetMenu(
        menuName = "Configurations/Player/" + nameof(UpgradeDefinition),
        fileName = nameof(UpgradeDefinition) + "_New")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        [SerializeField] private UpgradeStat _stat;
        [SerializeField] private string _displayName;

        // How much the stat's effective value moves per upgrade level - see PlayerStatsService's "Effective*"
        // properties for the exact formula each stat plugs this into. Damage uses Mathf.FloorToInt(level *
        // EffectPerLevel) with EffectPerLevel = 0.5, which reproduces integer division by 2.
        [SerializeField] private float _effectPerLevel;

        public UpgradeStat Stat => _stat;
        public string DisplayName => _displayName;
        public float EffectPerLevel => _effectPerLevel;
    }
}
