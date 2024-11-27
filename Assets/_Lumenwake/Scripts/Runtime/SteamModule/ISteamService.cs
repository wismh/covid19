namespace Project.Core.SteamModule
{
    public interface ISteamService
    {
        bool IsAvailable { get; }

        ulong SteamId { get; }

        string PersonaName { get; }
    }
}
