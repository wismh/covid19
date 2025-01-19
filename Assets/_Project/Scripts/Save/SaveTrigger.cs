using Project.Core.ApplicationLifecycle;
using Zenject;

namespace _Project.Save
{
    /// <summary>Flush-on-quit safety net for the save system. Exists separately from GameSaveService's own
    /// IDisposable.Dispose() because Zenject only calls that when ProjectContext's GameObject.OnDestroy() runs,
    /// which WebGL doesn't reliably guarantee on every quit path - LifeController.OnQuit is a second, independent
    /// signal raised directly from Unity's OnApplicationQuit().</summary>
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
