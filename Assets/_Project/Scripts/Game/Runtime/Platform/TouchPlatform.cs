using UnityEngine;

namespace _Project.Platform
{
    /// <summary>Single source of truth for "are we on a touch/mobile platform" for UI activation and layout.</summary>
    public static class TouchPlatform
    {
        public static bool ForceTouchPlatformInEditor = false;

        public static bool IsTouchPlatform => Application.isMobilePlatform || ForceTouchPlatformInEditor;
    }
}
