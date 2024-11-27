using Cysharp.Threading.Tasks;

namespace Project.Core.StateMachineModule {
    public abstract class PayLoadedStateBase<TPayload> : StateBase 
    {
        public virtual UniTask Enter(TPayload payLoad)
        {
            return Enter();
        }
    }
}