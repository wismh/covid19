using _Project.Entities.Permanent;
using Project.Core.SaveServiceModule;
using UnityEngine;
using Zenject;

namespace _Project.Gateways.SaveService
{
    /// <summary>
    /// Zenject-bound <see cref="IGameSaveService"/>, wrapping <c>ISaveService&lt;GameSaveState&gt;</c> (bound
    /// by <see cref="GameSaveModuleInstaller"/>). Loads the save file on <see cref="Initialize"/> and does a
    /// best-effort flush on <see cref="Dispose"/> - see <see cref="SaveTrigger"/> for the more reliable
    /// OnQuit-based flush, since container Dispose isn't guaranteed to run on every platform's quit path.
    /// </summary>
    public sealed class GameSaveService : IGameSaveService, IInitializable, System.IDisposable
    {
        private readonly ISaveService<GameSaveState> _saveService;

        public GameSaveService(ISaveService<GameSaveState> saveService)
        {
            _saveService = saveService;
        }

        public void Initialize()
        {
            _saveService.Load();
        }

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
