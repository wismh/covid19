using GMTK.Services;
using System;

namespace GMTK.GameStates
{
    public enum WorldState
    {
        MICRO_WORLD,
        MACRO_WORLD
    };

    public class GamePlayState : IState, IService
    {
        public GamePlayState(GameStateManager stateManager) { }
        public Action<WorldState> OnWorldChanged;

        private WorldState _worldState = WorldState.MACRO_WORLD;
        private VirusManager _virusManager;

        public WorldState State
        {
            get => _worldState;
            set
            {
                _virusManager ??= ServiceLocator.Instance.Get<VirusManager>();

                if (value == _worldState)
                    return;

                if (value == WorldState.MICRO_WORLD)
                    TransitionIntoMicroWorld();

                if (value == WorldState.MACRO_WORLD)
                    TransitionIntoMacroWorld();

                _worldState = value;
                OnWorldChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// Phase 7 testing fix (2026-09-11): mirrors _worldState from the new _Project.Mechanics.WorldScale.
        /// WorldScaleController WITHOUT re-running this class's own transition logic below (TransitionIntoMicro/
        /// MacroWorld, each a full independent DOTween-driven camera/player/cell transition - calling the real
        /// State setter here would run both the old and the new transition on the same objects at once). Old code
        /// the new architecture kept running (GMTK.Bullet.Update/OnEnable - scales/slows bullets to 0.3x in the
        /// micro world) still reads the State getter below, but nothing has updated it since the new
        /// WorldScaleController took over actually triggering transitions - user-reported symptom: oversized,
        /// full-speed bullets while inside a cell. A composition root calls this from WorldScaleController.
        /// OnScaleChanged to keep old readers correct without re-triggering old writers.
        /// </summary>
        public void SyncWorldState(WorldState state)
        {
            _worldState = state;
        }

        private void TransitionIntoMicroWorld()
        {
            new TransitionIntoMicroWorld()
                .Start(this);

            _virusManager.gameObject.SetActive(false);
        }

        private void TransitionIntoMacroWorld()
        {
            new TransitionIntoMacroWorld()
                .Start(this);

            _virusManager.gameObject.SetActive(true);
        }

        void IState.Enter()
        {
            ServiceLocator.Instance.Register(this);
        }

        void IState.Update()
        {

        }

        void IState.Exit()
        {

        }
    }
}
