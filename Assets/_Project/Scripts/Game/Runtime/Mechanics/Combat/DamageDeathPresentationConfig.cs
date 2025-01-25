using UnityEngine;

namespace _Project.Mechanics.Combat
{
    /// <summary>Damage-flash/death-animation timing shared by Player/MacroVirus/MicroVirus - identical across all
    /// of them, so it lives on one shared asset instead of being duplicated per-entity config.</summary>
    [CreateAssetMenu(
        menuName = "Configurations/Combat/" + nameof(DamageDeathPresentationConfig),
        fileName = nameof(DamageDeathPresentationConfig) + "_Default")]
    public sealed class DamageDeathPresentationConfig : ScriptableObject
    {
        [SerializeField] private float _damageFlashDurationSeconds = 0.1f;
        [SerializeField] private float _deathAnimationDelaySeconds = 0.2f;

        public float DamageFlashDurationSeconds => _damageFlashDurationSeconds;
        public float DeathAnimationDelaySeconds => _deathAnimationDelaySeconds;
    }
}
