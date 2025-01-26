namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>Keys the per-variant pool/factory in MicroVirusesInstaller and gates wave-unlock timing in
    /// MicroVirusSpawner. Which behavior/config a virus uses is decided by its concrete MicroVirusController
    /// subclass, not this enum.</summary>
    public enum MicroVirusVariant
    {
        Orange,
        Cucumber,
        Spiner
    }
}
