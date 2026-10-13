using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Benchmark de FPS por nivel. So corre se o jogo for lancado com "-benchmark"
/// (ex.: WheelchairSimulator.exe -benchmark), opcional -gfxQuality Alta|Media|Baixa. Em jogo normal nao faz nada.
///
/// Para cada nivel: espera que estabilize, roda a cadeira 360 graus devagar (para desenhar
/// a cena em todas as direcoes, com transito e peoes a mexer) e regista o tempo de cada frame.
/// Escreve o relatorio em [persistentDataPath]/benchmark_[data].txt (ou no caminho de "-benchmarkOut").
/// </summary>
public class PerformanceBenchmark : MonoBehaviour
{
    private const float WarmupSeconds = 4f;
    private const float MeasureSeconds = 12f;

    private static readonly string[] Levels =
    {
        "Level1", "Level2", "Level3", "Level4", "Level5", "Level6",
        "Level7", "Level8", "Level9", "Level10", "Level11"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoStart()
    {
        if (!HasArg("-benchmark")) return;

        GameObject go = new GameObject("PerformanceBenchmark");
        DontDestroyOnLoad(go);
        go.AddComponent<PerformanceBenchmark>();
    }

    private static bool HasArg(string name)
    {
        foreach (string a in System.Environment.GetCommandLineArgs())
            if (a == name) return true;
        return false;
    }

    private static string ArgValue(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    private IEnumerator Start()
    {
        // Medir sem limite de FPS
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        StringBuilder report = new StringBuilder();
        report.AppendLine("Benchmark de FPS - Wheelchair Simulator (versao PC)");
        report.AppendLine($"Data: {System.DateTime.Now:yyyy-MM-dd HH:mm}");
        report.AppendLine($"CPU: {SystemInfo.processorType} ({SystemInfo.processorCount} threads)");
        report.AppendLine($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB) - {SystemInfo.graphicsDeviceType}");
        report.AppendLine($"RAM: {SystemInfo.systemMemorySize} MB | Resolucao: {Screen.width}x{Screen.height} | Qualidade: {QualitySettings.names[QualitySettings.GetQualityLevel()]} | Graficos PC: {PCGraphicsQuality.CurrentTier}");
        report.AppendLine($"Por nivel: {WarmupSeconds}s de aquecimento + {MeasureSeconds}s a rodar a cadeira 360 graus. VSync desligado.");
        report.AppendLine();
        report.AppendLine("Nivel    | FPS medio | FPS 1% pior | Frame medio | Frame max | Frames >13.9ms (72Hz) | Draw calls* ");
        report.AppendLine("---------|-----------|-------------|-------------|-----------|-----------------------|------------");

        List<string> hitchLog = new List<string>();
        StartRecorders();

        string onlyLevels = ArgValue("-benchmarkLevels"); // ex.: Level5,Level7
        foreach (string level in Levels)
        {
            if (!string.IsNullOrEmpty(onlyLevels) && System.Array.IndexOf(onlyLevels.Split(','), level) < 0) continue;

            Time.timeScale = 1f;
            AsyncOperation load = SceneManager.LoadSceneAsync(level);
            while (!load.isDone) yield return null;

            float warmEnd = Time.realtimeSinceStartup + WarmupSeconds;
            while (Time.realtimeSinceStartup < warmEnd) { Time.timeScale = 1f; yield return null; }

            // -benchmarkShots pasta: guarda uma imagem de cada nivel (para comparar a qualidade grafica)
            string shotDir = ArgValue("-benchmarkShots");
            if (!string.IsNullOrEmpty(shotDir))
            {
                Directory.CreateDirectory(shotDir);
                ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, $"{level}_{PCGraphicsQuality.CurrentTier}.png"));
                yield return null;
                yield return null;
            }

            Transform chair = FindActiveChair();
            Quaternion startRot = chair != null ? chair.rotation : Quaternion.identity;

            List<float> frameMs = new List<float>(4096);
            float start = Time.realtimeSinceStartup;
            float last = start;
            while (Time.realtimeSinceStartup - start < MeasureSeconds)
            {
                yield return null;
                float now = Time.realtimeSinceStartup;
                frameMs.Add((now - last) * 1000f);
                if ((now - last) * 1000f > HitchMs) hitchLog.Add(DescribeHitch(level, now - start, (now - last) * 1000f));
                last = now;

                Time.timeScale = 1f; // o painel de inicio/contagem pode pausar o jogo
                if (chair != null)
                {
                    float t = (now - start) / MeasureSeconds;
                    chair.rotation = startRot * Quaternion.Euler(0f, 360f * t, 0f);
                }
            }

            report.AppendLine(FormatRow(level, frameMs));
            Debug.Log("[Benchmark] " + level + " concluido");
        }

        report.AppendLine();
        report.AppendLine($"Engasgos (frames > {HitchMs} ms) e o que demorou nesse frame (so em builds de desenvolvimento):");
        if (hitchLog.Count == 0) report.AppendLine("  nenhum");
        foreach (string h in hitchLog) report.AppendLine("  " + h);
        DisposeRecorders();
        report.AppendLine();
        report.AppendLine("* Draw calls nao disponiveis em builds de release.");
        report.AppendLine("Nota: o Quest 3 e varias vezes mais lento que um PC; estes valores servem para comparar niveis entre si.");

        string outPath = ArgValue("-benchmarkOut");
        if (string.IsNullOrEmpty(outPath))
            outPath = Path.Combine(Application.persistentDataPath, $"benchmark_{System.DateTime.Now:yyyyMMdd_HHmm}.txt");

        File.WriteAllText(outPath, report.ToString(), new UTF8Encoding(true));
        Debug.Log("[Benchmark] Relatorio: " + outPath);
        Application.Quit();
    }

