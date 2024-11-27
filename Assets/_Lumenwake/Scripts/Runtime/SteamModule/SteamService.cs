#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

namespace Project.Core.SteamModule
{
    public sealed class SteamService : ISteamService
    {
        public bool IsAvailable =>
#if !DISABLESTEAMWORKS
            SteamManager.Initialized;
#else
            false;
#endif

        public ulong SteamId
        {
            get
            {
#if !DISABLESTEAMWORKS
                if (!IsAvailable)
                    return 0;

                return Steamworks.SteamUser.GetSteamID().m_SteamID;
#else
                return 0;
#endif
            }
        }

        public string PersonaName
        {
            get
            {
#if !DISABLESTEAMWORKS
                if (!IsAvailable)
                    return string.Empty;

                return Steamworks.SteamFriends.GetPersonaName();
#else
                return string.Empty;
#endif
            }
        }
    }
}
