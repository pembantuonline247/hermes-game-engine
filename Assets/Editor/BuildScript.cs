using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildScript
{
    public static void BuildPlayerWebGL()
    {
        BuildPlayer(BuildTargetGroup.WebGL, BuildTarget.WebGL, "Builds/WebGL");
    }

    public static void BuildPlayerStandaloneWindows64()
    {
        BuildPlayer(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64, "Builds/Windows");
    }

    public static void BuildPlayerAndroid()
    {
        BuildPlayer(BuildTargetGroup.Android, BuildTarget.Android, "Builds/Android");
    }

    private static void BuildPlayer(BuildTargetGroup group, BuildTarget target, string location)
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = location,
            targetGroup = group,
            target = target,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded: {summary.totalSize} bytes");
        }
        else
        {
            Debug.LogError($"Build failed: {summary.result}");
            EditorApplication.Exit(1);
        }
    }
}
