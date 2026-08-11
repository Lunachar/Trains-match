using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SortingStation.EditorTools
{
    public static class BuildAutomation
    {
        private const string AndroidOutput = "Builds/Android/SortingStation.apk";
        private const string WindowsOutput = "Builds/Windows/SortingStation/SortingStation.exe";

        [MenuItem("Sorting Station/Build/Android APK")]
        public static void BuildAndroidApk()
        {
            ProjectBootstrapper.BuildProject();
            Build(AndroidOutput, BuildTarget.Android, BuildOptions.None);
        }

        [MenuItem("Sorting Station/Build/Android APK (Development)")]
        public static void BuildAndroidDevelopment()
        {
            ProjectBootstrapper.BuildProject();
            Build(AndroidOutput, BuildTarget.Android, BuildOptions.Development);
        }

        [MenuItem("Sorting Station/Build/Android Build, Install and Run")]
        public static void BuildInstallRunAndroid()
        {
            BuildAndroidApk();
            string adb = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "SDK", "platform-tools", "adb.exe");
            if (!File.Exists(adb)) throw new FileNotFoundException("Unity ADB was not found.", adb);
            Run(adb, "devices");
            Run(adb, "install -r \"" + Path.GetFullPath(AndroidOutput) + "\"");
            Run(adb, "shell am force-stop com.lunacharprod.sortingstation");
            Run(adb, "shell monkey -p com.lunacharprod.sortingstation 1");
        }

        [MenuItem("Sorting Station/Build/Windows x64")]
        public static void BuildWindows()
        {
            ProjectBootstrapper.BuildProject();
            Build(WindowsOutput, BuildTarget.StandaloneWindows64, BuildOptions.None);
        }

        public static void BuildAll()
        {
            BuildAndroidApk();
            BuildWindows();
        }

        private static void Build(string relativePath, BuildTarget target, BuildOptions options)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No enabled scenes.");
            string fullPath = Path.GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullPath,
                target = target,
                options = options
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"{target} build failed: {report.summary.result}");
            }
            Debug.Log($"Built {target}: {fullPath}");
        }

        private static string Run(string executable, string arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using Process process = Process.Start(info);
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException(output);
            Debug.Log(output);
            return output;
        }
    }
}
