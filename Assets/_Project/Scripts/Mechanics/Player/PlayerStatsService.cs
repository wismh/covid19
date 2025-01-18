using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Project.Mechanics.Player
{
    /// <summary>XP/level and per-stat upgrade tracking, plus the effective-stat formulas derived from them.</summary>
    public sealed class PlayerStatsService
    {
        private readonly PlayerConfig _config;
        private readonly Dictionary<UpgradeStat, UpgradeDefinition> _definitionsByStat;
        private readonly Dictionary<UpgradeStat, int> _upgradeLevels = new();

        private int _experience;
        private int _maxExperience;
        private int _level = 1;

        public PlayerStatsService(PlayerConfig config, IReadOnlyList<UpgradeDefinition> upgradeDefinitions)
        {
            _config = config;
            _maxExperience = config.StartingMaxExperience;

            _definitionsByStat = new Dictionary<UpgradeStat, UpgradeDefinition>();
            if (upgradeDefinitions != null)
            {
                foreach (UpgradeDefinition definition in upgradeDefinitions)
                {
                    if (definition != null)
                        _definitionsByStat[definition.Stat] = definition;
                }
            }
        }

        public int Level => _level;
        public int Experience => _experience;
        public int MaxExperience => _maxExperience;

        /// <summary>Raised whenever XP changes (experience, maxExperience), whether or not it caused a level-up.</summary>
        public event Action<int, int> OnExperienceChanged;

        /// <summary>Raised only on the tick the player's overall level increases, with the new level.</summary>
        public event Action<int> OnLevelUp;

        /// <summary>Raised whenever a specific stat's upgrade level increases: (stat, new level for that stat).</summary>
        public event Action<UpgradeStat, int> OnUpgraded;

        /// <summary>Fires alongside every upgrade pick (not just Health) whenever the player's overall level is
        /// even - an original-game rule, not a bug.</summary>
        public event Action<int> OnBonusHealthGranted;

        public int GetUpgradeLevel(UpgradeStat stat) =>
            _upgradeLevels.TryGetValue(stat, out int level) ? level : 1;

        public void AddExperience()
        {
            _experience += 1;
            bool leveledUp = false;

            if (_experience >= _maxExperience)
            {
                _level += 1;
                _experience = 0;
                _maxExperience = (int)(_maxExperience * _config.ExperienceGrowthMultiplierPerLevel);
                leveledUp = true;
            }

            OnExperienceChanged?.Invoke(_experience, _maxExperience);
            if (leveledUp)
                OnLevelUp?.Invoke(_level);
        }

        public void ApplyUpgrade(UpgradeStat stat)
        {
            int newLevel = GetUpgradeLevel(stat) + 1;
            _upgradeLevels[stat] = newLevel;

            OnUpgraded?.Invoke(stat, newLevel);

            if (_level % 2 == 0)
                OnBonusHealthGranted?.Invoke(_config.BonusHealthPerEvenLevel);
        }

        // Effective stats: pure functions of (config, upgrade level, upgrade definitions).

        public float EffectiveMoveSpeed =>
            _config.BaseMoveSpeed + EffectPerLevel(UpgradeStat.MoveSpeed) * (GetUpgradeLevel(UpgradeStat.MoveSpeed) - 1);

        public float EffectiveTurnRate =>
            _config.BaseMobility + EffectPerLevel(UpgradeStat.Mobility) * (GetUpgradeLevel(UpgradeStat.Mobility) - 1);

        /// <summary>
        /// Compounding formula: each AttackSpeed level multiplies fire rate by (1 + EffectPerLevel) over the
        /// previous level, i.e. reload = BaseReloadTime / (1 + EffectPerLevel)^level, floored at MinReloadTime.
        /// EffectPerLevel is a fraction (0.2 = +20% fire rate per level), not a flat seconds-per-level value.
        /// </summary>
        public float EffectiveReloadTime
        {
            get
            {
                int level = GetUpgradeLevel(UpgradeStat.AttackSpeed);
                float reload = _config.BaseReloadTime / Mathf.Pow(1f + EffectPerLevel(UpgradeStat.AttackSpeed), level);
                return Mathf.Max(reload, _config.MinReloadTime);
            }
        }

        public int EffectiveDamage
        {
            get
            {
                int level = GetUpgradeLevel(UpgradeStat.Damage);
                int scaled = _config.BaseDamage * Mathf.FloorToInt(level * EffectPerLevel(UpgradeStat.Damage));
                return Mathf.Max(1, scaled);
            }
        }

        /// <summary>Nominal armor level-up would grant, capped at MaxArmor. NOT the authoritative runtime armor -
        /// unlike the other effective stats, armor is consumable at runtime (a damage-ignore proc spends a
        /// point), so the live value lives on PlayerHealth, seeded from this and mutated independently.</summary>
        public int EffectiveArmor =>
            Mathf.Min(
                _config.BaseArmor + Mathf.RoundToInt(EffectPerLevel(UpgradeStat.Armor) * (GetUpgradeLevel(UpgradeStat.Armor) - 1)),
                _config.MaxArmor);

        /// <summary>The upgrade ceiling current health moves toward - distinct from PlayerConfig.StartingHealth
        /// (the initial value).</summary>
        public int EffectiveMaxHealth => _config.BaseMaxHealth;

        private float EffectPerLevel(UpgradeStat stat) =>
            _definitionsByStat.TryGetValue(stat, out UpgradeDefinition definition) ? definition.EffectPerLevel : 0f;
    }
}
