using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class UnitTestBuilder
{
    private const string OutputPath = "bin/UnitTest/StandaloneLinux64_IL2CPP/test";

    public static void BuildUnitTest()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        var outputPath = Path.Combine(projectRoot, OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);

        var options = new BuildPlayerOptions
        {
            scenes = Array.Empty<string>(),
            locationPathName = outputPath,
            target = BuildTarget.StandaloneLinux64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.EnableHeadlessMode
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new Exception($"Unit test player build failed: {report.summary.result}");
        }
    }
}
