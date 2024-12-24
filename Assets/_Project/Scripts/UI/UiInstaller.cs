using System.Collections.Generic;
using Lumenwake.UIModule;
using UnityEngine;
using Zenject;

namespace _Project.UI
{
    /// <summary>
    /// Binds IScreenManager for GameplayScene's SceneContext (attached alongside GameplaySceneInstaller). Revived
    /// 2026-09-19 as part of the SceneContext/modular-Installer DI migration - was dead code from Phase 1 until
    /// then (no SceneContext existed to run it; MainSceneComposition built a BaseScreenManager by hand instead,
    /// see Docs/REFACTORING_PLAN.md's "Пост-міграційний DI-рефакторинг" entry).
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
