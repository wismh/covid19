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
    /// <summary>
    /// Ports GMTK.UI.UpgradesHUD (Assets/GMTK/Scripts/UI/UpgradesHUD.cs) - the level-up upgrade card picker.
    /// The old `UpgradeType` enum was already replaced in Phase 2 by `_Project.Mechanics.Player.UpgradeStat`; this
    /// class ports the card-draw logic itself (OnEnable's random-3-of-6-no-repeats loop) and the click handler
    /// (OnClick -&gt; PlayerStats.Upgrade + HUD.UpgradeDialog(false)).
    ///
    /// --- Card wiring: reversed from the original "stay UI-agnostic" decision (Phase 8 testing fix, 2026-09-11) ---
    /// This class used to expose no Sprite/Image/Button fields at all, deliberately leaving "whoever wires the
    /// actual Canvas/3-card prefab in the Editor" to hook Button.onClick -&gt; SelectUpgrade(stat) themselves.
    /// Nobody ever did: user-reported crash (`KeyNotFoundException` on `ServiceLocator.Get&lt;GMTK.UI.HUD&gt;()`)
    /// traced back to the 3 card buttons under "HUD/UpgradeGroup/Upgrades" having ZERO persistent onClick
    /// listeners (confirmed live via Button.onClick.GetPersistentEventCount()) - the OLD GMTK.UI.UpgradesHUD
    /// component (still enabled on that same GameObject) was the ONLY thing ever actually responding to clicks,
    /// via its own runtime `AddListener` call in Awake(). It applied the upgrade through the OLD, disconnected
    /// GMTK.Services.PlayerStats (never bridged to this project's PlayerStatsService - unlike AddExperience,
    /// there's no OnUpgrade legacy bridge in MainSceneComposition), then crashed reaching for the no-longer-
    /// present GMTK.UI.HUD service (removed this same round to fix the HUD-statistics-corruption bug - see
    /// REFACTORING_PLAN.md's tenth-round entry). In other words: picking an upgrade had been a complete
    /// no-op against the real PlayerStatsService this whole time, silently, until removing GMTK.UI.HUD turned
    /// the silent no-op into a crash. Removed GMTK.UI.UpgradesHUD entirely (matching every other "old component
    /// shadowing a new one" fix this migration has made) and moved real wiring in here instead - this class is
    /// the one BaseScreen already responsible for the data (OfferedUpgrades), and HudScreen (Phase 5) already
    /// establishes the precedent that a BaseScreen owns its own Image/TMP_Text widget references directly rather
    /// than delegating to a separate presenter, so this follows that same convention instead of inventing a new
    /// one. <see cref="_cardButtons"/>/<see cref="_cardIcons"/>/<see cref="_cardLabels"/> mirror old
    /// UpgradesHUD's own `transform.GetChild(i)`-based Button/Image/TextMeshProUGUI lookups, just as explicit
    /// [SerializeField]s instead (this migration's established convention - see CellVisuals/PlayerHealth for the
    /// same GetChild-to-SerializeField fix applied elsewhere). <see cref="_upgradeIcons"/>/
    /// <see cref="_upgradeTexts"/> recover the exact Sprite/string data old UpgradesHUD had serialized (read
    /// directly from SampleScene.unity's YAML before it was lost by removing that component, not re-authored
    /// blind) - index order matches UpgradeStat's declaration order 1:1, which is itself already documented as a
    /// direct port of the old UpgradeType order (see UpgradeStat's own doc comment).
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

        // See class doc comment's "Card wiring" section. One entry per visible card (old: `transform.GetChild(i)`
        // for i in 0..3); _cardIcons/_cardLabels are each card's own Image/TextMeshProUGUI child, same index order.
        [SerializeField] private List<Button> _cardButtons = new();
        [SerializeField] private List<Image> _cardIcons = new();
        [SerializeField] private List<TMP_Text> _cardLabels = new();

        // See class doc comment - recovered verbatim from old GMTK.UI.UpgradesHUD's serialized values, index
        // order matches UpgradeStat's declaration order (MoveSpeed, Mobility, AttackSpeed, Armor, Damage, Health).
        [SerializeField] private List<Sprite> _upgradeIcons = new();
        [SerializeField] private List<string> _upgradeTexts = new();

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

        /// <summary>Old: GMTK.UI.UpgradesHUD.Awake() - `element.GetComponent&lt;Button&gt;().onClick.AddListener
        /// (() =&gt; OnClick(index));`, one persistent-for-the-component's-lifetime listener per card, capturing
        /// its own fixed index (not the stat itself, since OfferedUpgrades[index] changes every OnOpen()).</summary>
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
        }

        /// <summary>Old: GMTK.UI.UpgradesHUD.OnEnable()'s `_images[i].sprite = ...; _texts[i].text = ...;`
        /// half - see class doc comment for where _upgradeIcons/_upgradeTexts came from.</summary>
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
