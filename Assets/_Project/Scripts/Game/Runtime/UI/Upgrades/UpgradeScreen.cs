using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Lumenwake.UIModule;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zenject;
using _Project.Mechanics.Player;
using Random = UnityEngine.Random;

namespace _Project.UI.Upgrades
{
    /// <summary>Level-up upgrade card picker: offers a random selection of stats to choose from, redrawn fresh
    /// every time the screen opens.</summary>
    public class UpgradeScreen : BaseScreen
    {
        public override ScreenLayer Layer => ScreenLayer.Overlay;

        [SerializeField] private List<Button> _cardButtons = new();
        [SerializeField] private List<Image> _cardIcons = new();
        [SerializeField] private List<TMP_Text> _cardLabels = new();

        // Index order matches UpgradeStat's declaration order (MoveSpeed, Mobility, AttackSpeed, Armor, Damage, Health).
        [SerializeField] private List<Sprite> _upgradeIcons = new();
        [SerializeField] private List<string> _upgradeTexts = new();

        private PlayerStatsService _stats;
        private PlayerConfig _playerConfig;
        private IScreenManager _screenManager;

        private readonly List<UpgradeStat> _offeredUpgrades = new();

        public IReadOnlyList<UpgradeStat> OfferedUpgrades => _offeredUpgrades;

        [Inject]
        public void Construct(PlayerStatsService stats, PlayerConfig playerConfig, IScreenManager screenManager)
        {
            _stats = stats;
            _playerConfig = playerConfig;
            _screenManager = screenManager;
        }

        // Captures the card's fixed index, not the stat itself - OfferedUpgrades[index] changes every OnOpen().
        private void Awake()
        {
            for (int i = 0; i < _cardButtons.Count; i++)
            {
                int index = i;
                if (_cardButtons[i] != null)
                    _cardButtons[i].onClick.AddListener(() => SelectUpgradeAtIndex(index));
            }
        }

        public override void OnOpen()
        {
            RegenerateOfferedUpgrades();
            RefreshCards();
            Time.timeScale = 0f;
        }

        /// <summary>Resumes gameplay (paused in <see cref="OnOpen"/>) when the player picks a card or the
        /// screen is otherwise closed.</summary>
        public override UniTask OnClose()
        {
            Time.timeScale = 1f;
            return base.OnClose();
        }

        private void RefreshCards()
        {
            for (int i = 0; i < _offeredUpgrades.Count; i++)
            {
                int upgradeIndex = (int)_offeredUpgrades[i];

                if (i < _cardIcons.Count && _cardIcons[i] != null && upgradeIndex < _upgradeIcons.Count)
                    _cardIcons[i].sprite = _upgradeIcons[upgradeIndex];

                if (i < _cardLabels.Count && _cardLabels[i] != null && upgradeIndex < _upgradeTexts.Count)
                    _cardLabels[i].text = _upgradeTexts[upgradeIndex];
            }
        }

        private void SelectUpgradeAtIndex(int index)
        {
            if (index < 0 || index >= _offeredUpgrades.Count)
                return;

            SelectUpgrade(_offeredUpgrades[index]);
        }

        // Draws N of the enum values with no repeats within one draw by removing each pick from the pool.
        private void RegenerateOfferedUpgrades()
        {
            var pool = new List<UpgradeStat>((UpgradeStat[])Enum.GetValues(typeof(UpgradeStat)));
            _offeredUpgrades.Clear();

            for (int i = 0; i < _playerConfig.UpgradeChoicesPerLevelUp && pool.Count > 0; i++)
            {
                int index = Random.Range(0, pool.Count);
                _offeredUpgrades.Add(pool[index]);
                pool.RemoveAt(index);
            }
        }

        public void SelectUpgrade(UpgradeStat stat)
        {
            _stats.ApplyUpgrade(stat);
            _screenManager.CloseScreen<UpgradeScreen>().Forget();
        }
    }
}
