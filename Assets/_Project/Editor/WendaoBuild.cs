using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wendao.Editor
{
    public static class WendaoBuild
    {
        public const string DefaultMacosOutput =
            "Builds/macOS/WendaoChangsheng.app";
        public const string DefaultReportOutput =
            "TestResults/G07-04-build.json";

        [Serializable]
        private sealed class BuildEvidence
        {
            public string unityVersion = string.Empty;
            public string target = string.Empty;
            public string outputPath = string.Empty;
            public string result = string.Empty;
            public ulong totalSizeBytes;
            public double durationSeconds;
            public int warningCount;
            public int errorCount;
            public string[] scenes = Array.Empty<string>();
        }

        [MenuItem("问道长生/Build/macOS Release")]
        public static void BuildMacos()
        {
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string outputPath = ResolvePath(
                projectRoot,
                GetArgument("-wendaoBuildPath", DefaultMacosOutput));
            string reportPath = ResolvePath(
                projectRoot,
                GetArgument("-wendaoBuildReport", DefaultReportOutput));
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length != 6)
            {
                throw new BuildFailedException(
                    $"Expected 6 enabled MVP scenes, found {scenes.Length}.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            PlayerSettings.companyName = "Wendao Studio";
            PlayerSettings.productName = "问道长生";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultScreenWidth = 2560;
            PlayerSettings.defaultScreenHeight = 1440;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Standalone,
                "com.wendao.changsheng");

            BuildReport report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneOSX,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None
                });
            BuildSummary summary = report.summary;
            var evidence = new BuildEvidence
            {
                unityVersion = Application.unityVersion,
                target = summary.platform.ToString(),
                outputPath = MakeProjectRelative(projectRoot, outputPath),
                result = summary.result.ToString(),
                totalSizeBytes = summary.totalSize,
                durationSeconds = summary.totalTime.TotalSeconds,
                warningCount = summary.totalWarnings,
                errorCount = summary.totalErrors,
                scenes = scenes
            };
            File.WriteAllText(
                reportPath,
                JsonUtility.ToJson(evidence, true));

            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"macOS build failed: {summary.result}; errors={summary.totalErrors}.");
            }

            Debug.Log(
                $"Wendao macOS release build succeeded: {outputPath}; "
                + $"size={summary.totalSize} bytes; warnings={summary.totalWarnings}.");
        }

        private static string GetArgument(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.Ordinal))
                {
                    return args[index + 1];
                }
            }

            return fallback;
        }

        private static string ResolvePath(string projectRoot, string value)
        {
            return Path.GetFullPath(
                Path.IsPathRooted(value)
                    ? value
                    : Path.Combine(projectRoot, value));
        }

        private static string MakeProjectRelative(
            string projectRoot,
            string value)
        {
            string root = projectRoot.TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return value.StartsWith(root, StringComparison.Ordinal)
                ? value.Substring(root.Length)
                : value;
        }
    }
}
