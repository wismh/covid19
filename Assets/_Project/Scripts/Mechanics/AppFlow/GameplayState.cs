using Cysharp.Threading.Tasks;
using Project.Core.SceneLoaderServiceModule;
using Project.Core.StateMachineModule;
using Project.Generated;
using UnityEngine;

namespace _Project.Mechanics.AppFlow
{
    public sealed class GameplayState : StateBase
    {
        private readonly ISceneLoaderService _sceneLoaderService;

        public GameplayState(ISceneLoaderService sceneLoaderService)
        {
            _sceneLoaderService = sceneLoaderService;
        }

        public override async UniTask Enter()
        {
            // UpgradeScreen sets Time.timeScale = 0 while its card picker is up (see UpgradeScreen.OnOpen) and
            // relies on OnClose to restore it - but a screen that's merely pushed down the Overlay stack (e.g.
            // GameOverScreen opening on top of it) or a direct state-machine transition never calls OnClose.
            // Reset here so a stuck pause from a previous run can never carry into a fresh gameplay session.
            Time.timeScale = 1f;
            await _sceneLoaderService.LoadSceneAsync(SceneInBuild.GameplayScene, unloadRedundant: true);
        }

        public override UniTask Exit()
        {
            return UniTask.CompletedTask;
        }
    }
}
