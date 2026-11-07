using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LowercaseUppercase.Editor
{
    /// <summary>
    /// Builds the Windows, Linux and Web players into Builds/. All three use the scene list from Build Settings and
    /// the project's single quality tier, so there are no per-platform graphics settings to keep in sync.
    /// From the command line: Unity -batchmode -quit -projectPath . -executeMethod LowercaseUppercase.Editor.PlayerBuilds.BuildAll
    /// </summary>
    public static class PlayerBuilds
    {
        const string OutputFolder = "Builds";

        [MenuItem("lowercase UPPERCASE/Build/Windows")]
        public static void BuildWindows() => Run(BuildTarget.StandaloneWindows64);

        [MenuItem("lowercase UPPERCASE/Build/Linux")]
        public static void BuildLinux() => Run(BuildTarget.StandaloneLinux64);

        [MenuItem("lowercase UPPERCASE/Build/Web")]
        public static void BuildWeb() => Run(BuildTarget.WebGL);

        [MenuItem("lowercase UPPERCASE/Build/All")]
        public static void BuildAll() =>
            Run(BuildTarget.StandaloneWindows64, BuildTarget.StandaloneLinux64, BuildTarget.WebGL);

        static void Run(params BuildTarget[] targets)
        {
            BuildTarget startingTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup startingGroup = BuildPipeline.GetBuildTargetGroup(startingTarget);

            bool succeeded = targets.All(Build);

            if (EditorUserBuildSettings.activeBuildTarget != startingTarget)
                EditorUserBuildSettings.SwitchActiveBuildTarget(startingGroup, startingTarget);

            if (!succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        static bool Build(BuildTarget target)
        {
            string location = LocationFor(target);
            Directory.CreateDirectory(Path.GetDirectoryName(location));

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = location,
                target = target,
                options = BuildOptions.None
            });

            BuildSummary summary = report.summary;
            string message = $"{target} build {summary.result}: {location}, {summary.totalSize / (1024f * 1024f):F1} MB, " +
                             $"{summary.totalErrors} errors, {summary.totalTime.TotalSeconds:F0} s";

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(message);
                return true;
            }

            Debug.LogError(message);
            return false;
        }

        static string LocationFor(BuildTarget target)
        {
            string name = PlayerSettings.productName;
            return target switch
            {
                BuildTarget.StandaloneWindows64 => $"{OutputFolder}/Windows/{name}.exe",
                BuildTarget.StandaloneLinux64 => $"{OutputFolder}/Linux/{name}.x86_64",
                BuildTarget.WebGL => $"{OutputFolder}/Web",
                _ => throw new System.ArgumentOutOfRangeException(nameof(target), target, null)
            };
        }
    }
}
