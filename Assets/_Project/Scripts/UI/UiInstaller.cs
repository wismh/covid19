using System.Collections.Generic;
using Lumenwake.UIModule;
using UnityEngine;
using Zenject;

namespace _Project.UI
{
    /// <summary>
    /// Currently unused/dead code (Phase 7 Stage 6 finding, see
    /// Assets/_Project/Scripts/Mechanics/Composition/MainSceneComposition.cs's `Construct` doc comment,
    /// "SceneContext correction" section) - this class only ever runs if a Zenject `SceneContext` is configured
    /// to install it, and no `SceneContext` exists anywhere in this project (matching both local reference
    /// projects, `template` and `td-over-unity`, which also have none). `MainSceneComposition` builds a
    /// `BaseScreenManager` by hand instead (its `BuildScreenManager()` method). Left in place rather than
    /// deleted, per this migration's "disable/leave, don't delete yet" rule - if a future phase does introduce a
    /// `SceneContext`, this installer becomes wireable again as-is.
    /// </summary>
    public class UiInstaller : MonoInstaller
    {
        [SerializeField] private List<BaseScreen> _screens;

        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<BaseScreenManager>().AsSingle().WithArguments(_screens);
            foreach (BaseScreen screen in _screens)
            {
                Container.Bind(screen.GetType()).FromInstance(screen);
            }
        }
    }
}
