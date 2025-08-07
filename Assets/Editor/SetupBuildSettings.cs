using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

public class SetupBuildSettings
{
    public static void Setup()
    {
        Debug.Log("🛠️ Setting up WebGL build settings...");

        // Switch to WebGL platform
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            Debug.Log("✅ Switched to WebGL platform.");
        }

        // Enable all scenes in Assets/Scenes
        string[] scenePaths = Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories);
        EditorBuildSettings.scenes = scenePaths
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();

        Debug.Log($"✅ Added {scenePaths.Length} scenes to Build Settings.");
    }
}

