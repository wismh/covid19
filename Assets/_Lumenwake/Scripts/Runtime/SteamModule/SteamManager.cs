// Steamworks.NET bootstrap — based on the public-domain SteamManager sample.
// https://github.com/rlabrecque/Steamworks.NET-SteamManager

#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Project.Core.SteamModule
{
    [DisallowMultipleComponent]
    public sealed class SteamManager : MonoBehaviour
    {
#if !DISABLESTEAMWORKS
        private static bool _everInitialized;
        private static SteamManager _instance;

        private bool _initialized;
        private SteamAPIWarningMessageHook_t _steamApiWarningMessageHook;

        public static bool Initialized => _instance != null && _instance._initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _everInitialized = false;
            _instance = null;
        }

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (_everInitialized)
                throw new System.Exception("Tried to Initialize the SteamAPI twice in one session!");

            DontDestroyOnLoad(gameObject);

            if (!Packsize.Test())
                Debug.LogError("[Steamworks.NET] Packsize Test returned false.", this);

            if (!DllCheck.Test())
                Debug.LogError("[Steamworks.NET] DllCheck Test returned false.", this);

            try
            {
                if (SteamAPI.RestartAppIfNecessary(new AppId_t(SteamAppId.Value)))
                {
                    Application.Quit();
                    return;
                }
            }
            catch (System.DllNotFoundException e)
            {
                Debug.LogError(
                    "[Steamworks.NET] Could not load steam_api. Refer to the Steamworks.NET README.\n" + e,
                    this);
                Application.Quit();
                return;
            }

            _initialized = SteamAPI.Init();
            if (!_initialized)
            {
                Debug.LogError("[Steamworks.NET] SteamAPI_Init() failed.", this);
                return;
            }

            _everInitialized = true;
        }

        private void OnEnable()
        {
            if (_instance == null)
                _instance = this;

            if (!_initialized || _steamApiWarningMessageHook != null)
                return;

            _steamApiWarningMessageHook = SteamAPIDebugTextHook;
            SteamClient.SetWarningMessageHook(_steamApiWarningMessageHook);
        }

        private void OnDestroy()
        {
            if (_instance != this)
                return;

            _instance = null;

            if (!_initialized)
                return;

            SteamAPI.Shutdown();
        }

        private void Update()
        {
            if (!_initialized)
                return;

            SteamAPI.RunCallbacks();
        }

        [AOT.MonoPInvokeCallback(typeof(SteamAPIWarningMessageHook_t))]
        private static void SteamAPIDebugTextHook(int severity, System.Text.StringBuilder debugText) =>
            Debug.LogWarning(debugText);
#else
        public static bool Initialized => false;
#endif
    }
}
