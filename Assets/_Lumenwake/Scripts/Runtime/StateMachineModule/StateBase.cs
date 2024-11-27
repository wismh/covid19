using Cysharp.Threading.Tasks;

namespace Project.Core.StateMachineModule 
{
    public abstract class StateBase
    {
        public abstract UniTask Enter();

        public abstract UniTask Exit();
    }
}