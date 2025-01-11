using System.Collections.Generic;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Audio;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Combat;
using _Project.Mechanics.Pickups;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Micro;
using _Project.Mechanics.Viruses.Macro;
using _Project.Mechanics.Waves;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Composition
{
    public class GameplaySceneInstaller : MonoInstaller
    {
        [Header("Player")]
        [SerializeField] private PlayerConfig _playerConfig;
        [SerializeField] private List<UpgradeDefinition> _upgradeDefinitions;
        [SerializeField] private Bullet _bulletPrefab;

        [Header("Cells")]
        [SerializeField] private CellConfig _cellConfig;

        [Header("Micro viruses")]
        [SerializeField] private MicroVirusController _orangeVirusPrefab;
        [SerializeField] private MicroVirusController _cucumberVirusPrefab;
        [SerializeField] private MicroVirusController _spinerVirusPrefab;
        [SerializeField] private VirusBullet _virusBulletPrefab;

        [Header("Macro viruses")]
        [SerializeField] private MacroVirusConfig _macroVirusConfig;
        [SerializeField] private MacroVirusController _macroVirusPrefab;
        [SerializeField] private MacroVirusController _macroGlistPrefab;

        [Header("Pickups")]
        [SerializeField] private PickupsConfig _pickupsConfig;
        [SerializeField] private MedicalPickup _medicalPrefab;

        [Header("Waves")]
        [SerializeField] private WaveScalingConfig _waveScalingConfig;

        [Header("World scale")]
        [SerializeField] private WorldScaleConfig _worldScaleConfig;

        [Header("Audio")]
        [SerializeField] private GameAudioCatalog _gameAudioCatalog;

        public override void InstallBindings()
        {
            PlayerInstaller.Install(Container, _playerConfig, _upgradeDefinitions, _bulletPrefab);
            CellsInstaller.Install(Container, _cellConfig);
            MicroVirusesInstaller.Install(Container, _orangeVirusPrefab, _cucumberVirusPrefab, _spinerVirusPrefab, _virusBulletPrefab);
            VirusesInstaller.Install(Container, _macroVirusConfig, _waveScalingConfig, _macroVirusPrefab, _macroGlistPrefab);
            WorldScaleInstaller.Install(Container, _worldScaleConfig);
            PickupsInstaller.Install(Container, _pickupsConfig, _medicalPrefab);

            // Needs PlayerMovement already bound above.
            Container.Bind<IPlayerTargetProvider>()
                .FromMethod(ctx => new PlayerTargetProvider(ctx.Container.Resolve<PlayerMovement>().transform))
                .AsSingle();

            // Plain Bind, not BindInterfacesAndSelfTo: GameAudioService's Dispose() is called manually
            // elsewhere, not by container teardown - avoids a double-dispose.
            Container.Bind<GameAudioCatalog>().FromInstance(_gameAudioCatalog).AsSingle();
            Container.Bind<GameAudioService>().AsSingle();

            GameplayEventWiringInstaller.Install(Container);
        }
    }
}
