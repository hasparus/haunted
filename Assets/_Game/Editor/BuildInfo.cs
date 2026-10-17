using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Demons.Editor
{
    // Bakes the commit into Resources/commit.txt; development builds show it next to the version.
    public class BuildInfo : IPreprocessBuildWithReport
    {
        const string Path = "Assets/_Game/Resources/commit.txt";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string sha = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? Git("rev-parse HEAD");
            if (string.IsNullOrEmpty(sha)) return;
            File.WriteAllText(Path, sha.Trim()[..Math.Min(7, sha.Trim().Length)]);
            AssetDatabase.ImportAsset(Path);
        }

        static string Git(string args)
        {
            try
            {
                using var git = Process.Start(new ProcessStartInfo("git", args) { RedirectStandardOutput = true, UseShellExecute = false });
                string output = git.StandardOutput.ReadToEnd();
                git.WaitForExit();
                return git.ExitCode == 0 ? output : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