    // ==========================================
    // DIAGNOSTICO DE ENGASGOS
    // ==========================================

    private const float HitchMs = 25f;

    // Marcadores do Profiler que costumam causar engasgos
    private static readonly (ProfilerCategory category, string marker, string label)[] HitchMarkers =
    {
        (ProfilerCategory.Render, "Shader.CreateGPUProgram", "compilar shader"),
        (ProfilerCategory.Render, "CreateGPUProgram", "compilar shader (GPU)"),
        (ProfilerCategory.Render, "Shader.Parse", "ler shader"),
        (ProfilerCategory.Render, "Gfx.CreatePipelineState", "criar PSO"),
        (ProfilerCategory.Memory, "GC.Collect", "limpeza de memoria (GC)"),
        (ProfilerCategory.Loading, "Loading.ReadObject", "carregar asset"),
        (ProfilerCategory.Loading, "Loading.AwakeFromLoad", "inicializar asset"),
        (ProfilerCategory.Scripts, "BehaviourUpdate", "scripts Update"),
        (ProfilerCategory.Scripts, "CoroutinesDelayedCalls", "coroutines"),
        (ProfilerCategory.Physics, "Physics.Simulate", "fisica"),
        (ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread", "esperar pela GPU"),
        (ProfilerCategory.Render, "Gfx.UploadTexture", "enviar textura"),
        (ProfilerCategory.Render, "Mesh.UploadMeshData", "enviar mesh"),
    };

    private readonly List<ProfilerRecorder> recorders = new List<ProfilerRecorder>();
    private ProfilerRecorder gcAllocRecorder;

    private void StartRecorders()
    {
        foreach (var m in HitchMarkers)
            recorders.Add(ProfilerRecorder.StartNew(m.category, m.marker));
        gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
    }

    private void DisposeRecorders()
    {
        foreach (ProfilerRecorder r in recorders) r.Dispose();
        recorders.Clear();
        gcAllocRecorder.Dispose();
    }

    private string DescribeHitch(string level, float atSeconds, float frameMs)
    {
        StringBuilder sb = new StringBuilder($"{level} aos {atSeconds:F1}s: {frameMs:F1} ms ->");
        bool any = false;
        for (int i = 0; i < recorders.Count; i++)
        {
            if (!recorders[i].Valid) continue;
            double ms = recorders[i].LastValue / 1e6;
            if (ms < 2.0) continue;
            sb.Append($" {HitchMarkers[i].label} {ms:F1} ms;");
            any = true;
        }
        if (gcAllocRecorder.Valid && gcAllocRecorder.LastValue > 1024 * 1024)
            sb.Append($" alocou {gcAllocRecorder.LastValue / (1024f * 1024f):F1} MB;");
        if (!any) sb.Append(" (sem marcador conhecido)");
        return sb.ToString();
    }

    private static Transform FindActiveChair()
    {
        MovementPC pc = FindFirstObjectByType<MovementPC>(FindObjectsInactive.Exclude);
        if (pc != null) return pc.transform;
        MovementVR vr = FindFirstObjectByType<MovementVR>(FindObjectsInactive.Exclude);
        return vr != null ? vr.transform : null;
    }

    private static string FormatRow(string level, List<float> frameMs)
    {
        if (frameMs.Count == 0) return $"{level,-8} | sem frames";

        float sum = 0f, max = 0f;
        int over = 0;
        foreach (float ms in frameMs)
        {
            sum += ms;
            if (ms > max) max = ms;
            if (ms > 13.9f) over++;
        }
        float avgMs = sum / frameMs.Count;

        // FPS do 1% de frames mais lentos
        List<float> sorted = new List<float>(frameMs);
        sorted.Sort();
        int n1 = Mathf.Max(1, sorted.Count / 100);
        float worstSum = 0f;
        for (int i = sorted.Count - n1; i < sorted.Count; i++) worstSum += sorted[i];
        float low1Fps = 1000f / (worstSum / n1);

        return $"{level,-8} | {1000f / avgMs,9:F0} | {low1Fps,11:F0} | {avgMs,8:F2} ms | {max,6:F1} ms | {over,8} ({100f * over / frameMs.Count,5:F1}%)       | -";
    }
}
