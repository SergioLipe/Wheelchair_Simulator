using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Exporta o historico clinico para CSV, para os terapeutas abrirem no Excel.
/// - Separador ";" e UTF-8 com BOM (o Excel em portugues abre diretamente, com acentos corretos).
/// - Sem casas decimais nos numeros (evita a confusao virgula/ponto entre idiomas).
/// - Ficheiros em [persistentDataPath]/Exportacoes. No Quest: Android/data/[pacote]/files/Exportacoes
///   (acessivel por USB a partir do PC).
/// </summary>
public static class ClinicalExport
{
    public const char Separator = ';';
    public const string FolderName = "Exportacoes";

    private static readonly string[] Header =
    {
        "Paciente", "Data", "Nivel", "Resultado", "Motivo da falha",
        "Tempo (s)", "Tempo (mm:ss)", "Colisoes", "Deslizes", "Estrelas",
        "Estrelas recolhidas", "Estrelas totais", "Plataforma", "Direcao", "Controlo"
    };

    public static string ExportFolder => Path.Combine(
        string.IsNullOrEmpty(SaveManager.DataDirectoryOverride) ? Application.persistentDataPath : SaveManager.DataDirectoryOverride,
        FolderName);

    /// <summary>Exporta todos os perfis para um unico CSV. Devolve o caminho, ou null em caso de erro.</summary>
    public static string ExportAllProfiles()
    {
        List<PlayerData> profiles = new List<PlayerData>();
        foreach (string id in SaveManager.GetAllProfileIDs())
        {
            PlayerData p = SaveManager.LoadProfile(id);
            if (p != null) profiles.Add(p);
        }
        return WriteCsv(profiles, "historico_todos");
    }

    /// <summary>Exporta um perfil. Devolve o caminho, ou null em caso de erro.</summary>
    public static string ExportProfile(PlayerData profile)
    {
        if (profile == null) return null;
        return WriteCsv(new[] { profile }, "historico_" + SaveManager.SanitizeProfileID(profile.profileID));
    }

    private static string WriteCsv(IEnumerable<PlayerData> profiles, string baseName)
    {
        try
        {
            Directory.CreateDirectory(ExportFolder);
            string path = Path.Combine(ExportFolder, $"{baseName}_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            File.WriteAllText(path, BuildCsv(profiles), new UTF8Encoding(true));
            Debug.Log("Histórico exportado para: " + path);
            return path;
        }
        catch (Exception e)
        {
            Debug.LogError("Erro ao exportar histórico: " + e.Message);
            return null;
        }
    }

    public static string BuildCsv(IEnumerable<PlayerData> profiles)
    {
        StringBuilder sb = new StringBuilder();
        AppendRow(sb, Header);

        foreach (PlayerData p in profiles)
        {
            if (p == null || p.sessionHistory == null) continue;
            foreach (SessionRecord r in p.sessionHistory)
            {
                AppendRow(sb, BuildRow(p.profileID, r));
            }
        }
        return sb.ToString();
    }

    public static string[] BuildRow(string profileID, SessionRecord r)
    {
        bool freestyle = SessionHistoryFormatter.IsFreestyle(r.levelName);
        string result = freestyle ? "Modo livre" : (r.failed ? "Falhou" : "Concluido");

        string date = SessionHistoryFormatter.TryParseDate(r.sessionDate, out DateTime d)
            ? d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : r.sessionDate ?? "";

        int seconds = Mathf.RoundToInt(r.completionTime);
        bool hasTime = !freestyle;

        return new[]
        {
            profileID ?? "",
            date,
            freestyle ? "Modo livre" : (r.levelName ?? ""),
            result,
            r.failed ? (r.failReason ?? "") : "",
            hasTime ? seconds.ToString(CultureInfo.InvariantCulture) : "",
            hasTime ? $"{seconds / 60:00}:{seconds % 60:00}" : "",
            freestyle ? "" : r.totalCollisions.ToString(CultureInfo.InvariantCulture),
            freestyle ? "" : r.totalSlides.ToString(CultureInfo.InvariantCulture),
            (!freestyle && !r.failed && r.starsEarned > 0) ? r.starsEarned.ToString(CultureInfo.InvariantCulture) : "",
            freestyle ? r.starsCollected.ToString(CultureInfo.InvariantCulture) : "",
            freestyle ? r.starsTotal.ToString(CultureInfo.InvariantCulture) : "",
            r.platform ?? "",
            SessionHistoryFormatter.SteeringLabel(r.steeringType),
            SessionHistoryFormatter.InputLabel(r.inputMode)
        };
    }

    private static void AppendRow(StringBuilder sb, string[] fields)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0) sb.Append(Separator);
            sb.Append(Escape(fields[i]));
        }
        sb.Append("\r\n");
    }

    public static string Escape(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        bool needsQuotes = field.IndexOf(Separator) >= 0 || field.IndexOf('"') >= 0
                           || field.IndexOf('\n') >= 0 || field.IndexOf('\r') >= 0;
        if (!needsQuotes) return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
