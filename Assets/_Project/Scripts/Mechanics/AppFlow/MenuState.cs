using Cysharp.Threading.Tasks;
using Project.Core.SceneLoaderServiceModule;
using Project.Core.StateMachineModule;
using Project.Generated;

namespace _Project.Mechanics.AppFlow
{
    public sealed class MenuState : StateBase
    {
        private readonly ISceneLoaderService _sceneLoaderService;

        public MenuState(ISceneLoaderService sceneLoaderService)
        {
            _sceneLoaderService = sceneLoaderService;
        }

        public override async UniTask Enter()
        {
            await _sceneLoaderService.LoadSceneAsync(SceneInBuild.Menu, unloadRedundant: true);
        }

        public override UniTask Exit()
        {
            return UniTask.CompletedTask;
        }
    }
}
