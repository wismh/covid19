using UnityEditor;
using _Project.Platform;

namespace _Project.Editor
{
    /// <summary>Editor menu toggle for TouchPlatform.ForceTouchPlatformInEditor, to preview the mobile/touch layout without a device.</summary>
    public static class SimulateAndroidMenu
    {
        private const string MenuPath = "Tools/Debug/Simulate Android (Touch Controls)";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            TouchPlatform.ForceTouchPlatformInEditor = !TouchPlatform.ForceTouchPlatformInEditor;
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, TouchPlatform.ForceTouchPlatformInEditor);
            return true;
        }
    }
}
