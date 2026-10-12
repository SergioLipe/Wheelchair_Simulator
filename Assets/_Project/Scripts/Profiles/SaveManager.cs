using UnityEngine;
using System.IO;

public static class SaveManager
{
    private const string BackupSuffix = ".bak";
    private const string TempSuffix = ".tmp";

    /// <summary>
    /// Limpa um ID escrito pelo utilizador para poder ser usado como nome de ficheiro.
    /// Caracteres invalidos (/ \ : * ? " < > | ...) passam a '_'. Devolve "" se nao sobrar nada.
    /// </summary>
    public static string SanitizeProfileID(string rawID)
    {
        if (string.IsNullOrEmpty(rawID)) return "";

        char[] invalid = Path.GetInvalidFileNameChars();
        System.Text.StringBuilder sb = new System.Text.StringBuilder(rawID.Length);
        foreach (char c in rawID.Trim())
        {
            bool bad = c == '/' || c == '\\' || char.IsControl(c) || System.Array.IndexOf(invalid, c) >= 0;
            sb.Append(bad ? '_' : c);
        }

        // Windows nao aceita nomes a terminar em ponto ou espaco
        return sb.ToString().Trim().TrimEnd('.');
    }

    private static string GetProfilePath(string profileID)
    {
        return Path.Combine(Application.persistentDataPath, profileID + "_profile.json");
    }

    // Saves the player data to a JSON file.
    // Escreve primeiro num ficheiro temporario e so depois substitui o original,
    // para que um crash a meio da escrita nao corrompa o historico do paciente.
    // Devolve false em caso de erro (nunca lanca excecao).
    public static bool SaveProfile(PlayerData data)
    {
        if (data == null || string.IsNullOrEmpty(data.profileID)) return false;

        string filePath = GetProfilePath(data.profileID);
        string tempPath = filePath + TempSuffix;

        try
        {
            string jsonText = JsonUtility.ToJson(data, true);
            File.WriteAllText(tempPath, jsonText);

            if (File.Exists(filePath))
            {
                try
                {
                    File.Replace(tempPath, filePath, filePath + BackupSuffix);
                }
                catch (System.Exception)
                {
                    // Algumas plataformas nao suportam File.Replace: fallback copia + delete
                    File.Copy(filePath, filePath + BackupSuffix, true);
                    File.Copy(tempPath, filePath, true);
                    File.Delete(tempPath);
                }
            }
            else
            {
                File.Move(tempPath, filePath);
            }

            Debug.Log("Perfil guardado com sucesso no caminho: " + filePath);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Erro ao guardar perfil '" + data.profileID + "': " + e.Message);
            return false;
        }
    }

    // Loads the player data from a JSON file.
    // Se o ficheiro estiver corrompido, tenta a copia de seguranca (.bak).
    public static PlayerData LoadProfile(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return null;

        string filePath = GetProfilePath(profileID);

        PlayerData loadedData = TryReadProfile(filePath);
        if (loadedData == null && File.Exists(filePath + BackupSuffix))
        {
            Debug.LogWarning("Atenção: Perfil '" + profileID + "' ilegível. A usar a cópia de segurança.");
            loadedData = TryReadProfile(filePath + BackupSuffix);
        }

        if (loadedData == null)
        {
            Debug.LogWarning("Atenção: Ficheiro de perfil não encontrado em: " + filePath);
            return null;
        }

        // Perfis gravados por versoes antigas podem nao ter estes campos
        if (string.IsNullOrEmpty(loadedData.profileID)) loadedData.profileID = profileID;
        if (loadedData.sessionHistory == null) loadedData.sessionHistory = new System.Collections.Generic.List<SessionRecord>();
        if (loadedData.inputSettings == null) loadedData.inputSettings = new InputSettings();
        if (loadedData.levelProgress == null) loadedData.levelProgress = new System.Collections.Generic.List<LevelProgress>();

        Debug.Log("Perfil carregado com sucesso!");
        return loadedData;
    }

