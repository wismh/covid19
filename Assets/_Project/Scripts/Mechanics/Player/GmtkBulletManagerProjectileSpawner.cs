using UnityEngine;

namespace _Project.Mechanics.Player
{
    /// <summary>
    /// TODO(projectile migration): temporary bridge to the old GMTK.BulletManager pooled-bullet system
    /// (Assets/GMTK/Scripts/Behaviors/Bullet/BulletManager.cs + Bullet.cs). REFACTORING_PLAN.md sections 4 and 5
    /// deliberately keep BulletManager unchanged ("вже коректно - залишити"), so this bridge is expected to
    /// remain the long-term integration point, not just a stopgap removed in a later phase.
    ///
    /// Damage seam: the spawned GMTK.Bullet still computes its own damage internally from the OLD
    /// GMTK.Services.PlayerStats via GMTK.Services.ServiceLocator (see Bullet.cs OnTriggerEnter2D:
    /// `_damage * (playerStats.GetLevelUpgrade(PLAYER_DAMAGE) / 2)`), NOT from this project's
    /// PlayerStatsService.EffectiveDamage. Plugging the new stats service in requires porting Bullet.cs itself
    /// off ServiceLocator/GMTK.Services.PlayerStats, which is a projectile-system migration out of scope for
    /// Phase 2. Practically, this means the bridge only produces correct damage in a scene where the legacy GMTK
    /// ServiceLocator + GMTK.Services.PlayerStats are also bootstrapped (i.e. the old GamePlayState/BootstrapState
    /// flow) - PlayerStatsService.EffectiveDamage is where a ported Bullet would read damage from instead.
    ///
    /// Also out of scope here (see Bullet.cs / PlayerParticlesController.cs): Bullet's macro/micro world
    /// rescale-and-slow coupling to GamePlayState.State - that's Phase 4's "Built to Scale" work.
    /// </summary>
    public sealed class GmtkBulletManagerProjectileSpawner : MonoBehaviour, IProjectileSpawner
    {
        [SerializeField] private GMTK.BulletManager _bulletManager;

        public void SpawnProjectile(Vector3 position, Vector3 direction)
        {
            _bulletManager.SpawnBullet(position, direction);
        }
    }
}
