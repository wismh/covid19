using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lumenwake.UIModule;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using _Project.Mechanics.Player;
using _Project.UI.GameOver;
using _Project.UI.Upgrades;

namespace _Project.UI.Hud
{
    /// <summary>
    /// Real content for the persistent gameplay HUD - ports the "Bars" (health/armor/XP) and "Timer"/"Statistics"
    /// text pieces of GMTK.UI.HUD + GMTK.UI.BarsHUD (Assets/GMTK/Scripts/UI/HUD.cs, BarsHUD.cs). The old monolith
    /// also owned AdviceGetOutText/GameOverGroup/UpgradeGroup toggling - those are NOT reproduced here, because
    /// the new `BaseScreen`/`ScreenLayer` model already gives each of those its own independent screen
    /// (GameOverScreen/UpgradeScreen, both ScreenLayer.Overlay - see UiInstaller) instead of one class flipping
    /// sibling GameObjects active/inactive.
    ///
    /// --- Health/Armor bars: the old 0.13f magic-number scale, and how this replaces it ---
    /// Old GMTK.UI.BarsHUD.Update() (polled every frame, TECHNICAL_AUDIT.md 2.2's flagged smell):
    ///   `_healthBarFilled.fillAmount = _player.Health * 0.13f; _armorBarFilled.fillAmount = _player.Armor * 0.13f;`
    /// 0.13f is not a documented fraction of anything in the old code - it's suspiciously close to 1/8 (0.125) or
    /// 1/7.69, and PlayerConfig.BaseMaxHealth (read from Player.prefab in Phase 2) is 8, and MaxArmor is 8 too, so
    /// `health * 0.13` and `health / 8` produce nearly the same visual result for on-screen bars (8 * 0.13 = 1.04,
    /// clamped) without ever having been *defined* as "health / maxHealth" - it reads as a tuned-by-eye constant
    /// that happens to approximate that ratio for this game's specific numbers, not an intentional formula. Per
    /// the phase brief's explicit permission to not preserve this blindly: this class models both bars as a
    /// proper `current / max` fill fraction instead - `PlayerHealth.Health / PlayerHealth.MaxHealth` and
    /// `PlayerHealth.Armor / PlayerConfig.MaxArmor` - which reproduces the same near-full-bar-at-max-stats visual
    /// behavior while actually being correct if BaseMaxHealth/MaxArmor are ever retuned in PlayerConfig (the old
    /// 0.13f constant would silently stop matching the real max the moment either config number changed).
    ///
    /// --- Polling -> events ---
    /// BarsHUD polled Health/Armor from Update() because PlayerDamageable had no health-changed event to
    /// subscribe to. PlayerHealth (Phase 2) does expose enough events to cover every mutation site of its private
    /// _health/_armor fields: OnDamaged (every hit, whether it reduced health or consumed armor),
    /// PlayerStatsService.OnUpgraded (Health/Armor upgrade picks), and PlayerStatsService.OnBonusHealthGranted
    /// (the even-level silent bonus HP - see PlayerStatsService's doc comment). Subscribing to those three instead
    /// of polling Update() matches REFACTORING_PLAN.md section 4's "події, а не полінг" rule directly, rather than
    /// invoking its one documented escape hatch (polling HP each frame with a comment explaining why) - polling
    /// simply wasn't necessary here once PlayerHealth's actual event surface was checked.
    ///
    /// --- Timer ---
    /// Old: GMTK.UI.HUD.StartTime() - a `StartCoroutine` `while(true) { playerStats.Time += 1; ...; yield return
    /// new WaitForSeconds(1); }` loop that also happened to be the sole place `PlayerStats.Time` was written to -
    /// every other system that gated content on elapsed time (WaveScalingConfig thresholds, ported in Phase 3)
    /// read that same field. This class ports just the MM:SS display tick via UniTask + CancellationTokenSource
    /// (REFACTORING_PLAN.md section 4/5: "Корутини -&gt; UniTask + CancellationTokenSource в OnDestroy" - here,
    /// started in OnOpen/cancelled in OnClose instead, since a BaseScreen has no OnDestroy step of its own in this
    /// phase). It does NOT re-create `PlayerStats.Time` as a shared value other systems read - Phase 3's
    /// WaveScalingConfig-driven unlock logic has no `_Project` composition root wiring it to anything yet (see
    /// WaveScalingConfig.cs / CellInfectionState.cs doc comments for that same "no registry/composition root yet"
    /// gap), so there is nothing else to feed a shared timer value into right now. Whoever writes that
    /// composition root should decide then whether the elapsed-seconds counter lives here or gets hoisted into
    /// its own small shared service; duplicating it into two owners would be worse than this screen owning the
    /// display-only copy until that's decided.
    ///
    /// --- Statistics text: documented gap ---
    /// Old: GMTK.UI.HUD.Awake() captured the TextMeshProUGUI's own initial text as a `{lc}`/`{sc}`/`{cd}` template
    /// and re-rendered it on GMTK.Services.GameStatistics.OnStatisticsChange (living/sick/died cell counts).
    /// REFACTORING_PLAN.md's own Phase 3 notes confirm no `_Project` equivalent of GameStatistics/CellManager
    /// exists yet ("Немає реєстру клітин/вірусів... усе це свідомо відкладено" - Phase 3 section). Per the phase
    /// brief, this is a real, documented gap, not something to fake with an invented registry: <see
    /// cref="SetStatistics"/> is exposed as the hook a future cell-registry composition root calls, exactly the
    /// same shape as the old event handler's body, once that registry exists.
    ///
    /// --- Auto-opening UpgradeScreen/GameOverScreen: a judgement call ---
    /// The phase brief asks UpgradeScreen to "open automatically when PlayerStatsService.OnLevelUp fires (wire
    /// the subscription the same OnOpen/OnClose way)" and GameOverScreen to "subscribe to PlayerHealth.OnDied".
    /// Read literally on each screen individually this is circular: an Overlay-layer screen that starts closed
    /// can't be the one to open itself, because BaseScreenManager only calls a screen's OnOpen() from inside
    /// OpenScreen() - a screen that has never been opened is never subscribed, so it can never observe the event
    /// that would open it for the first time. HudScreen is the one screen guaranteed to already be open for the
    /// entire gameplay session (ScreenLayer.Default, opened once when gameplay starts and never closed until the
    /// session ends), so it is the natural place to hold the "does the event mean an overlay should open"
    /// decision - it still follows the OnOpen/OnClose subscribe/unsubscribe convention, just on this screen
    /// instead of on the overlay being opened. UpgradeScreen/GameOverScreen still each do their own OnOpen-time
    /// setup (regenerating offered upgrades / picking the death message) independently - see their class comments.
    /// </summary>
    public class HudScreen : BaseScreen
    {
        [Header("Bars (old GMTK.UI.BarsHUD)")]
        [SerializeField] private Image _healthBarFilled;
        [SerializeField] private Image _armorBarFilled;
        [SerializeField] private Image _experienceBarFilled;
        [SerializeField] private TMP_Text _levelText;

