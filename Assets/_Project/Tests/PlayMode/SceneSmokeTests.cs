using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Smoke test: carrega cada cena do jogo, deixa-a correr alguns segundos com o rig de PC
/// e depois com o rig de VR, e falha se houver alguma excecao.
/// Os erros (LogError) nao fazem falhar o teste - sao escritos num relatorio
/// (Logs/SmokeTestReport.txt) para comparar antes/depois de alteracoes.
/// Esta assembly nao pode referenciar o Assembly-CSharp, por isso usa reflexao.
/// </summary>
public class SceneSmokeTests
{
    private const float SecondsPerRig = 3f;

    private static readonly string[] Scenes =
    {
        "MainMenu", "Level1", "Level2", "Level3", "Level4", "Level5", "Level6",
        "Level7", "Level8", "Level9", "Level10", "Level11"
    };

    private readonly List<string> exceptions = new List<string>();
    private readonly List<string> errors = new List<string>();

    private static readonly string ReportPath = Path.Combine(Application.dataPath, "..", "Logs", "SmokeTestReport.txt");

    [OneTimeSetUp]
    public void ClearReport()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "");
    }

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        string firstStackLine = stackTrace != null ? stackTrace.Split('\n')[0] : "";
        if (type == LogType.Exception) exceptions.Add(condition + " @ " + firstStackLine);
        else if (type == LogType.Error || type == LogType.Assert) errors.Add(condition);
    }

    [UnityTest]
    public IEnumerator SceneRunsWithoutExceptions([ValueSource(nameof(Scenes))] string sceneName)
    {
        // Os erros sao registados no relatorio em vez de fazerem falhar o teste
        LogAssert.ignoreFailingMessages = true;
        exceptions.Clear();
        errors.Clear();
        Application.logMessageReceived += OnLog;

        try
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
            yield return null;
            yield return null;

            // 1) Rig por defeito (sem headset: PC)
            yield return RunFor(SecondsPerRig);

            // 2) Forcar o rig de VR (desliga o PlayerRigManager, que escolhe o rig todos os frames)
            bool hasVrRig = ForceVrRig();
            if (hasVrRig)
            {
                Time.timeScale = 1f;
                yield return RunFor(SecondsPerRig);
            }

            WriteReport(sceneName, hasVrRig);
            WritePivotReport(sceneName);
        }
        finally
        {
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
        }

        Assert.IsEmpty(exceptions, $"Exceções na cena {sceneName}:\n" + string.Join("\n", exceptions));
    }

    private static IEnumerator RunFor(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    private static bool ForceVrRig()
    {
        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null || mb.GetType().Name != "PlayerRigManager") continue;

            System.Type t = mb.GetType();
            GameObject pc = t.GetField("wheelchairPC", BindingFlags.Public | BindingFlags.Instance)?.GetValue(mb) as GameObject;
            GameObject vr = t.GetField("wheelchairVR", BindingFlags.Public | BindingFlags.Instance)?.GetValue(mb) as GameObject;
            if (vr == null) return false;

            mb.enabled = false;
            if (pc != null) pc.SetActive(false);
            vr.SetActive(true);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Regista a distancia ao eixo de rotacao medida nas rodas do modelo real (frontal / traseira),
    /// para confirmar que o modelo de rotacao usa valores fisicamente plausiveis.
    /// </summary>
    private static void WritePivotReport(string sceneName)
    {
        System.Type model = System.Type.GetType("WheelchairDriveModel, Assembly-CSharp");
        System.Type settingsType = System.Type.GetType("WheelchairTurnSettings, Assembly-CSharp");
        System.Type steeringType = System.Type.GetType("WheelController+SteeringType, Assembly-CSharp");
        if (model == null || settingsType == null || steeringType == null) return;

        MethodInfo compute = model.GetMethod("ComputePivotLocalZ");
        object settings = System.Activator.CreateInstance(settingsType);
        var sb = new StringBuilder();

        foreach (MonoBehaviour wc in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (wc == null || wc.GetType().Name != "WheelController") continue;
            object front = compute.Invoke(null, new[] { wc.transform, wc, System.Enum.ToObject(steeringType, 0), settings });
            object rear = compute.Invoke(null, new[] { wc.transform, wc, System.Enum.ToObject(steeringType, 1), settings });
            sb.AppendLine($"  PIVOT {wc.gameObject.name}: frontal={front:0.000} m  traseira={rear:0.000} m");
        }
        if (sb.Length > 0) File.AppendAllText(ReportPath, $"--- pivots {sceneName}\n{sb}\n");
    }

    private void WriteReport(string sceneName, bool hasVrRig)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {sceneName} (VR rig testado: {(hasVrRig ? "sim" : "não")}) ===");
        sb.AppendLine($"Exceções: {exceptions.Count}");
        foreach (string e in new HashSet<string>(exceptions)) sb.AppendLine("  EXC " + e);
        sb.AppendLine($"Erros: {errors.Count}");
        foreach (string e in new HashSet<string>(errors)) sb.AppendLine("  ERR " + e);
        sb.AppendLine();
        File.AppendAllText(ReportPath, sb.ToString());
    }
}
