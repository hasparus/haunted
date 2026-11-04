using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Demons.Editor
{
    // Headless builds for any platform, from tools/unity:
    //   tools/unity build StandaloneLinux64 [--dev]
    // which runs Unity -batchmode -buildTarget <target> -executeMethod Demons.Editor.BuildScript.Build [-buildPath <dir>] [-development].
    // -buildTarget switches the platform before scripts load, so the build never reimports halfway through.
    public static class BuildScript
    {
        const string AppName = "My Demons Inc";

        public static void WebGL() => Build(BuildTarget.WebGL, "Builds/WebGL");

        // F8 ends a Survival run in development builds.
        public static void WebGLDev() => Build(BuildTarget.WebGL, "Builds/WebGLDev", BuildOptions.Development);

        // Builds the active platform into -buildPath (Builds/<target> by default).
        public static void Build()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var options = Flag("-development") ? BuildOptions.Development : BuildOptions.None;
            Build(target, Arg("-buildPath") ?? $"Builds/{target}", options);
        }

        static void Build(BuildTarget target, string folder, BuildOptions options = BuildOptions.None)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(scenes, Output(target, folder), target, options);
            var summary = report.summary;
            Debug.Log($"[BuildScript] {target} {summary.result}: {summary.outputPath}, {summary.totalSize / 1048576f:0.0} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalTime.TotalSeconds:0}s");
            if (summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        // Where the player goes inside the folder: platforms that build a single file or bundle need its name.
        static string Output(BuildTarget target, string folder) => target switch
        {
            BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneWindows => $"{folder}/{AppName}.exe",
            BuildTarget.StandaloneOSX => $"{folder}/{AppName}.app",
            BuildTarget.StandaloneLinux64 => $"{folder}/{AppName}.x86_64",
            BuildTarget.Android => $"{folder}/my-demons-inc.{(EditorUserBuildSettings.buildAppBundle ? "aab" : "apk")}",
            _ => folder,
        };

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static bool Flag(string name) => Environment.GetCommandLineArgs().Contains(name);
    }
}