        [Header("Timer (old GMTK.UI.HUD._timeTextMesh)")]
        [SerializeField] private TMP_Text _timerText;

        [Header("Statistics (documented gap - see class doc comment)")]
        [SerializeField] private TMP_Text _statisticsText;

        private PlayerConfig _config;
        private PlayerHealth _playerHealth;
        private PlayerStatsService _playerStats;
        private IScreenManager _screenManager;

        private CancellationTokenSource _timerCts;
        private int _elapsedSeconds;

        // Captured from _statisticsText's own authored text the first time this screen opens - exact port of
        // `_statisticsTextPattern = _statisticsTextMesh.text;` in old GMTK.UI.HUD.Awake(). A human wires the
        // literal "{lc}/{sc}/{cd}" template into the TMP_Text asset later, same as the old prefab did.
        private string _statisticsPattern;

        /// <summary>
        /// Composition-root note: PlayerConfig/PlayerHealth/PlayerStatsService are Mechanics/* POCOs/MonoBehaviours
        /// deliberately kept outside Zenject binding per REFACTORING_PLAN.md section 4 (see PlayerHealth.Construct's
        /// own doc comment for the identical, already-existing gap) - a future GameplayComposition/MainSceneComposition
        /// class is expected to supply them, exactly as Phase 2/3/4 already leave for PlayerMovement/PlayerTurret/
        /// CellVisuals. IScreenManager IS a real Zenject binding (UiInstaller -&gt; BaseScreenManager) and resolves today.
        /// </summary>
        [Inject]
        public void Construct(PlayerConfig config, PlayerHealth playerHealth, PlayerStatsService playerStats, IScreenManager screenManager)
        {
            _config = config;
            _playerHealth = playerHealth;
            _playerStats = playerStats;
            _screenManager = screenManager;
        }

