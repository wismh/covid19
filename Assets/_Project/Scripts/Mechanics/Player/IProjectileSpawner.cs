using UnityEngine;

namespace _Project.Mechanics.Player
{
    /// <summary>
    /// Small seam between PlayerTurret and whatever actually spawns/pools projectiles, so PlayerTurret doesn't
    /// depend on a concrete pooling implementation. Phase 2 is scoped to pure C# + config assets only (no
    /// prefabs), so building a brand-new projectile/pooling system is explicitly out of scope here - see
    /// GmtkBulletManagerProjectileSpawner for the concrete bridge to the existing, already-correct
    /// GMTK.BulletManager (REFACTORING_PLAN.md keeps it unchanged: "UnityEngine.Pool.ObjectPool&lt;Transform&gt;
    /// ... вже коректно - залишити").
    /// </summary>
    public interface IProjectileSpawner
    {
        void SpawnProjectile(Vector3 position, Vector3 direction);
    }
}
