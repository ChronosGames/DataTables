using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class UnitTestBuilder
{
    private const string OutputPath = "bin/UnitTest/StandaloneLinux64_IL2CPP/test";

    public static void BuildUnitTest()
    {
        var projectDirectory = Directory.GetParent(Application.dataPath);
        if (projectDirectory == null)
        {
            throw new InvalidOperationException("Could not determine the Unity project directory.");
        }

        var outputPath = Path.Combine(projectDirectory.FullName, OutputPath);
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(outputDirectory))
        {
            throw new InvalidOperationException($"Could not determine the unit test output directory from '{outputPath}'.");
        }

        Directory.CreateDirectory(outputDirectory);

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);

        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.EnableHeadlessMode
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"Unit test player build failed: {report.summary.result}");
        }
    }
}
