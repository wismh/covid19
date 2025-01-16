using System.Collections.Generic;
using Lumenwake.UIModule;
using UnityEngine;
using Zenject;

namespace _Project.UI
{
    /// <summary>Binds IScreenManager for GameplayScene's SceneContext.</summary>
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
