namespace _Project.Mechanics.Player
{
    /// <summary>
    /// Matches GMTK.UI.UpgradeType (Assets/GMTK/Scripts/UI/UpgradesHUD.cs) one-to-one, renamed from
    /// SCREAMING_SNAKE_CASE with a redundant "PLAYER_" prefix to this project's PascalCase enum convention
    /// (see e.g. Project.Core.AssetLoaderModule.AssetLoadSource). PLAYER_HP became Health for readability;
    /// every other member is a direct casing port:
    /// PLAYER_MOVE_SPEED -> MoveSpeed, PLAYER_MOBILITY -> Mobility, PLAYER_ATTACK_SPEED -> AttackSpeed,
    /// PLAYER_ARMOR -> Armor, PLAYER_DAMAGE -> Damage, PLAYER_HP -> Health.
    /// </summary>
    public enum UpgradeStat
    {
        MoveSpeed,
        Mobility,
        AttackSpeed,
        Armor,
        Damage,
        Health
    }
}
