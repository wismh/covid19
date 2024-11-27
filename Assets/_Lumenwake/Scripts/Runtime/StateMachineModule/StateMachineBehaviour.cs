using Cysharp.Threading.Tasks;
using Lumenwake;
using System;
using System.Collections.Generic;

namespace Project.Core.StateMachineModule {
    public abstract class StateMachineBehaviour<TStateBase> : IDisposable
        where TStateBase : StateBase
    {
        protected TStateBase ActiveStateBase;
        protected Dictionary<Type, TStateBase> States;

        private bool _cleanupRunning;
        private bool _disposed;
        private UniTask _exitThenClearStatesTask;

        public void SetStates(List<TStateBase> states)
        {
            ThrowIfTeardownStarted();

            States = new();
            foreach (TStateBase state in states)
            {
                States.Add(state.GetType(), state);
            }
        }

        public virtual async UniTask Enter<TState>() where TState : TStateBase
        {
            ThrowIfTeardownStarted();

            if (ActiveStateBase != null)
            {
                await ActiveStateBase.Exit();
            }

            ActiveStateBase = States[typeof(TState)];
            await ActiveStateBase.Enter();
        }

        public virtual async UniTask Enter<TState, TPayload>(TPayload payLoad)
            where TState : PayLoadedStateBase<TPayload>
        {
            ThrowIfTeardownStarted();

            if (ActiveStateBase != null)
            {
                await ActiveStateBase.Exit();
            }

            TState newState = States[typeof(TState)] as TState;

            if (newState != null)
            {
                await newState.Enter(payLoad);
            }

            ActiveStateBase = newState as TStateBase;
        }

        public virtual Type GetCurrentState() =>
            ActiveStateBase?.GetType();

        /// <summary>
        /// Awaits the active state's <see cref="StateBase.Exit"/>, then clears <see cref="States"/>.
        /// Shares the same teardown task as <see cref="Dispose"/> when exit is deferred.
        /// </summary>
        public virtual async UniTask ShutdownAsync()
        {
            if (_disposed)
            {
                return;
            }

            if (_cleanupRunning)
            {
                await _exitThenClearStatesTask;
                return;
            }

            if (ActiveStateBase != null)
            {
                TStateBase exiting = ActiveStateBase;
                ActiveStateBase = null;
                _cleanupRunning = true;
                _exitThenClearStatesTask = ExitThenClearStatesAsync(exiting);
                await _exitThenClearStatesTask;
                return;
            }

            States?.Clear();
            _disposed = true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_cleanupRunning)
            {
                return;
            }

            if (ActiveStateBase != null)
            {
                TStateBase exiting = ActiveStateBase;
                ActiveStateBase = null;
                _cleanupRunning = true;
                _exitThenClearStatesTask = ExitThenClearStatesAsync(exiting);
                _exitThenClearStatesTask.Forget();
                return;
            }

            States?.Clear();
            _disposed = true;
        }

        private async UniTask ExitThenClearStatesAsync(TStateBase exiting)
        {
            try
            {
                await exiting.Exit();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LoggingSystem.LogError($"State machine teardown: Exit failed. {ex}");
            }
            finally
            {
                States?.Clear();
                _cleanupRunning = false;
                _disposed = true;
            }
        }

        private void ThrowIfTeardownStarted()
        {
            if (_disposed || _cleanupRunning)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}