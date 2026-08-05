using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Potshot.EditorTools
{
    /// <summary>Player builds. Scene lists are passed explicitly — nothing
    /// leaks via EditorBuildSettings (QA scenes stay out of builds).</summary>
    public static class Builder
    {
        public static void BuildMacDev()
        {
            // The subtarget is GLOBAL editor state — BuildLinuxServer sets
            // Server and it sticks, breaking this build (M3).
            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
            // Client boots into the menu; the SERVER build list (below)
            // deliberately excludes it.
            var scenes = new System.Collections.Generic.List<string>
                { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/DevArena.unity" };
            scenes.AddRange(System.IO.Directory
                .GetFiles("Assets/Scenes/Maps", "*.unity")
                .OrderBy(p => p));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = "Builds/PotshotDev.app",
                target = BuildTarget.StandaloneOSX,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.Development,
            });
            Report(report);
        }

        /// <summary>Headless Linux dedicated server. Mono backend (no Linux
        /// IL2CPP toolchain on this Mac). Version tagging lives on the
        /// Docker image (git sha) — GameVersion stays a source constant,
        /// the -potshotVersion arg is informational only (M3 review).</summary>
        public static void BuildLinuxServer()
        {
            string outDir = Arg("-potshotOut") ?? "server/build";
            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
            var scenes = new System.Collections.Generic.List<string>
                { "Assets/Scenes/DevArena.unity" };
            scenes.AddRange(System.IO.Directory
                .GetFiles("Assets/Scenes/Maps", "*.unity")
                .OrderBy(p => p));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = $"{outDir}/potshot-server.x86_64",
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
            });
            Report(report);
        }

        static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static void Report(BuildReport report)
        {
            var s = report.summary;
            Debug.Log($"[Builder] {s.result}: {s.outputPath} " +
                      $"({s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors)");
            if (s.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
