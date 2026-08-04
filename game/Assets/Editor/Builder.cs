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
            var scenes = new System.Collections.Generic.List<string>
                { "Assets/Scenes/DevArena.unity" };
            scenes.AddRange(System.IO.Directory
                .GetFiles("Assets/Scenes/Maps", "*.unity")
                .OrderBy(p => p));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = "Builds/PotshotDev.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development,
            });
            Report(report);
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
