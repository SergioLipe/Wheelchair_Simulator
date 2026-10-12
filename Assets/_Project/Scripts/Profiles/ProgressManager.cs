using UnityEngine;

/// <summary>
/// Progresso dos niveis (estrelas e desbloqueio) guardado no perfil de cada paciente.
/// Antes o progresso estava em PlayerPrefs e era partilhado por todos os perfis do dispositivo.
/// Sem perfil ativo, usa as chaves antigas de PlayerPrefs.
/// </summary>
public static class ProgressManager
{
    private const string UnlockAllKey = "UnlockAll";

    private static string LegacyKey(int levelID) => "Level_" + levelID + "_Stars";

    private static PlayerData CurrentPlayer =>
        ProfileManager.Instance != null ? ProfileManager.Instance.currentPlayer : null;

    public static int GetBestStars(int levelID)
    {
        PlayerData p = CurrentPlayer;
        if (p == null) return PlayerPrefs.GetInt(LegacyKey(levelID), 0);

        MigrateLegacyProgress(p);
        LevelProgress entry = Find(p, levelID);
        return entry != null ? entry.bestStars : 0;
    }

    /// <summary>
    /// Regista o resultado se for melhor que o anterior. Nao grava em disco:
    /// quem chama grava o perfil (junto com o registo da sessao).
    /// </summary>
    public static void RecordStars(int levelID, int stars)
    {
        PlayerData p = CurrentPlayer;
        if (p == null)
        {
            if (stars > PlayerPrefs.GetInt(LegacyKey(levelID), 0))
            {
                PlayerPrefs.SetInt(LegacyKey(levelID), stars);
                PlayerPrefs.Save();
            }
            return;
        }

        MigrateLegacyProgress(p);
        LevelProgress entry = Find(p, levelID);
        if (entry == null)
        {
            entry = new LevelProgress { levelID = levelID, bestStars = 0 };
            p.levelProgress.Add(entry);
        }
        if (stars > entry.bestStars) entry.bestStars = stars;
    }

    public static bool IsUnlockAll() => PlayerPrefs.GetInt(UnlockAllKey, 0) == 1;

    public static void SetUnlockAll()
    {
        PlayerPrefs.SetInt(UnlockAllKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Apaga o progresso do perfil ativo (ou o global, sem perfil) e o "desbloquear tudo".
    /// Ao contrario do antigo PlayerPrefs.DeleteAll, mantem o idioma escolhido.
    /// </summary>
    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(UnlockAllKey);

        PlayerData p = CurrentPlayer;
        if (p == null)
        {
            for (int i = 1; i <= 50; i++) PlayerPrefs.DeleteKey(LegacyKey(i));
            PlayerPrefs.Save();
            return;
        }

        p.levelProgress.Clear();
        p.legacyProgressMigrated = true;
        PlayerPrefs.Save();
        SaveManager.SaveProfile(p);
    }

    /// <summary>
    /// Perfis criados antes desta versao recebem uma copia do progresso global antigo,
    /// para ninguem perder niveis desbloqueados com a atualizacao.
    /// </summary>
    private static void MigrateLegacyProgress(PlayerData p)
    {
        if (p.legacyProgressMigrated) return;
        if (p.levelProgress == null) p.levelProgress = new System.Collections.Generic.List<LevelProgress>();

        for (int i = 1; i <= 50; i++)
        {
            int stars = PlayerPrefs.GetInt(LegacyKey(i), 0);
            if (stars > 0 && Find(p, i) == null)
            {
                p.levelProgress.Add(new LevelProgress { levelID = i, bestStars = stars });
            }
        }

        p.legacyProgressMigrated = true;
        SaveManager.SaveProfile(p);
    }

    private static LevelProgress Find(PlayerData p, int levelID)
    {
        if (p.levelProgress == null) p.levelProgress = new System.Collections.Generic.List<LevelProgress>();
        for (int i = 0; i < p.levelProgress.Count; i++)
        {
            if (p.levelProgress[i].levelID == levelID) return p.levelProgress[i];
        }
        return null;
    }
}
