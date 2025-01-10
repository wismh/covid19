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
    /// <summary>
    /// Umbrella MonoInstaller for GameplayScene's SceneContext - holds the Mechanics-POCO config assets and calls
    /// out to the small per-feature XxxInstaller.Install(Container, ...) calls, the same shape tactics-cards'
    /// GamePlayContextInstaller uses. Each XxxInstaller is a real Zenject <c>Installer&lt;...&gt;</c> subclass
    /// (constructor-injected params, <c>Container</c>/<c>InstallBindings()</c> from the base class) - not a bare
    /// static helper class - matching tactics-cards' own installers (PlaceablePoolsInstaller,
    /// ArenaMatchBindingsInstaller, GameStateInstaller, etc.) exactly; `Install(Container, ...)` itself is
    /// Zenject's own inherited static factory method, not a hand-rolled one (2026-09-20 correction - the
    /// installers here used to be bare static classes, an incorrect reading of the tactics-cards pattern).
    /// Replaces MainSceneComposition.BuildMechanicsPocos() (which used to `new` every POCO by hand) -
    /// MainSceneComposition now receives them via [Inject] instead. UI screens/IScreenManager are bound
    /// separately by UiInstaller (also attached to the same SceneContext).
    ///
    /// 2026-09-20 "MainCompositionRoot split into installers": also calls
    /// <see cref="GameplayEventWiringInstaller"/>, which binds the cross-system event-wiring drivers split out
    /// of MainSceneComposition.WireCrossSystemEvents - see that installer's own doc comment.
    /// </summary>
    public class GameplaySceneInstaller : MonoInstaller
    {
        [Header("Player")]
        [SerializeField] private PlayerConfig _playerConfig;
        [SerializeField] private List<UpgradeDefinition> _upgradeDefinitions;
        [SerializeField] private Bullet _bulletPrefab;

        [Header("Cells")]
        [SerializeField] private CellConfig _cellConfig;

        [Header("Micro viruses (in-cell) - pooled prefabs, see MicroVirusesInstaller")]
        [SerializeField] private MicroVirusController _orangeVirusPrefab;
        [SerializeField] private MicroVirusController _cucumberVirusPrefab;
        [SerializeField] private MicroVirusController _spinerVirusPrefab;
        [SerializeField] private VirusBullet _virusBulletPrefab;

        [Header("Macro viruses")]
        [SerializeField] private MacroVirusConfig _macroVirusConfig;
        [SerializeField] private MacroVirusController _macroVirusPrefab;
        [SerializeField] private MacroVirusController _macroGlistPrefab;

        [Header("Pickups (medical pills) - pooled prefab, see PickupsInstaller")]
        [SerializeField] private PickupsConfig _pickupsConfig;
        [SerializeField] private MedicalPickup _medicalPrefab;

        [Header("Waves - shared by MacroVirusSpawner and CellSpawner")]
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

            // IPlayerTargetProvider wraps PlayerMovement.transform - needs PlayerMovement already bound above.
            Container.Bind<IPlayerTargetProvider>()
                .FromMethod(ctx => new PlayerTargetProvider(ctx.Container.Resolve<PlayerMovement>().transform))
                .AsSingle();

            // Plain Bind, not BindInterfacesAndSelfTo: GameAudioService's IDisposable lifecycle stays owned by
            // MainSceneComposition.OnDestroy() (its existing manual Dispose() call), not Zenject's container
            // teardown - avoids a double-dispose between the two.
            Container.Bind<GameAudioCatalog>().FromInstance(_gameAudioCatalog).AsSingle();
            Container.Bind<GameAudioService>().AsSingle();

            GameplayEventWiringInstaller.Install(Container);
        }
    }
}