        public override void OnOpen()
        {
            _playerStats.OnExperienceChanged += HandleExperienceChanged;
            _playerStats.OnLevelUp += HandleLevelUp;
            _playerStats.OnUpgraded += HandleUpgraded;
            _playerStats.OnBonusHealthGranted += HandleBonusHealthGranted;
            _playerHealth.OnDamaged += HandleDamaged;
            _playerHealth.OnDied += HandleDied;

            RefreshHealthAndArmorBars();
            RefreshExperienceBar();
            if (_levelText != null)
                _levelText.text = _playerStats.Level.ToString();

            if (_statisticsText != null)
                _statisticsPattern = _statisticsText.text;

            _elapsedSeconds = 0;
            UpdateTimerText();
            _timerCts = new CancellationTokenSource();
            RunTimerAsync(_timerCts.Token).Forget();
        }

        public override UniTask OnClose()
        {
            _playerStats.OnExperienceChanged -= HandleExperienceChanged;
            _playerStats.OnLevelUp -= HandleLevelUp;
            _playerStats.OnUpgraded -= HandleUpgraded;
            _playerStats.OnBonusHealthGranted -= HandleBonusHealthGranted;
            _playerHealth.OnDamaged -= HandleDamaged;
            _playerHealth.OnDied -= HandleDied;

            _timerCts?.Cancel();
            _timerCts?.Dispose();
            _timerCts = null;

            return base.OnClose();
        }

        /// <summary>Hook for a future cell-registry composition root - see class doc comment's "Statistics text:
        /// documented gap" section. Old: the inline lambda GMTK.UI.HUD.Awake() subscribed to
        /// GameStatistics.OnStatisticsChange, ported verbatim (same {lc}/{sc}/{cd} `D2` formatting).</summary>
        public void SetStatistics(int livingCells, int sickCells, int diedCells)
        {
            if (_statisticsText == null || _statisticsPattern == null)
                return;

            _statisticsText.text = _statisticsPattern
                .Replace("{lc}", $"{livingCells:D2}")
                .Replace("{sc}", $"{sickCells:D2}")
                .Replace("{cd}", $"{diedCells:D2}");
        }

        private async UniTaskVoid RunTimerAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: cancellationToken);
                    _elapsedSeconds += 1;
                    UpdateTimerText();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on OnClose() cancelling _timerCts - not an error.
            }
        }

        private void UpdateTimerText()
        {
            if (_timerText == null)
                return;

            int minutes = _elapsedSeconds / 60;
            int seconds = _elapsedSeconds % 60;
            _timerText.text = $"{minutes:D2}:{seconds:D2}";
        }

        private void RefreshHealthAndArmorBars()
        {
            if (_healthBarFilled != null)
                _healthBarFilled.fillAmount = _playerHealth.MaxHealth > 0
                    ? (float)_playerHealth.Health / _playerHealth.MaxHealth
                    : 0f;

            if (_armorBarFilled != null)
                _armorBarFilled.fillAmount = _config.MaxArmor > 0
                    ? (float)_playerHealth.Armor / _config.MaxArmor
                    : 0f;
        }

        private void RefreshExperienceBar()
        {
            if (_experienceBarFilled == null)
                return;

            _experienceBarFilled.fillAmount = _playerStats.MaxExperience > 0
                ? (float)_playerStats.Experience / _playerStats.MaxExperience
                : 0f;
        }

        private void HandleDamaged(int damage, Vector3 attackDirection) => RefreshHealthAndArmorBars();

        private void HandleUpgraded(UpgradeStat stat, int newLevel) => RefreshHealthAndArmorBars();

        private void HandleBonusHealthGranted(int amount) => RefreshHealthAndArmorBars();

        private void HandleExperienceChanged(int experience, int maxExperience) => RefreshExperienceBar();

        private void HandleLevelUp(int newLevel)
        {
            if (_levelText != null)
                _levelText.text = newLevel.ToString();

            // See class doc comment's "Auto-opening" section for why this lives here instead of on UpgradeScreen.
            _screenManager.OpenScreen<UpgradeScreen>().Forget();
        }

        private void HandleDied()
        {
            // See class doc comment's "Auto-opening" section. GameOverScreen defaults to the "you died" message;
            // the "all cells died" variant is a separate, explicitly-triggered path - see GameOverScreen.SetAllCellsDied.
            _screenManager.OpenScreen<GameOverScreen>().Forget();
        }
    }
}
