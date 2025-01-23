using Cysharp.Threading.Tasks;
using Project.Core.SceneLoaderServiceModule;
using Project.Core.StateMachineModule;
using Project.Generated;
using UnityEngine;

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
            // See GameplayState.Enter() - same stuck-pause guard for UpgradeScreen's Time.timeScale = 0.
            Time.timeScale = 1f;
            await _sceneLoaderService.LoadSceneAsync(SceneInBuild.Menu, unloadRedundant: true);
        }

        public override UniTask Exit()
        {
            return UniTask.CompletedTask;
        }
    }
}
