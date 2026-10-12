using System;
using UnityEngine;

/// <summary>
/// Cria e grava os registos de sessao (concluidas ou falhadas) no perfil do paciente ativo.
/// Usado pelo LevelManager_PC e pelo LevelManagerVR, para os dois gravarem exatamente os mesmos dados.
/// </summary>
public static class SessionRecorder
{
    public const string DateFormat = "yyyy-MM-dd HH:mm";

    public static SessionRecord Create(string levelName, float time, int collisions, int slides, bool isVR)
    {
        SessionRecord r = new SessionRecord();
        r.sessionDate = DateTime.Now.ToString(DateFormat);
        r.levelName = levelName;
        r.completionTime = time;
        r.totalCollisions = collisions;
        r.totalSlides = slides;
        r.platform = isVR ? "VR" : "PC";
        r.steeringType = DetectSteering();
        r.inputMode = isVR ? DetectVrInput() : DetectPcInput();
        return r;
    }

    /// <summary>
    /// Adiciona o registo ao paciente ativo e grava em disco. Devolve false se nao houver perfil ativo.
    /// </summary>
    public static bool Save(SessionRecord record)
    {
        PlayerData p = ProfileManager.Instance != null ? ProfileManager.Instance.currentPlayer : null;
        if (p == null)
        {
            Debug.LogWarning("Aviso: Nenhum perfil de paciente ativo detetado. Os dados clínicos não foram guardados.");
            return false;
        }

        p.sessionHistory.Add(record);
        bool ok = SaveManager.SaveProfile(p);
        if (ok) Debug.Log($"Sessão gravada no perfil '{p.profileID}' ({record.levelName}, falhou={record.failed}).");
        return ok;
    }

    private static string DetectSteering()
    {
        // Pode haver duas cadeiras na cena (PC e VR): usar a que esta ativa
        WheelController wc = UnityEngine.Object.FindFirstObjectByType<WheelController>(FindObjectsInactive.Exclude);
        if (wc != null) return wc.GetSteeringType().ToString();
        return SteeringPreference.CurrentSteering.ToString();
    }

    private static string DetectPcInput()
    {
        PlayerData p = ProfileManager.Instance != null ? ProfileManager.Instance.currentPlayer : null;
        int mode = p != null && p.inputSettings != null ? p.inputSettings.inputMode : 0;
        return ((MovementPC.InputMode)mode).ToString();
    }

    private static string DetectVrInput()
    {
        MovementVR m = UnityEngine.Object.FindFirstObjectByType<MovementVR>(FindObjectsInactive.Exclude);
        if (m == null) return "Comando";
        if (m.UsedHandJoystick && m.UsedController) return "Comando+Maos";
        return m.UsedHandJoystick ? "Maos" : "Comando";
    }
}
