using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Project.Core.SceneLoaderServiceModule
{
    public sealed class GameplayLevelSceneService : IGameplayLevelSceneService
    {
        private readonly ISceneLoaderService _sceneLoaderService;

        public GameplayLevelSceneService(ISceneLoaderService sceneLoaderService) =>
            _sceneLoaderService = sceneLoaderService;

        public async UniTask SwapLevelSceneAsync(string levelSceneToLoad, string levelSceneToUnload)
        {
            var shouldUnload = levelSceneToUnload != levelSceneToLoad &&
                               SceneManager.GetSceneByName(levelSceneToUnload).isLoaded;

            if (shouldUnload)
                await _sceneLoaderService.UnloadSceneAsync(levelSceneToUnload);

            var needLoad = !SceneManager.GetSceneByName(levelSceneToLoad).isLoaded;

            if (needLoad)
                await _sceneLoaderService.LoadSceneAsync(levelSceneToLoad, unloadRedundant: false);
        }
    }
}
