using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Gera uma build Windows para o benchmark de FPS.
/// Linha de comandos: Unity.exe -batchmode -quit -projectPath ... -executeMethod BenchmarkBuild.Build -buildOut C:\pasta\WheelchairSim.exe
/// Tambem disponivel no menu Tools > Benchmark > Build Windows.
/// </summary>
public static class BenchmarkBuild
{
    [MenuItem("Tools/Benchmark/Build Windows")]
    public static void Build()
    {
        string outPath = null;
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-buildOut") outPath = args[i + 1];
        if (string.IsNullOrEmpty(outPath)) outPath = "Builds/Benchmark/WheelchairSim.exe";

        // -development: build de desenvolvimento (o benchmark consegue dizer o que causou cada engasgo)
        bool development = System.Array.IndexOf(args, "-development") >= 0;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = outPath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = development ? BuildOptions.Development : BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[BenchmarkBuild] Resultado: {report.summary.result} | {report.summary.totalTime} | {outPath}");

        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
