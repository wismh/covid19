using System.Collections.Generic;
using UnityEngine;
using Zenject;
using _Project.Mechanics.Audio;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.InCell;
using _Project.Mechanics.Viruses.Macro;
using _Project.Mechanics.Waves;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Composition
{
    /// <summary>
    /// Umbrella MonoInstaller for SampleScene's SceneContext - holds the Mechanics-POCO config assets and calls
    /// out to the small per-feature XxxInstaller.Install(Container, ...) static helpers, the same shape
    /// tactics-cards' GamePlayContextInstaller uses. Replaces MainSceneComposition.BuildMechanicsPocos() (which
    /// used to `new` every POCO by hand) - MainSceneComposition now receives them via [Inject] instead.
    /// UI screens/IScreenManager are bound separately by UiInstaller (also attached to the same SceneContext).
    /// </summary>
    public class GameplaySceneInstaller : MonoInstaller
    {
        [Header("Player")]
        [SerializeField] private PlayerConfig _playerConfig;
        [SerializeField] private List<UpgradeDefinition> _upgradeDefinitions;

        [Header("Cells")]
        [SerializeField] private CellConfig _cellConfig;

        [Header("Macro viruses")]
        [SerializeField] private MacroVirusConfig _macroVirusConfig;

        [Header("Waves - shared by MacroVirusSpawner and CellSpawner")]
        [SerializeField] private WaveScalingConfig _waveScalingConfig;

        [Header("World scale")]
        [SerializeField] private WorldScaleConfig _worldScaleConfig;

        [Header("Audio")]
        [SerializeField] private GameAudioCatalog _gameAudioCatalog;

        public override void InstallBindings()
        {
            PlayerInstaller.Install(Container, _playerConfig, _upgradeDefinitions);
            CellsInstaller.Install(Container, _cellConfig);
            VirusesInstaller.Install(Container, _macroVirusConfig, _waveScalingConfig);
            WorldScaleInstaller.Install(Container, _worldScaleConfig);

            // IPlayerTargetProvider wraps PlayerMovement.transform - needs PlayerMovement already bound above.
            Container.Bind<IPlayerTargetProvider>()
                .FromMethod(ctx => new PlayerTargetProvider(ctx.Container.Resolve<PlayerMovement>().transform))
                .AsSingle();

            // Plain Bind, not BindInterfacesAndSelfTo: GameAudioService's IDisposable lifecycle stays owned by
            // MainSceneComposition.OnDestroy() (its existing manual Dispose() call), not Zenject's container
            // teardown - avoids a double-dispose between the two.
            Container.Bind<GameAudioCatalog>().FromInstance(_gameAudioCatalog).AsSingle();
            Container.Bind<GameAudioService>().AsSingle();
        }
    }
}
