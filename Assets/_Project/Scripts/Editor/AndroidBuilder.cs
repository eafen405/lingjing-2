using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Lingjing.Editor
{
    /// <summary>
    /// 一条命令出 APK：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.AndroidBuilder.Build
    /// 竖切阶段用 Mono2x + ARM64，保证编译快、包能装。
    /// </summary>
    public static class AndroidBuilder
    {
        const string BundleId = "cn.lingjing.eous";
        const string OutputPath = "Build/M1.apk";

        public static void Build()
        {
            Directory.CreateDirectory("Build");

            // 必须先切到 Android，否则 PlayerSettings.Android.* 会被忽略。
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                Debug.Log($"[apk] switch to Android: {switched}");
            }

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard_2_0);
            Debug.Log($"[apk] arch={PlayerSettings.Android.targetArchitectures} backend={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}");
            EditorUserBuildSettings.buildAppBundle = false;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { M1SceneBuilder.ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log($"[apk] result={summary.result} size={summary.totalSize / (1024 * 1024)}MB errors={summary.totalErrors} warnings={summary.totalWarnings} path={OutputPath}");

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[apk] BUILD FAILED");
                EditorApplication.Exit(1);
            }
        }
    }
}
