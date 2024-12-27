using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Project.Mechanics.Player
{
    /// <summary>
    /// Plain C# service (POCO), not a MonoBehaviour - matches the Mechanics/* manual-composition philosophy from
    /// REFACTORING_PLAN.md section 4 (gameplay systems are composed by hand, e.g. `new PlayerStatsService(...)`,
    /// rather than threaded through Zenject like the platform services bound in ProjectContextInstaller). Ports
    /// GMTK.Services.PlayerStats (XP/level, Assets/GMTK/Scripts/Behaviors/Player/PlayerStats.cs) merged with the
    /// upgrade-application logic that used to live partly in PlayerStats.Upgrade() and partly in
    /// PlayerBodyController/PlayerDamageable's OnUpgrade handlers.
    ///
    /// Receives its <see cref="UpgradeDefinition"/> list via a plain constructor parameter rather than a Zenject
    /// collection binding ([Inject] UpgradeDefinition[] _definitions). Per REFACTORING_PLAN.md section 4,
    /// gameplay systems under Mechanics/* are deliberately NOT threaded through the DI container the way
    /// platform services (Save/Audio/SceneLoader/Assets) are - so wiring a Zenject collection binding here would
    /// cut against that stated architecture decision. Whoever composes the gameplay scene (a future
    /// GameplayComposition/MainSceneComposition class, per the plan) is expected to pass the six
    /// UpgradeDefinition_* assets in directly - e.g. from a `[SerializeField] List&lt;UpgradeDefinition&gt;` on
    /// that composition root, the same way PlayerConfig itself would be assigned and passed along.
    /// </summary>
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

        /// <summary>
        /// Explicit port of a rule that was a hidden side effect in the old code: inside
        /// GMTK.PlayerDamageable.OnUpgrade, `if (_playerStats.Level % 2 == 0) _health += 1;` ran unconditionally
        /// for EVERY upgrade type, not just PLAYER_HP - so picking any upgrade while the player's overall level
        /// happened to be even granted a silent, uncapped bonus HP on top of whatever stat was actually picked.
        /// Kept here as a named, documented rule (PlayerConfig.BonusHealthPerEvenLevel) fired explicitly from
        /// ApplyUpgrade instead of being buried in an unrelated class - see TECHNICAL_AUDIT.md's callout on
        /// magic numbers / balance rules hidden in unrelated code paths.
        /// </summary>
        public event Action<int> OnBonusHealthGranted;

        public int GetUpgradeLevel(UpgradeStat stat) =>
            _upgradeLevels.TryGetValue(stat, out int level) ? level : 1;

        /// <summary>Old: GMTK.Services.PlayerStats.AddExperience() - XP curve grows maxExperience ×1.5 per level.</summary>
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

        /// <summary>Old: GMTK.Services.PlayerStats.Upgrade(UpgradeType).</summary>
        public void ApplyUpgrade(UpgradeStat stat)
        {
            int newLevel = GetUpgradeLevel(stat) + 1;
            _upgradeLevels[stat] = newLevel;

            OnUpgraded?.Invoke(stat, newLevel);

            // See OnBonusHealthGranted doc: this used to be a hidden side effect of ANY upgrade pick, now it's
            // an explicit, named step.
            if (_level % 2 == 0)
                OnBonusHealthGranted?.Invoke(_config.BonusHealthPerEvenLevel);
        }

        // --- Effective stats: pure functions of (config, upgrade level, upgrade definitions). ---
        // NOTE: MoveSpeed/Mobility were stateful accumulators in the old DriftMovableObject
        // (`SetSpeed(GetSpeed() + 0.5f)` on every OnUpgrade callback - a subscription PlayerBodyController never
        // unsubscribed, see TECHNICAL_AUDIT.md 2.1's "потенційний memory leak" callout). Recomputing them here
        // as pure functions of the stored upgrade level is mathematically equivalent to that old accumulation
        // AND removes the double-subscription/leak risk entirely.

        public float EffectiveMoveSpeed =>
            _config.BaseMoveSpeed + EffectPerLevel(UpgradeStat.MoveSpeed) * (GetUpgradeLevel(UpgradeStat.MoveSpeed) - 1);

        public float EffectiveTurnRate =>
            _config.BaseMobility + EffectPerLevel(UpgradeStat.Mobility) * (GetUpgradeLevel(UpgradeStat.Mobility) - 1);

        /// <summary>Old: `_baseReloadTime - GetLevelUpgrade(PLAYER_ATTACK_SPEED) * _scaleByLevelUpgrade`, floored to 0.25s.</summary>
        public float EffectiveReloadTime
        {
            get
            {
                int level = GetUpgradeLevel(UpgradeStat.AttackSpeed);
                float reload = _config.BaseReloadTime - EffectPerLevel(UpgradeStat.AttackSpeed) * level;
                return reload <= 0f ? _config.MinReloadTime : reload;
            }
        }

        /// <summary>
        /// Old: `_damage * (GetLevelUpgrade(PLAYER_DAMAGE) / 2)` using C# integer division (Bullet.cs
        /// OnTriggerEnter2D), minimum 1. This is the plug-in point for a future Bullet/projectile port - see
        /// GmtkBulletManagerProjectileSpawner's doc comment for why the OLD GMTK.Bullet still computes its own
        /// damage from the OLD GMTK.Services.PlayerStats instead of this property, for now.
        /// </summary>
        public int EffectiveDamage
        {
            get
            {
                int level = GetUpgradeLevel(UpgradeStat.Damage);
                int scaled = _config.BaseDamage * Mathf.FloorToInt(level * EffectPerLevel(UpgradeStat.Damage));
                return Mathf.Max(1, scaled);
            }
        }

        /// <summary>
        /// Level-derived nominal armor value (what upgrading Armor currently grants), capped at
        /// PlayerConfig.MaxArmor exactly like the old inline `Armor &lt; 8` check. NOT the authoritative runtime
        /// armor: unlike MoveSpeed/ReloadTime/Damage, armor is consumable at runtime (a damage-ignore proc spends
        /// 1 point - old PlayerDamageable.Damage: `Armor -= 1`), so the live value that decreases from combat
        /// lives on PlayerHealth, seeded from this property and mutated independently - exactly how
        /// GMTK.PlayerDamageable.Armor worked (a plain stateful int, never recomputed from level on every use).
        /// </summary>
        public int EffectiveArmor =>
            Mathf.Min(
                _config.BaseArmor + Mathf.RoundToInt(EffectPerLevel(UpgradeStat.Armor) * (GetUpgradeLevel(UpgradeStat.Armor) - 1)),
                _config.MaxArmor);

        /// <summary>
        /// Old: `_maxHealth` was a static ceiling itself (not derived from level) that current health moved
        /// toward via the HP upgrade card - `if (_health &lt; _maxHealth) _health += 1;` - so it's a pure
        /// PlayerConfig value with no per-level term. NOT the starting health - see PlayerConfig.StartingHealth/
        /// BaseMaxHealth doc comments for why those are two distinct original values (3 and 8 respectively).
        /// </summary>
        public int EffectiveMaxHealth => _config.BaseMaxHealth;

        private float EffectPerLevel(UpgradeStat stat) =>
            _definitionsByStat.TryGetValue(stat, out UpgradeDefinition definition) ? definition.EffectPerLevel : 0f;
    }
}
