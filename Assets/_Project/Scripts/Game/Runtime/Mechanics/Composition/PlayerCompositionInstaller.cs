using System.Collections.Generic;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Combat;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Micro;

namespace _Project.Mechanics.Composition
{
    public class PlayerCompositionInstaller : MonoInstaller
    {
        [SerializeField] private PlayerConfig _playerConfig;
        [SerializeField] private List<UpgradeDefinition> _upgradeDefinitions;
        [SerializeField] private Bullet _bulletPrefab;

        public override void InstallBindings()
        {
            PlayerInstaller.Install(Container, _playerConfig, _upgradeDefinitions, _bulletPrefab);

            Container.Bind<BulletManager>().FromComponentInHierarchy().AsSingle();

            // Needs PlayerMovement already bound above.
            Container.Bind<IPlayerTargetProvider>()
                .FromMethod(ctx => new PlayerTargetProvider(ctx.Container.Resolve<PlayerMovement>().transform))
                .AsSingle();

            Container.BindInterfacesAndSelfTo<PlayerCombatSpawnerDriver>().AsSingle();
        }
    }
}
