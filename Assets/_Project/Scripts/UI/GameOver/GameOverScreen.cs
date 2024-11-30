using Cysharp.Threading.Tasks;
using Lumenwake.UIModule;
using TMPro;
using UnityEngine;
using Zenject;
using _Project.Mechanics.AppFlow;

namespace _Project.UI.GameOver
{
    /// <summary>
    /// Ports the game-over half of GMTK.UI.HUD (Assets/GMTK/Scripts/UI/HUD.cs's GameOverGroup(bool)/AnyKeyHandle)
    /// as its own screen, per REFACTORING_PLAN.md's "HUD.cs (моноліт-оркестратор) -&gt; окремі BaseScreen" mapping.
    ///
    /// --- Two ways the old code showed this panel ---
    /// Old `HUD.GameOverGroup(true)` was called from two independent places:
    ///   1. GMTK.PlayerDamageable (player death) - the plain "You Died" case.
    ///   2. GMTK.CellController, when the cell that just died was the last one alive (`CellManager.Pool.Count == 0`
    ///      checked INSIDE GameOverGroup itself) - swaps the text to "All Cells Have Died" instead.
    /// This screen keeps both outcomes but splits how they're triggered: PlayerHealth.OnDied is a real Phase-2
    /// event this class can subscribe to directly (see HudScreen's class doc comment, "Auto-opening" section, for
    /// why HudScreen - not this screen - holds that subscription and calls OpenScreen&lt;GameOverScreen&gt;()).
    /// The "all cells died" branch has NO equivalent trigger yet: REFACTORING_PLAN.md's own Phase 3 notes confirm
    /// no _Project cell/virus registry exists (same gap CellInfectionState.cs and HudScreen's statistics-text hook
    /// document). <see cref="SetAllCellsDied"/> is exposed as that future trigger's entry point - a future
    /// cell-registry composition root calls `gameOverScreen.SetAllCellsDied(true)` and then
    /// `screenManager.OpenScreen&lt;GameOverScreen&gt;()` directly (bypassing HudScreen's OnDied-based open path
    /// entirely, since a "last cell died" game over does not necessarily involve PlayerHealth.OnDied firing at
    /// all) once that registry exists. Not invented here - just the documented seam.
    ///
    /// --- Restart ---
    /// Old: `HUD.AnyKeyHandle` (bound to InputAction "GameOver.AnyKey") -&gt; `ServiceLocator.Instance.
    /// Get&lt;FadeManager&gt;().FadeIn(() =&gt; SceneManager.LoadScene("Menu"))`. The raw scene load is replaced
    /// with the Phase-1 app-flow state machine, per the phase brief: `GlobalGameStateMachine.Enter&lt;MenuState&gt;()`
    /// (MenuState itself loads the Menu scene via ISceneLoaderService - see MenuState.cs). The old "any key"
    /// input binding and fade transition are not reproduced - this exposes a plain <see cref="Restart"/> method
    /// for a UI Button.onClick a human wires up later (same "expose for whatever UI will be wired later" shape as
    /// UpgradeScreen.SelectUpgrade), rather than this pure-C#-only phase inventing new InputAction/fade wiring.
    /// </summary>
    public class GameOverScreen : BaseScreen
    {
        public override ScreenLayer Layer => ScreenLayer.Overlay;

        [Header("Old: GameOverGroup's child \"YouDied\" TextMeshProUGUI, text-swapped in place")]
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private string _playerDiedMessage = "You Died";
        [SerializeField] private string _allCellsDiedMessage = "All Cells Have Died";

        private GlobalGameStateMachine _stateMachine;
        private bool _allCellsDied;

        /// <summary>GlobalGameStateMachine IS a real Zenject binding (ProjectContextInstaller) and resolves today,
        /// unlike the Mechanics/* POCOs other screens in this phase depend on.</summary>
        [Inject]
        public void Construct(GlobalGameStateMachine stateMachine)
        {
            _stateMachine = stateMachine;
        }

        public override void OnOpen()
        {
            if (_messageText != null)
                _messageText.text = _allCellsDied ? _allCellsDiedMessage : _playerDiedMessage;
        }

        public override UniTask OnClose()
        {
            // Reset to the default (player-died) variant so a later plain OpenScreen<GameOverScreen>() call
            // (e.g. from HudScreen's OnDied handler) doesn't accidentally show a stale "All Cells Have Died"
            // from a previous session's SetAllCellsDied(true) call.
            _allCellsDied = false;
            return base.OnClose();
        }

        /// <summary>Documented gap - see class doc comment's "Two ways the old code showed this panel" section.
        /// A future cell-registry composition root calls this (then opens this screen itself) when the last
        /// living cell dies; nothing in this phase calls it.</summary>
        public void SetAllCellsDied(bool allCellsDied)
        {
            _allCellsDied = allCellsDied;
        }

        /// <summary>Old: HUD.AnyKeyHandle - see class doc comment's "Restart" section.</summary>
        public void Restart()
        {
            _stateMachine.Enter<MenuState>().Forget();
        }
    }
}
