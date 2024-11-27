using Cysharp.Threading.Tasks;

namespace Project.Core.SceneLoaderServiceModule
{
    /// <summary>
    /// Keeps Global + gameplay additive stack and swaps only the level scene (additive).
    /// </summary>
    public interface IGameplayLevelSceneService
    {
        UniTask SwapLevelSceneAsync(string levelSceneToLoad, string levelSceneToUnload);
    }
}