    private static PlayerData TryReadProfile(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            string jsonText = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(jsonText)) return null;
            return JsonUtility.FromJson<PlayerData>(jsonText);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Erro ao ler perfil em '" + path + "': " + e.Message);
            return null;
        }
    }

    /// <summary>
    /// True se ja existe um perfil com este ID (incluindo so a copia de seguranca).
    /// </summary>
    public static bool ProfileExists(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return false;
        string filePath = GetProfilePath(profileID);
        return File.Exists(filePath) || File.Exists(filePath + BackupSuffix);
    }

    // Searches the folder and returns a list of all existing profile IDs
    public static string[] GetAllProfileIDs()
    {
        // Get all files ending with "_profile.json" in the folder
        string[] files = Directory.GetFiles(Application.persistentDataPath, "*_profile.json");
        string[] profileIDs = new string[files.Length];
        for (int i = 0; i < files.Length; i++)
        {
            // Extract just the file name and remove the "_profile.json" extension to get the clean ID
            string fileName = Path.GetFileName(files[i]);
            profileIDs[i] = fileName.Replace("_profile.json", "");
        }

        return profileIDs;
    }

    // ==========================================
    // NEW: DELETE PROFILE
    // ==========================================

    /// <summary>
    /// Deletes a profile file (and its backup) from disk by its ID.
    /// Returns true if deleted successfully, false if file didn't exist or deletion failed.
    /// </summary>
    public static bool DeleteProfile(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return false;

        string filePath = GetProfilePath(profileID);

        try
        {
            bool existed = File.Exists(filePath);
            if (existed) File.Delete(filePath);

            // Sem isto, LoadProfile iria "ressuscitar" o perfil a partir do .bak
            if (File.Exists(filePath + BackupSuffix)) File.Delete(filePath + BackupSuffix);
            if (File.Exists(filePath + TempSuffix)) File.Delete(filePath + TempSuffix);

            if (existed)
            {
                Debug.Log("Perfil apagado com sucesso: " + filePath);
                return true;
            }

            Debug.LogWarning("Atenção: Ficheiro de perfil não encontrado para apagar: " + filePath);
            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Erro ao apagar perfil '" + profileID + "': " + e.Message);
            return false;
        }
    }

    // ==========================================
    // NEW: RENAME PROFILE
    // ==========================================

    /// <summary>
    /// Renames a profile by copying its data to a new ID and deleting the old one.
    /// Returns true if successful, false if the old profile doesn't exist or the new ID is already taken.
    /// O novo ID e limpo com SanitizeProfileID.
    /// </summary>
    public static bool RenameProfile(string oldID, string newID)
    {
        newID = SanitizeProfileID(newID);
        if (string.IsNullOrEmpty(oldID) || string.IsNullOrEmpty(newID)) return false;
        if (oldID == newID) return true; // nothing to do

        // Em Windows os nomes de ficheiro nao distinguem maiusculas: "ana" -> "Ana" apontaria
        // para o mesmo ficheiro e o DeleteProfile final apagaria o perfil.
        if (string.Equals(oldID, newID, System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("Atenção: Não é possível renomear só a mudar maiúsculas/minúsculas ('" + oldID + "' -> '" + newID + "').");
            return false;
        }

        // Check if newID already exists - we don't want to overwrite another profile
        if (ProfileExists(newID))
        {
            Debug.LogWarning("Atenção: Já existe um perfil com o ID '" + newID + "'. Não é possível renomear.");
            return false;
        }

        // Load old profile
        PlayerData data = LoadProfile(oldID);
        if (data == null)
        {
            Debug.LogWarning("Atenção: Perfil '" + oldID + "' não encontrado para renomear.");
            return false;
        }

        // Update the ID inside the data
        data.profileID = newID;

        // Save under new name; so apaga o antigo se a gravacao correu bem
        if (!SaveProfile(data))
        {
            Debug.LogError("Erro: Não foi possível gravar o perfil renomeado. O perfil '" + oldID + "' foi mantido.");
            return false;
        }

        DeleteProfile(oldID);

        // Se o perfil renomeado estava ativo, atualiza a sessao para nao voltar a gravar com o ID antigo
        if (ProfileManager.Instance != null && ProfileManager.Instance.currentPlayer != null
            && ProfileManager.Instance.currentPlayer.profileID == oldID)
        {
            ProfileManager.Instance.SetActiveProfile(data);
        }

        Debug.Log("Perfil renomeado de '" + oldID + "' para '" + newID + "'.");
        return true;
    }
}
