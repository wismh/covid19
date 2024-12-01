using Project.Core.ApplicationLifecycle;
using Zenject;

namespace _Project.Save
{
    /// <summary>
    /// Explicit, LifeController-driven "flush on quit" safety net for the save system - the `SaveTrigger`
    /// REFACTORING_PLAN.md sections 1/4/5 name alongside GameSaveState/GameSaveModuleInstaller.
    ///
    /// --- Why this exists separately from GameSaveService's own IDisposable.Dispose() ---
    /// See GameSaveService's class doc comment: Zenject only calls a bound IDisposable's Dispose() when its
    /// ProjectContext's own GameObject.OnDestroy() actually runs, which is not guaranteed on every quit path
    /// (WebGL - one of this project's two REFACTORING_PLAN.md section 7 build targets - is notorious for
    /// OnApplicationQuit/OnDestroy not firing reliably before the tab/process tears down). `LifeController`
    /// (Assets/_Lumenwake/Scripts/Runtime/ApplicationLifecycleModule/LifeController.cs) exposes its own
    /// `OnQuit` event, raised from Unity's `OnApplicationQuit()` MonoBehaviour callback directly - a second,
    /// independent signal that does not depend on Zenject's own container-teardown ordering. Subscribing to it
    /// here means the save file gets an explicit immediate flush attempt through a path that fires even if
    /// Zenject's IDisposable-based flush does not.
    ///
    /// --- Why this is NOT also debouncing a pile of gameplay events ---
    /// The phase brief describes td-over-unity's SaveTrigger as debouncing `RequestSave()` calls on "whatever
    /// change events make sense to persist." In THIS codebase, `Project.Core.SaveServiceModule.SaveService&lt;
    /// TState&gt;.RequestSave()` already implements its own 300ms debounce internally (see SaveService.cs -
    /// every call to RequestSave() cancels and restarts a single pending write, it does not queue N separate
    /// writes) - so `GameSaveService`'s own setters (`SetMasterVolume`/`SetMusicVolume`/`SetSfxVolume`) and
    /// record methods (`RecordSurvivalTime`/`RecordPlayerLevel`) already call `RequestSave()` directly and get
    /// debouncing for free, without a separate class re-implementing it. There is no natural "economic/progress
    /// event" source to subscribe to yet either: `GameAudioService`/`PlayerStatsService`/`WorldScaleController`
    /// are all hand-composed Mechanics/* POCOs per REFACTORING_PLAN.md section 4's own rule (not Zenject-bound),
    /// and no gameplay composition root exists yet to construct them and hand this class references to their
    /// events - inventing that coupling here would mean either reaching into Mechanics/* from Save/* (wrong
    /// direction of dependency for a platform service) or speculatively wiring events nothing calls yet. This
    /// class is therefore scoped to exactly the one connection point that already exists cleanly today
    /// (LifeController, already bound in ProjectContextInstaller via `FromComponentInHierarchy()`) - the
    /// gameplay-event debounce triggers remain a documented gap for whoever builds the future
    /// GameplayComposition/MainSceneComposition root, at which point they can call `IGameSaveService`'s own
    /// Record*/Set* methods directly from the relevant event handlers exactly as HudScreen/GameOverScreen
    /// already do for other Mechanics/* events - no additional SaveTrigger plumbing is needed for that.
    /// </summary>
    public sealed class SaveTrigger : IInitializable, System.IDisposable
    {
        private readonly IGameSaveService _saveService;
        private readonly LifeController _lifeController;

        public SaveTrigger(IGameSaveService saveService, LifeController lifeController)
        {
            _saveService = saveService;
            _lifeController = lifeController;
        }

        public void Initialize()
        {
            _lifeController.OnQuit += HandleQuit;
        }

        public void Dispose()
        {
            _lifeController.OnQuit -= HandleQuit;
        }

        private void HandleQuit()
        {
            _saveService.SaveImmediate();
        }
    }
}
