using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Demons.Editor
{
    // Unity -batchmode -projectPath . -executeMethod Demons.Editor.BuildScript.WebGL -quit
    public static class BuildScript
    {
        public static void WebGL() => Build(BuildTarget.WebGL, "Builds/WebGL");

        static void Build(BuildTarget target, string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(scenes, path, target, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
