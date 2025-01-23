using Project.Generated;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Project.Mechanics.GameBootstrap
{
    public sealed class Bootstrap : MonoBehaviour
    {
        private void Start()
        {
            // Keeps gameplay/physics/coroutines ticking while the game window is unfocused - without this,
            // Unity throttles Update/FixedUpdate to near-zero when another window has focus, which breaks any
            // out-of-process testing/automation (e.g. driving Play Mode via the Unity CLI) and is generally
            // undesirable for a game the player might alt-tab out of.
            Application.runInBackground = true;

            SceneManager.LoadScene(SceneInBuild.Menu);
        }
    }
}
