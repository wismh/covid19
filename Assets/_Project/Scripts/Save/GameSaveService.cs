using Project.Core.SaveServiceModule;
using UnityEngine;
using Zenject;

namespace _Project.Save
{
    /// <summary>
    /// Zenject-bound implementation of <see cref="IGameSaveService"/>, wrapping the generic
    /// <c>ISaveService&lt;GameSaveState&gt;</c> (bound by <see cref="GameSaveModuleInstaller"/>). Unlike
    /// Mechanics/* classes (PlayerStatsService, GameAudioService, WorldScaleController), this one IS threaded
    /// through the Zenject container - per REFACTORING_PLAN.md section 4, Save is explicitly listed alongside
    /// Audio/SceneLoader/Assets as a "platform/cross-cutting service" that goes through DI, not a hand-composed
    /// gameplay system.
    ///
    /// --- Lifecycle: IInitializable / IDisposable ---
    /// <c>Project.Core.SaveServiceModule.SaveService&lt;TState&gt;</c> itself does NOT implement Zenject's
    /// <c>IInitializable</c>/<c>IDisposable</c> (confirmed by reading SaveService.cs directly - it only
    /// implements the plain <c>ISaveService&lt;TState&gt;</c> interface, with explicit <c>Load()</c>/
    /// <c>LoadAsync()</c>/<c>SaveImmediate()</c>/<c>RequestSave()</c> methods callers must invoke themselves).
    /// So this wrapper class is where the "space-bac auto-load on container build / auto-persist on dispose"
    /// pattern REFACTORING_PLAN.md section 4 describes actually gets implemented: <see cref="Initialize"/> calls
    /// the underlying service's synchronous <see cref="ISaveService{TState}.Load"/> once, up front, so every
    /// getter below can assume state is already loaded without needing to be async itself; <see cref="Dispose"/>
    /// calls <see cref="ISaveService{TState}.SaveImmediate"/> as a best-effort final flush of anything still
    /// sitting in the underlying service's 300ms debounce window when the container tears down.
    ///
    /// This IDisposable-driven flush is intentionally treated as a BEST EFFORT, not the sole quit-safety net -
    /// see <see cref="SaveTrigger"/>'s own doc comment for why an explicit <c>LifeController.OnQuit</c>
    /// subscription exists as a second, more platform-robust path to the same <see cref="SaveImmediate"/> call
    /// (Zenject's ProjectContext only reliably calls Dispose on its bound IDisposables when its own
    /// GameObject's OnDestroy actually runs, which is not guaranteed on every platform/quit path - WebGL in
    /// particular, per REFACTORING_PLAN.md section 7's build target list, is notorious for OnApplicationQuit/
    /// OnDestroy not firing before the tab/process is torn down).
    /// </summary>
    public sealed class GameSaveService : IGameSaveService, IInitializable, System.IDisposable
    {
        private readonly ISaveService<GameSaveState> _saveService;

        public GameSaveService(ISaveService<GameSaveState> saveService)
        {
            _saveService = saveService;
        }

        /// <summary>Loads the save file synchronously so every getter/setter below can rely on state being
        /// ready by the time any other Zenject-bound consumer's own Initialize()/Construct() runs. Zenject
        /// guarantees IInitializable.Initialize() runs after all bindings are constructed but before the first
        /// frame - see SaveService.Load()'s own doc comment ("for bootstrap / Zenject constructors").</summary>
        public void Initialize()
        {
            _saveService.Load();
        }

        /// <summary>Best-effort final flush on container teardown - see this class's own doc comment for why
        /// <see cref="SaveTrigger"/>'s LifeController.OnQuit hook exists as a second, more reliable path to the
        /// same call.</summary>
        public void Dispose()
        {
            _saveService.SaveImmediate();
        }

        public float GetMasterVolume() => _saveService.State.MasterVolume;

        public float GetMusicVolume() => _saveService.State.MusicVolume;

        public float GetSfxVolume() => _saveService.State.SfxVolume;

        public void SetMasterVolume(float volume)
        {
            _saveService.State.MasterVolume = Mathf.Clamp01(volume);
            _saveService.RequestSave();
        }

        public void SetMusicVolume(float volume)
        {
            _saveService.State.MusicVolume = Mathf.Clamp01(volume);
            _saveService.RequestSave();
        }

        public void SetSfxVolume(float volume)
        {
            _saveService.State.SfxVolume = Mathf.Clamp01(volume);
            _saveService.RequestSave();
        }

        public float GetBestSurvivalTimeSeconds() => _saveService.State.BestSurvivalTimeSeconds;

        public bool RecordSurvivalTime(float seconds)
        {
            GameSaveState state = _saveService.State;
            bool isNewRecord = seconds > state.BestSurvivalTimeSeconds;
            if (isNewRecord)
            {
                state.BestSurvivalTimeSeconds = seconds;
            }

            _saveService.RequestSave();
            return isNewRecord;
        }

        public int GetBestPlayerLevel() => _saveService.State.BestPlayerLevel;

        public bool RecordPlayerLevel(int level)
        {
            GameSaveState state = _saveService.State;
            bool isNewRecord = level > state.BestPlayerLevel;
            if (isNewRecord)
            {
                state.BestPlayerLevel = level;
            }

            _saveService.RequestSave();
            return isNewRecord;
        }

        public void SaveImmediate() => _saveService.SaveImmediate();
    }
}
