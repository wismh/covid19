using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _Project.Platform
{
    /// <summary>Single source of truth for "are we on a touch/mobile platform" for UI activation and layout.</summary>
    public static class TouchPlatform
    {
#if UNITY_EDITOR
        private const string ForceTouchPlatformEditorPrefKey = "_Project.Platform.SimulateAndroidInEditor";

        public static bool ForceTouchPlatformInEditor
        {
            get => EditorPrefs.GetBool(ForceTouchPlatformEditorPrefKey, false);
            set => EditorPrefs.SetBool(ForceTouchPlatformEditorPrefKey, value);
        }
#else
        public static bool ForceTouchPlatformInEditor = false;
#endif

        public static bool IsTouchPlatform => Application.isMobilePlatform || ForceTouchPlatformInEditor;
    }
}
