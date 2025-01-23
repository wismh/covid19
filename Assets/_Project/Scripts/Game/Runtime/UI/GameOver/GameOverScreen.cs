using Cysharp.Threading.Tasks;
using Lumenwake.UIModule;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;
using _Project.Input;
using _Project.Mechanics.AppFlow;

namespace _Project.UI.GameOver
{
    /// <summary>Game-over screen with two outcomes: plain player death, or "all cells died" (triggered by a
    /// future cell-registry composition root via <see cref="SetAllCellsDied"/> - no such registry exists yet).
    /// HudScreen holds the PlayerHealth.OnDied subscription and opens this screen; this class only owns
    /// presentation and the restart input.</summary>
    public class GameOverScreen : BaseScreen
    {
        public override ScreenLayer Layer => ScreenLayer.Overlay;

        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private string _playerDiedMessage = "You Died";
        [SerializeField] private string _allCellsDiedMessage = "All Cells Have Died";

        private GlobalGameStateMachine _stateMachine;
        private bool _allCellsDied;

        private Actions _inputActions;

        [Inject]
        public void Construct(GlobalGameStateMachine stateMachine)
        {
            _stateMachine = stateMachine;
        }

        private void Awake()
        {
            _inputActions = new Actions();
        }

        private void OnDestroy()
        {
            _inputActions?.Dispose();
        }

        public override void OnOpen()
        {
            _messageText.text = _allCellsDied ? _allCellsDiedMessage : _playerDiedMessage;

            _inputActions.GameOver.Enable();
            _inputActions.GameOver.AnyKey.performed += OnAnyKeyPerformed;
        }

        public override UniTask OnClose()
        {
            _inputActions.GameOver.AnyKey.performed -= OnAnyKeyPerformed;
            _inputActions.GameOver.Disable();

            // Reset to the default (player-died) variant so a later plain OpenScreen<GameOverScreen>() call
            // (e.g. from HudScreen's OnDied handler) doesn't accidentally show a stale "All Cells Have Died"
            // from a previous session's SetAllCellsDied(true) call.
            _allCellsDied = false;
            return base.OnClose();
        }

        private void OnAnyKeyPerformed(InputAction.CallbackContext context) => Restart();

        public void SetAllCellsDied(bool allCellsDied)
        {
            _allCellsDied = allCellsDied;
        }

        public void Restart()
        {
            _stateMachine.Enter<MenuState>().Forget();
        }
    }
}
