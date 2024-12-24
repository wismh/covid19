namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>
    /// PascalCase replacement for GMTK.MicroViruses.InCellVirusBehaviorType (Assets/GMTK/Scripts/Behaviors/
    /// MicroVirus/InCellVirus.cs - `ORANGE, CUCUMBER, SPINER`), matching this project's enum-naming convention
    /// (see _Project.Mechanics.Player.UpgradeStat for the Phase 2 precedent). Selects which
    /// <see cref="IMicroVirusBehavior"/> MicroVirusController builds and identifies which
    /// MicroVirusConfig_&lt;Variant&gt; asset a given config instance represents.
    /// </summary>
    public enum MicroVirusVariant
    {
        Orange,
        Cucumber,
        Spiner
    }
}
