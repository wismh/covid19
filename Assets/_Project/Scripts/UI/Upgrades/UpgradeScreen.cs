using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Lumenwake.UIModule;
using Zenject;
using _Project.Mechanics.Player;
using Random = UnityEngine.Random;

namespace _Project.UI.Upgrades
{
    /// <summary>
    /// Ports GMTK.UI.UpgradesHUD (Assets/GMTK/Scripts/UI/UpgradesHUD.cs) - the level-up upgrade card picker.
    /// The old `UpgradeType` enum was already replaced in Phase 2 by `_Project.Mechanics.Player.UpgradeStat`; this
    /// class ports the card-draw logic itself (OnEnable's random-3-of-6-no-repeats loop) and the click handler
    /// (OnClick -&gt; PlayerStats.Upgrade + HUD.UpgradeDialog(false)).
    ///
    /// No Sprite/Image/Button [SerializeField] fields are exposed here on purpose - the old class held its own
    /// per-card Image/TextMeshProUGUI/Button references directly (`transform.GetChild(i)`), but per the phase
    /// brief this screen only needs to expose the DATA (which 3 UpgradeStat values are on offer right now) via
    /// <see cref="OfferedUpgrades"/> plus a single <see cref="SelectUpgrade"/> entry point. Whoever wires the
    /// actual Canvas/3-card prefab in the Editor drives its own Button.onClick -&gt; SelectUpgrade(stat) and reads
    /// OfferedUpgrades[i] to know which icon/label to show per card - this keeps the screen agnostic of how many
    /// visual card widgets exist or how they're laid out, which the old per-index Image/Text field list wasn't.
    ///
    /// See HudScreen's class doc comment ("Auto-opening" section) for why HudScreen - not this class - is the one
    /// that subscribes to PlayerStatsService.OnLevelUp and calls `screenManager.OpenScreen&lt;UpgradeScreen&gt;()`.
    /// This screen's own OnOpen() instead does the thing the phase brief explicitly asks for regardless of who
    /// triggered the open: "expose them ... regenerated each time the screen opens" - OfferedUpgrades is redrawn
    /// fresh on every OnOpen(), exactly mirroring old UpgradesHUD.OnEnable()'s draw-on-every-enable behavior.
    /// </summary>
    public class UpgradeScreen : BaseScreen
    {
        public override ScreenLayer Layer => ScreenLayer.Overlay;

        private PlayerStatsService _stats;
        private IScreenManager _screenManager;

        private readonly List<UpgradeStat> _offeredUpgrades = new();

        /// <summary>The 3 stats currently on offer, redrawn every time this screen opens. Old: the 3-element
        /// `_upgradeTypes` list in GMTK.UI.UpgradesHUD, redrawn in OnEnable().</summary>
        public IReadOnlyList<UpgradeStat> OfferedUpgrades => _offeredUpgrades;

        /// <summary>See HudScreen.Construct's doc comment - PlayerStatsService is a Mechanics/* POCO outside
        /// Zenject binding today, supplied by a future composition root. IScreenManager is a real binding.</summary>
        [Inject]
        public void Construct(PlayerStatsService stats, IScreenManager screenManager)
        {
            _stats = stats;
            _screenManager = screenManager;
        }

        public override void OnOpen()
        {
            RegenerateOfferedUpgrades();
        }

        /// <summary>
        /// Old: GMTK.UI.UpgradesHUD.OnEnable() -
        /// `var types = new List&lt;UpgradeType&gt;(Enum.GetValues(...)); for (i in 0..3) { at =
        /// Random.Range(0, types.Count); _upgradeTypes[i] = types[at]; types.RemoveAt(at); }` - draws 3 of the 6
        /// enum values with no repeats within one draw by removing each pick from the candidate pool. Ported
        /// exactly, just over UpgradeStat instead of the old UpgradeType.
        /// </summary>
        private void RegenerateOfferedUpgrades()
        {
            var pool = new List<UpgradeStat>((UpgradeStat[])Enum.GetValues(typeof(UpgradeStat)));
            _offeredUpgrades.Clear();

            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int index = Random.Range(0, pool.Count);
                _offeredUpgrades.Add(pool[index]);
                pool.RemoveAt(index);
            }
        }

        /// <summary>Old: GMTK.UI.UpgradesHUD.OnClick(int) -
        /// `ServiceLocator.Instance.Get&lt;PlayerStats&gt;().Upgrade(_upgradeTypes[index]); ServiceLocator.Instance.
        /// Get&lt;HUD&gt;().UpgradeDialog(false);` - applies the chosen upgrade then closes this screen, replacing
        /// the old direct HUD.UpgradeDialog(false) call with the new IScreenManager.CloseScreen&lt;T&gt;() API.</summary>
        public void SelectUpgrade(UpgradeStat stat)
        {
            _stats.ApplyUpgrade(stat);
            _screenManager.CloseScreen<UpgradeScreen>().Forget();
        }
    }
}
