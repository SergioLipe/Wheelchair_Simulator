using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Texto do historico de tentativas (painel "Historico" dos menus PC e VR).
/// Sem dependencias de UI, para poder ser testado e partilhado pelos dois menus.
/// </summary>
public static class SessionHistoryFormatter
{
    /// <summary>Funcao de traducao: (chave, texto por defeito) -> texto traduzido.</summary>
    public delegate string Localizer(string key, string fallback);

    private static readonly Localizer NoTranslation = (key, fallback) => fallback;

    public static bool IsFreestyle(string levelName)
    {
        return levelName == "Freestyle" || levelName == "Level11" || levelName == "FreestyleLevel";
    }

    private static bool SameLevel(string recordLevel, string levelName)
    {
        if (IsFreestyle(levelName)) return IsFreestyle(recordLevel);
        return recordLevel == levelName;
    }

    public static int CountAttempts(PlayerData data, string levelName)
    {
        int count = 0;
        if (data == null || data.sessionHistory == null) return 0;
        foreach (SessionRecord r in data.sessionHistory)
        {
            if (SameLevel(r.levelName, levelName)) count++;
        }
        return count;
    }

    /// <summary>
    /// Formata a data guardada (varios formatos ao longo das versoes) como dd/MM/yyyy HH:mm.
    /// </summary>
    public static string FormatDate(string stored, string atWord = "às")
    {
        if (string.IsNullOrEmpty(stored)) return "";
        if (TryParseDate(stored, out DateTime d)) return d.ToString("dd/MM/yyyy '" + atWord + "' HH:mm", CultureInfo.InvariantCulture);
        return stored;
    }

    public static bool TryParseDate(string stored, out DateTime date)
    {
        if (DateTime.TryParseExact(stored, SessionRecorder.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return true;
        return DateTime.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date)
            || DateTime.TryParse(stored, out date);
    }

    /// <summary>
    /// Texto das tentativas de um nivel, da mais recente para a mais antiga.
    /// A mais recente tem o numero mais alto (antes a mais recente aparecia como "Tentativa 1").
    /// </summary>
    public static string BuildAttemptsText(PlayerData data, string levelName, Localizer L = null)
    {
        if (L == null) L = NoTranslation;
        if (data == null || data.sessionHistory == null) return "";

        string lblAttempt   = L("attempt_num", "TENTATIVA");
        string lblStars     = L("stars_label", "Estrelas");
        string lblComplete  = L("complete_label", "Completo!");
        string lblTime      = L("time_label", "Tempo");
        string lblColisoes  = L("colisoes", "Colisões");
        string lblDeslizes  = L("deslizes", "Deslizes");
        string lblFailed    = L("history_failed", "Falhou");
        string lblAt        = L("history_at", "às");

        List<SessionRecord> records = new List<SessionRecord>();
        foreach (SessionRecord r in data.sessionHistory)
        {
            if (SameLevel(r.levelName, levelName)) records.Add(r);
        }

        StringBuilder sb = new StringBuilder();
        for (int i = records.Count - 1; i >= 0; i--)
        {
            SessionRecord r = records[i];
            int number = i + 1;

            sb.Append($"<color=#A0E4FF><b>{lblAttempt} {number}</b></color>   <color=#AAAAAA>•   {FormatDate(r.sessionDate, lblAt)}</color>");
            string details = BuildDetailsLine(r, L);
            if (details.Length > 0) sb.Append($"   <color=#888888><size=80%>{details}</size></color>");
            sb.Append('\n');

            if (IsFreestyle(r.levelName))
            {
                sb.Append($"{lblStars}: <color=#FCD34D><b>{r.starsCollected} / {r.starsTotal}</b></color>");
                if (r.starsTotal > 0 && r.starsCollected >= r.starsTotal)
                    sb.Append($"   <color=#16A34A><b>✓ {lblComplete}</b></color>");
                sb.Append('\n');
            }
            else
            {
                if (r.failed)
                {
                    string reason = string.IsNullOrEmpty(r.failReason) ? "" : ": " + L(r.failReason, r.failReason);
                    sb.Append($"<color=#EF4444><b>✗ {lblFailed}{reason}</b></color>\n");
                }
                else if (r.starsEarned > 0)
                {
                    sb.Append($"<color=#FCD34D>{new string('★', r.starsEarned)}{new string('☆', 3 - Math.Min(3, r.starsEarned))}</color>\n");
                }

                sb.Append($"{lblTime}: <b>{r.completionTime.ToString("F1", CultureInfo.InvariantCulture)}s</b>   |   {lblColisoes}: <color=#EF4444><b>{r.totalCollisions}</b></color>   |   {lblDeslizes}: <color=#FCD34D><b>{r.totalSlides}</b></color>\n");
            }

            sb.Append("<color=#333333>──────────────────────────────────────────</color>\n\n");
        }

        return sb.ToString();
    }

    /// <summary>"VR · Frontal · Comando" (vazio para registos antigos sem esta informacao).</summary>
    public static string BuildDetailsLine(SessionRecord r, Localizer L = null)
    {
        if (L == null) L = NoTranslation;
        List<string> parts = new List<string>();
        if (!string.IsNullOrEmpty(r.platform)) parts.Add(r.platform);
        if (!string.IsNullOrEmpty(r.steeringType)) parts.Add(SteeringLabel(r.steeringType, L));
        if (!string.IsNullOrEmpty(r.inputMode)) parts.Add(InputLabel(r.inputMode, L));
        return string.Join(" · ", parts);
    }

    public static string SteeringLabel(string steeringType, Localizer L = null)
    {
        if (L == null) L = NoTranslation;
        if (steeringType == "RearSteering") return L("traseira", "Traseira");
        if (steeringType == "FrontSteering") return L("frontal", "Frontal");
        return steeringType ?? "";
    }

    public static string InputLabel(string inputMode, Localizer L = null)
    {
        if (L == null) L = NoTranslation;
        switch (inputMode)
        {
            case "Teclado": return L("hud_keyboard", "Teclado");
            case "RatoRock": return L("hud_mouse", "Rato");
            case "Comando": return L("hud_joystick", "Comando");
            case "Maos": return L("input_hands", "Mãos");
            case "Comando+Maos": return L("hud_joystick", "Comando") + "+" + L("input_hands", "Mãos");
            default: return inputMode ?? "";
        }
    }
}
