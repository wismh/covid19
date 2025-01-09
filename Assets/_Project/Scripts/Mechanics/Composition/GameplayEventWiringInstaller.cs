using Zenject;

namespace _Project.Mechanics.Composition
{
    /// <summary>
    /// Binds the cross-system event-wiring drivers split out of MainSceneComposition.WireCrossSystemEvents
    /// (2026-09-20 "MainCompositionRoot split into installers" - see the individual driver classes' own doc
    /// comments for what each replaces). Follows the space-bac/tactics-cards convention of a plain static
    /// Install(Container) helper, called from <see cref="GameplaySceneInstaller"/> - the same shape
    /// PlayerInstaller/CellsInstaller/VirusesInstaller/etc. already use for their own bind-logic.
    ///
    /// Each driver is <c>IInitializable</c>/<c>IDisposable</c>: Zenject calls <c>Initialize()</c> on every scene
    /// binding before any scene MonoBehaviour's own <c>Awake()</c> runs (even earlier than the previous
    /// MainSceneComposition.Awake()-based wiring), and <c>Dispose()</c> on SceneContext teardown - the exact
    /// subscribe/unsubscribe symmetry MainSceneComposition.OnDestroy() used to hand-manage for these same
    /// subscriptions. Safe because every event source here (CellRegistry/MacroVirusRegistry/WorldScaleController
    /// are container-built POCOs; PlayerHealth/CellSpawner/MacroVirusSpawner are FromComponentInHierarchy
    /// bindings) exists and has live event fields the moment it's resolved, regardless of Unity's own
    /// Awake/Start ordering.
    ///
    /// Not every WireCrossSystemEvents concern moved here - see MainSceneComposition's own doc comment for the
    /// one piece (forced-macro-exit on cell death) that stayed behind because it needs the locally-owned,
    /// per-poll-tick-mutated WorldScaleTransitionContext, which is deliberately not a container binding.
    /// </summary>
    public static class GameplayEventWiringInstaller
    {
        public static void Install(DiContainer container)
        {
            container.BindInterfacesAndSelfTo<MacroVirusReplacementDriver>().AsSingle();
            container.BindInterfacesAndSelfTo<MacroVirusPauseDriver>().AsSingle();
            container.BindInterfacesAndSelfTo<VirusExperienceDriver>().AsSingle();
            container.BindInterfacesAndSelfTo<HudStatisticsDriver>().AsSingle();
            container.BindInterfacesAndSelfTo<GameOverDriver>().AsSingle();
        }
    }
}
