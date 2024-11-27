using System.Collections.Generic;
using Project.Core.StateMachineModule;
using Zenject;

namespace _Project.Mechanics.AppFlow
{
    public sealed class GlobalGameStateMachine : StateMachineBehaviour<StateBase>
    {
        [Inject]
        public GlobalGameStateMachine(MenuState menuState, GameplayState gameplayState)
        {
            SetStates(new List<StateBase>
            {
                menuState,
                gameplayState
            });
        }
    }
}
