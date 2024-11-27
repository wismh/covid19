using Project.Generated;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Project.Bootstrap
{
    public sealed class Bootstrap : MonoBehaviour
    {
        private void Start()
        {
            SceneManager.LoadScene(SceneInBuild.Menu);
        }
    }
}
