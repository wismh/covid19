using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
    /// 1/7.69. Originally read as "health / maxHealth" in disguise, since PlayerConfig.BaseMaxHealth (read from
    /// Player.prefab in Phase 2) and MaxArmor were both 8 at the time - so this class modeled both bars as a
    /// proper `current / max` fill fraction: `PlayerHealth.Health / PlayerHealth.MaxHealth` and `PlayerHealth.
    /// Armor / PlayerConfig.MaxArmor`.
    ///
    /// That reading turned out to be wrong for health specifically (testing fix, 2026-09-11, same day):
    /// BaseMaxHealth got reverted to 3 (the ORIGINAL shipped game's actual starting HP - see that commit),
    /// exposing that 0.13f was never "health / maxHealth" at all - it's the bar ART's fixed ~8-unit visual
    /// capacity, independent of whatever the gameplay max happens to be. `Health / MaxHealth` at 3/3 is always
    /// 1.0 (bar always reads "full"), which is what the user reported. See _healthBarVisualCapacity's own doc
    /// comment for the fix - health now divides by that fixed capacity instead. Armor's reasoning still holds:
    /// PlayerConfig.MaxArmor is the real gameplay cap AND stayed the value the armor bar art was tuned for, so
    /// `Armor / MaxArmor` is correct as originally written.
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
    /// --- Statistics text: documented gap, now RESOLVED (CellRegistry.OnStatisticsChanged wired in Phase 7) ---
    /// Old: GMTK.UI.HUD.Awake() captured the TextMeshProUGUI's own initial text as a `{lc}`/`{sc}`/`{cd}` template
    /// and re-rendered it on GMTK.Services.GameStatistics.OnStatisticsChange (living/sick/died cell counts).
    /// <see cref="SetStatistics"/> is that same hook, wired to CellRegistry.OnStatisticsChanged by
    /// MainSceneComposition.WireCrossSystemEvents() once that registry existed - see CellRegistry's own doc
    /// comment for the exact wiring.
    ///
    /// --- Phase 8 testing fix (2026-09-11): the shared "Statistics" TMP_Text was corrupted before this class
    /// ever saw it ---
    /// User-reported symptom: living/sick/died cell counts on the HUD never updated, always showing "00". Root
    /// cause: GMTK.UI.HUD - the OLD monolith this class's own class doc comment already said it replaces - was
    /// still enabled on the SAME "HUD" GameObject, doing `transform.Find("Statistics")` to reach the exact same
    /// TMP_Text this class's <see cref="_statisticsText"/> is wired to. Old HUD.Awake() captures ITS OWN pattern
    /// then immediately renders once against its own (never-updated-again, always-zero) GMTK.Services.
    /// GameStatistics snapshot - permanently baking "00"/"00"/"00" over the `{lc}`/`{sc}`/`{cd}` placeholders in
    /// the shared TMP_Text's runtime text, BEFORE this class's own OnOpen() ever runs (confirmed live: Debug.Log
    /// tracing showed OnOpen's own pattern capture already read back "00...00", not the authored template -
    /// old HUD.Awake() runs earlier in the Awake ordering). Once the `{lc}` placeholders were gone, every
    /// subsequent SetStatistics() call's .Replace() calls were permanent no-ops - the exact same class of bug as
    /// the health bar fix two rounds ago, just via GameObject-sharing instead of a stale formula. Removed the
    /// GMTK.UI.HUD component from the scene (its job is now fully covered by this class), the same fix already
    /// applied to GMTK.UI.AdviceDialog for the same reason (see REFACTORING_PLAN.md's ninth-round entry).
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

        /// <summary>
        /// Phase 7 testing fix (2026-09-11): the health bar's ART was always designed for the old ~8-unit visual
        /// capacity this class's own doc comment already derives from old code's `0.13f` fill constant (below) -
        /// a UI/presentation concern, independent of PlayerConfig.BaseMaxHealth (the GAMEPLAY health cap, which
        /// the same day's earlier fix correctly reverted to 3 to match the original's actual starting HP - see
        /// that commit). Dividing by PlayerHealth.MaxHealth directly (as this class originally did) silently
        /// assumed those two numbers stayed equal - true only by coincidence while BaseMaxHealth was still 8.
        /// Once it became 3, `Health / MaxHealth` is always 1.0 at full health regardless of the actual value,
        /// so the bar visually reads "full" (all ~8 segments lit) even at the correct 3 HP - exactly the user-
        /// reported symptom. Kept as its own field, not derived from PlayerConfig, since it describes the
        /// artwork's fixed segment count, not anything about the player's stats.
        /// </summary>
        [SerializeField] private float _healthBarVisualCapacity = 8f;

        [Header("Timer (old GMTK.UI.HUD._timeTextMesh)")]
        [SerializeField] private TMP_Text _timerText;

        [Header("Statistics (documented gap - see class doc comment)")]
        [SerializeField] private TMP_Text _statisticsText;

        /// <summary>
        /// Phase 8 testing fix (2026-09-11): PlayerTurret.ReloadProgress01 already existed, built specifically
        /// for "a future UI screen to bind a reload indicator to" (see that property's own doc comment) - nothing
        /// ever did. Old PlayerTurretController drove Player.prefab's "ReloadIndicatorFull" Image directly
        /// (`_filledReloadImage.fillAmount`, DOTween'd 0→1 over the reload duration on every shot); that old
        /// component is now disabled (superseded by this class + PlayerTurret), so the indicator was never
        /// touched again and stayed frozen at its Editor-authored default fillAmount (1 - always "full",
        /// regardless of actual reload state) - exactly the user-reported symptom. Polled every frame in Update()
        /// rather than event-driven like the other bars: ReloadProgress01 is a continuous Time.time-based value
        /// with no natural "changed" event to hook, same reasoning old BarsHUD.Update() had for polling Health/
        /// Armor before PlayerHealth grew real events.
        /// </summary>
        [SerializeField] private Image _reloadIndicatorFilled;

        [Header("Fade (old FadeManager)")]
        [SerializeField] private Image _fadePanel;

        private PlayerConfig _config;
        private PlayerHealth _playerHealth;
        private PlayerStatsService _playerStats;
        private IScreenManager _screenManager;
        private PlayerTurret _playerTurret;

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
        public void Construct(PlayerConfig config, PlayerHealth playerHealth, PlayerStatsService playerStats, IScreenManager screenManager, PlayerTurret playerTurret)
        {
            _config = config;
            _playerHealth = playerHealth;
            _playerStats = playerStats;
            _screenManager = screenManager;
            _playerTurret = playerTurret;
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

            if (_fadePanel == null)
            {
                var fadeChild = transform.Find("FadePanelSprite");
                if (fadeChild != null)
                    _fadePanel = fadeChild.GetComponent<Image>();
            }

            if (_fadePanel != null)
            {
                _fadePanel.gameObject.SetActive(true);
                _fadePanel.color = new Color(_fadePanel.color.r, _fadePanel.color.g, _fadePanel.color.b, 1f);
                _fadePanel.DOFade(0f, 0.4f).SetUpdate(true).OnComplete(() =>
                {
                    if (_fadePanel != null)
                        _fadePanel.gameObject.SetActive(false);
                });
            }
        }

        private void Update()
        {
            // See _reloadIndicatorFilled's doc comment - continuous value, no change event to hook.
            //
            // Phase 9 testing fix (2026-09-12): user-reported symptom - the reload ring stayed visible (and kept
            // animating, since nothing ever stopped polling it) after the player died. Old PlayerTurretController
            // simply stopped running once its GameObject/component was disabled on death, freezing the old
            // DOTween-driven fill in place; this polls unconditionally every frame with no equivalent gate, so it
            // kept ticking through to fully-reloaded on the game-over screen. Gated on PlayerHealth.IsAlive - the
            // indicator hides itself and stops updating the instant the player dies, matching what a player
            // expects to see once the fight is over, rather than leaking gameplay HUD into the death screen.
            if (_reloadIndicatorFilled == null || _playerTurret == null || _playerHealth == null)
                return;

            bool isAlive = _playerHealth.IsAlive;
            if (_reloadIndicatorFilled.gameObject.activeSelf != isAlive)
                _reloadIndicatorFilled.gameObject.SetActive(isAlive);

            if (isAlive)
                _reloadIndicatorFilled.fillAmount = _playerTurret.ReloadProgress01;
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
            // See _healthBarVisualCapacity's doc comment - divides by the bar art's fixed segment count, not
            // PlayerHealth.MaxHealth (the actual, now much smaller, gameplay cap).
            if (_healthBarFilled != null)
                _healthBarFilled.fillAmount = _healthBarVisualCapacity > 0f
                    ? Mathf.Clamp01(_playerHealth.Health / _healthBarVisualCapacity)
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

        private void OnDestroy()
        {
            if (_fadePanel != null)
                DOTween.Kill(_fadePanel);
        }
    }
}
