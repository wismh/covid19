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
    /// <summary>Persistent gameplay HUD content: health/armor/XP bars, timer, cell statistics text, reload
    /// indicator, and open-transition fade. GameOverScreen/UpgradeScreen are separate overlay screens (see
    /// UiInstaller), not toggled from here.
    ///
    /// This screen also subscribes to PlayerStatsService.OnLevelUp/PlayerHealth.OnDied and opens
    /// UpgradeScreen/GameOverScreen itself, rather than those screens subscribing on their own: an Overlay-layer
    /// screen that starts closed can't open itself, since BaseScreenManager only calls OnOpen() from inside
    /// OpenScreen() - a screen that's never been opened is never subscribed. HudScreen is the one screen
    /// guaranteed open for the whole gameplay session, so it holds that decision instead.
    /// </summary>
    public class HudScreen : BaseScreen
    {
        [SerializeField] private Image _healthBarFilled;
        [SerializeField] private Image _armorBarFilled;
        [SerializeField] private Image _experienceBarFilled;
        [SerializeField] private TMP_Text _levelText;

        // The health bar art's fixed visual segment count - independent of PlayerConfig.BaseMaxHealth (the
        // gameplay cap), so health fills as a fraction of this instead of MaxHealth directly.
        [SerializeField] private float _healthBarVisualCapacity = 8f;

        [SerializeField] private TMP_Text _timerText;

        [SerializeField] private TMP_Text _statisticsText;

        [SerializeField] private Image _reloadIndicatorFilled;

        [SerializeField] private Image _fadePanel;

        private PlayerConfig _config;
        private PlayerHealth _playerHealth;
        private PlayerStatsService _playerStats;
        private IScreenManager _screenManager;
        private PlayerTurret _playerTurret;

        private CancellationTokenSource _timerCts;
        private int _elapsedSeconds;

        // Captured from _statisticsText's own authored text the first time this screen opens - the "{lc}/{sc}/{cd}"
        // template is wired directly into the TMP_Text asset in the Editor.
        private string _statisticsPattern;

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
            _levelText.text = _playerStats.Level.ToString();

            _statisticsPattern = _statisticsText.text;

            _elapsedSeconds = 0;
            UpdateTimerText();
            _timerCts = new CancellationTokenSource();
            RunTimerAsync(_timerCts.Token).Forget();

            _fadePanel.gameObject.SetActive(true);
            _fadePanel.color = new Color(_fadePanel.color.r, _fadePanel.color.g, _fadePanel.color.b, 1f);
            _fadePanel.DOFade(0f, 0.4f).SetUpdate(true).OnComplete(() => _fadePanel.gameObject.SetActive(false));
        }

        private void Update()
        {
            // ReloadProgress01 is a continuous value with no change event to hook, so it's polled. Gated on
            // IsAlive so the indicator hides and stops updating the instant the player dies.
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

        /// <summary>Wired to CellRegistry.OnStatisticsChanged by
        /// <see cref="_Project.Mechanics.Composition.HudStatisticsDriver"/>.</summary>
        public void SetStatistics(int livingCells, int sickCells, int diedCells)
        {
            if (_statisticsPattern == null)
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
            int minutes = _elapsedSeconds / 60;
            int seconds = _elapsedSeconds % 60;
            _timerText.text = $"{minutes:D2}:{seconds:D2}";
        }

        private void RefreshHealthAndArmorBars()
        {
            _healthBarFilled.fillAmount = _healthBarVisualCapacity > 0f
                ? Mathf.Clamp01(_playerHealth.Health / _healthBarVisualCapacity)
                : 0f;

            _armorBarFilled.fillAmount = _config.MaxArmor > 0
                ? (float)_playerHealth.Armor / _config.MaxArmor
                : 0f;
        }

        private void RefreshExperienceBar()
        {
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
            _levelText.text = newLevel.ToString();
            _screenManager.OpenScreen<UpgradeScreen>().Forget();
        }

        private void HandleDied()
        {
            _screenManager.OpenScreen<GameOverScreen>().Forget();
        }

        private void OnDestroy()
        {
            DOTween.Kill(_fadePanel);
        }
    }
}
