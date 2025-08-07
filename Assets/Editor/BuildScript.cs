using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Linq;

public class BuildScript
{
    public static void BuildWebGL()
    {
        Debug.Log("📦 Starting WebGL build...");

        // Use all enabled scenes from Build Settings
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        string outputPath = "Build";

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"✅ WebGL build succeeded! Size: {summary.totalSize} bytes");
        }
        else
        {
            Debug.LogError("❌ WebGL build failed.");
            EditorApplication.Exit(1); // Exit with error code for Jenkins/CLI
        }
    }
}

