#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Project.Core.SteamModule.Editor
{
    public static class SteamAppIdBuildProcessor
    {
        [PostProcessBuild(1)]
        public static void CopySteamAppIdFile(BuildTarget target, string pathToBuiltProject)
        {
            if (target is not (BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneWindows
                    or BuildTarget.StandaloneLinux64 or BuildTarget.StandaloneOSX))
                return;

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var source = Path.Combine(projectRoot, "steam_appid.txt");
            if (!File.Exists(source))
                return;

            var destinationDirectory = target == BuildTarget.StandaloneOSX
                ? Path.Combine(pathToBuiltProject, "Contents", "MacOS")
                : Path.GetDirectoryName(pathToBuiltProject);

            if (string.IsNullOrEmpty(destinationDirectory))
                return;

            File.Copy(source, Path.Combine(destinationDirectory, "steam_appid.txt"), overwrite: true);
        }
    }
}
#endif
