using System.Collections.Generic;
using Zenject;

namespace _Project.Mechanics.Player
{
    /// <summary>
    /// Binds the player POCO/config and the statically-placed Player-prefab MonoBehaviours (so other
    /// installers/consumers can resolve them via [Inject] instead of a hand-built reference), for
    /// GameplaySceneInstaller. Follows the space-bac/tactics-cards convention: a plain static Install(Container, ...)
    /// helper, not a MonoInstaller of its own.
    /// </summary>
    public static class PlayerInstaller
    {
        public static void Install(DiContainer container, PlayerConfig playerConfig, IReadOnlyList<UpgradeDefinition> upgradeDefinitions)
        {
            container.Bind<PlayerConfig>().FromInstance(playerConfig).AsSingle();
            container.Bind<PlayerStatsService>().AsSingle().WithArguments(playerConfig, upgradeDefinitions);

            container.Bind<PlayerMovement>().FromComponentInHierarchy().AsSingle();
            container.Bind<PlayerTurret>().FromComponentInHierarchy().AsSingle();
            container.Bind<PlayerHealth>().FromComponentInHierarchy().AsSingle();
        }
    }
}
