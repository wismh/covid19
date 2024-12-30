namespace _Project.Mechanics.Viruses.Macro
{
    /// <summary>
    /// Which macro-virus prefab variant to spawn - mirrors _Project.Mechanics.Viruses.Micro.MicroVirusVariant's
    /// role, used to key MacroVirusSpawner's per-variant Zenject pool factories (see VirusesInstaller).
    /// </summary>
    public enum MacroVirusVariant
    {
        Virus,
        Glist
    }
}
