using Cysharp.Threading.Tasks;
using Lumenwake.UIModule;
using UnityEngine;
using Zenject;
using _Project.Mechanics.AppFlow;

namespace _Project.UI.MainMenu
{
    /// <summary>
    /// Ports GMTK.MenuManager (Assets/GMTK/Scripts/Menu/MenuManager.cs)'s two button handlers.
    ///
    /// --- Play() ---
    /// Old `Play()` played a Timeline cutscene (`_cutscene.Play()`) + an SFX `AudioSource.Play()`, waited 1.5s
    /// (`DelayBeforePlay` coroutine), faded a UI panel to black over 0.3s via DOTween, then on fade-complete
    /// called `LoadGameScene()` (`SceneManager.LoadScene("SampleScene")`). This phase is pure-C#-UI-logic only -
    /// no Timeline/PlayableDirector asset exists to reference and porting cutscene PLAYBACK content is explicitly
    /// out of scope per the phase brief ("just note it as an out-of-scope visual/content detail"). What IS ported
    /// is the actual scene transition, replacing the raw `SceneManager.LoadScene("SampleScene")` call with the
    /// Phase-1 app-flow state machine: `GlobalGameStateMachine.Enter&lt;GameplayState&gt;()`, which itself loads
    /// SceneInBuild.SampleScene via ISceneLoaderService (see GameplayState.cs) - i.e. the exact same target scene
    /// the old code loaded, just routed through the new state machine instead of a direct SceneManager call. The
    /// cutscene/fade/audio choreography that used to gate that scene load is left for whoever next touches this
    /// screen with Editor access to a Timeline asset and a fade panel to wire up.
    ///
    /// --- Exit() ---
    /// Old: `Application.Quit(0)` - ported as `Application.Quit()` (equivalent; the old explicit `0` argument sets
    /// the process exit code to 0, which is also `Application.Quit()`'s implicit default).
    /// </summary>
    public class MainMenuScreen : BaseScreen
    {
        private GlobalGameStateMachine _stateMachine;

        /// <summary>GlobalGameStateMachine IS a real Zenject binding (ProjectContextInstaller) and resolves today.</summary>
        [Inject]
        public void Construct(GlobalGameStateMachine stateMachine)
        {
            _stateMachine = stateMachine;
        }

        /// <summary>Old: MenuManager.Play() (minus the Timeline cutscene - see class doc comment).</summary>
        public void Play()
        {
            _stateMachine.Enter<GameplayState>().Forget();
        }

        /// <summary>Old: MenuManager.Exit().</summary>
        public void Exit()
        {
            Application.Quit();
        }
    }
}
