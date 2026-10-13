using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Localization.Settings;

/// <summary>
/// Logica comum aos menus principais de PC e VR: lista de perfis (entrar / renomear / apagar),
/// historico de sessoes, exportacao CSV e botoes dos niveis.
/// Antes estava copiada nos dois scripts (~950 linhas cada) e as correcoes tinham de ser feitas duas vezes.
///
/// Os nomes dos campos sao os mesmos que ja existiam nos dois scripts, por isso as referencias
/// guardadas nas cenas continuam ligadas.
/// </summary>
public abstract class MainMenuManagerBase : MonoBehaviour
{
    [Header("--- Main UI Elements ---")]
    [Tooltip("Drag the main game title text here so it hides during history")]
    public GameObject mainTitle;

    [Header("--- Profile Selection Panel ---")]
    public GameObject profileSelectionPanel;
    public GameObject levelSelectionPanel;
    public TMP_Text txtCurrentProfile;

    [Header("--- Existing Profiles List ---")]
    [Tooltip("Drag the Content GameObject of the ProfilesList ScrollView (where rows will be cloned)")]
    public Transform profilesListContent;

    [Tooltip("Drag the BtnProfileTemplate GameObject. Must contain children: Btn_LoginProfile, InputEditName, Btn_EditProfile, Btn_DeleteProfile.")]
    public GameObject profileButtonTemplate;

    [Header("--- Confirm Delete Popup ---")]
    public GameObject confirmDeletePanel;
    public TMP_Text txtConfirmMessage;
    public Button btnConfirmYes;
    public Button btnConfirmNo;

    [Header("--- View History ---")]
    public Button btnViewHistory;

    [Tooltip("Panel shown when user clicks 'View history' to choose which profile to inspect")]
    public GameObject historyProfileSelectorPanel;

    [Tooltip("Where history profile buttons will be cloned")]
    public Transform historyProfilesListContent;

    [Tooltip("Template button for history profile selector")]
    public GameObject historyProfileButtonTemplate;

    [Header("--- History: Levels Panel ---")]
    public GameObject historyLevelsPanel;
    public Button btnCloseHistory;
    [Tooltip("Drag the history level buttons here in order (Level 1, Level 2...)")]
    public Button[] historyLevelButtons;
    public TMP_Text txtHistorySubtitle;
    public Button btnHistFreestyle;
    public TMP_Text txtHistFreestyleCount;

    [Header("--- History: Attempts Panel ---")]
    public GameObject historyAttemptsPanel;
    public TMP_Text txtHistoryDetails;
    public Button btnBackToHistLevels;

    [Header("--- History: Attempts Panel Header ---")]
    public TMP_Text txtPatientName;
    public TMP_Text txtLevelTitle;
    public TMP_Text txtAttemptsCount;

    [Header("--- Main Game Level Buttons ---")]
    public Button[] levelButtons;

    [Header("--- UI Colors ---")]
    public Color unlockedBGColor = new Color(0f, 0.78f, 0.32f, 1f);
    public Color freeStyleColor = new Color(0f, 0.6f, 1f, 1f);
    public Color lockedBGColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    public Color starEarnedColor = new Color(1f, 0.84f, 0f, 1f);
    public Color starEmptyColor = new Color(0f, 0f, 0f, 0.4f);

    [Header("--- History Button Colors ---")]
    [Tooltip("Color used for level buttons that have session records")]
    public Color historyHasRecordsColor = new Color(0.086f, 0.639f, 0.290f, 1f);
    [Tooltip("Color used for level buttons with no session records (faded/grayed out)")]
    public Color historyNoRecordsColor = new Color(0.32f, 0.32f, 0.32f, 0.5f);

    [Header("--- Exportar histórico (CSV) ---")]
    [Tooltip("Opcional: texto onde aparece o caminho do ficheiro exportado")]
    public TMP_Text txtExportStatus;

    [Header("--- Versao ---")]
    [Tooltip("Opcional: texto para o numero da versao. Se ficar vazio, e criado no canto inferior direito do menu.")]
    public TMP_Text txtVersion;

    protected readonly List<GameObject> spawnedProfileButtons = new List<GameObject>();
    protected readonly List<GameObject> spawnedHistoryProfileButtons = new List<GameObject>();
    protected string profileToDelete = null;

    // ==========================================
    // HOOKS (diferencas entre PC e VR)
    // ==========================================

    /// <summary>Prefixo dos avisos na consola, ex. "[MainMenuManager_VR]".</summary>
    protected abstract string LogTag { get; }

    /// <summary>Clique no nome de um perfil da lista.</summary>
    protected abstract void LoginFromProfileList(string profileID);

    /// <summary>Campo onde se escreve o nome de um perfil novo (limpo ao voltar ao ecra de perfis).</summary>
    protected abstract TMP_InputField NewProfileInputField { get; }

    /// <summary>Um perfil foi renomeado (newID != null) ou apagado (newID == null).</summary>
    protected virtual void OnProfileRenamedOrDeleted(string oldID, string newID) { }

    /// <summary>A lista de perfis foi (re)construida.</summary>
    protected virtual void OnProfilesListPopulated(int profileCount) { }

    /// <summary>"Ver historico" quando nao ha painel de escolha de perfil.</summary>
    protected virtual void OpenHistoryWithoutSelector()
    {
        string[] ids = SaveManager.GetAllProfileIDs();
        if (ids != null && ids.Length > 0) OpenHistoryForProfile(ids[0]);
    }

    /// <summary>Mostrar "MODO LIVRE —" antes do numero de sessoes do modo livre.</summary>
    protected virtual bool ShowFreestyleModePrefix => true;

    // ==========================================
    // LOCALIZACAO
    // ==========================================

    protected const string TABLE = "GameText";

    protected string L(string key, string fallback)
    {
        try
        {
            string v = LocalizationSettings.StringDatabase.GetLocalizedString(TABLE, key);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }
        catch { return fallback; }
    }

    // ==========================================
    // ARRANQUE COMUM
    // ==========================================

    protected bool HasActiveProfile =>
        ProfileManager.Instance != null
        && ProfileManager.Instance.currentPlayer != null
        && !string.IsNullOrEmpty(ProfileManager.Instance.currentPlayer.profileID);

    /// <summary>
    /// Paineis iniciais, lista de perfis, botoes do historico e dos niveis.
    /// Se ja ha um perfil ativo (voltou de um nivel), salta o login e mostra os niveis.
    /// </summary>
    protected void SetupCommonMenu(Button btnCloseHistoryProfileSelector)
    {
        if (HasActiveProfile)
        {
            if (profileSelectionPanel != null) profileSelectionPanel.SetActive(false);
            if (levelSelectionPanel != null) levelSelectionPanel.SetActive(true);
            if (txtCurrentProfile != null) txtCurrentProfile.text = ProfileManager.Instance.currentPlayer.profileID;
        }
        else
        {
            if (profileSelectionPanel != null) profileSelectionPanel.SetActive(true);
            if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        }

        if (historyLevelsPanel != null) historyLevelsPanel.SetActive(false);
        if (historyAttemptsPanel != null) historyAttemptsPanel.SetActive(false);
        if (historyProfileSelectorPanel != null) historyProfileSelectorPanel.SetActive(false);
        if (confirmDeletePanel != null) confirmDeletePanel.SetActive(false);

        if (profileButtonTemplate != null) profileButtonTemplate.SetActive(false);
        if (historyProfileButtonTemplate != null) historyProfileButtonTemplate.SetActive(false);
        PopulateProfilesList();

        if (btnViewHistory != null) btnViewHistory.onClick.AddListener(OpenHistoryProfileSelector);
        if (btnCloseHistoryProfileSelector != null) btnCloseHistoryProfileSelector.onClick.AddListener(CloseHistoryProfileSelector);
        if (btnCloseHistory != null) btnCloseHistory.onClick.AddListener(CloseHistory);
        if (btnBackToHistLevels != null) btnBackToHistLevels.onClick.AddListener(BackToHistoryLevels);

        if (btnConfirmYes != null) btnConfirmYes.onClick.AddListener(OnConfirmDeleteYes);
        if (btnConfirmNo != null) btnConfirmNo.onClick.AddListener(OnConfirmDeleteNo);

        InitializeAllLevels();
        ShowVersion();
    }

    // ==========================================
    // VERSAO
    // ==========================================

    /// <summary>
    /// Mostra "v" + a versao da app (Project Settings > Player > Version), para se saber
    /// que build esta instalada (ex.: no Quest).
    /// </summary>
    protected void ShowVersion()
    {
        if (txtVersion == null) txtVersion = CreateVersionLabel();
        if (txtVersion != null) txtVersion.text = "v" + Application.version;
    }

    private TMP_Text CreateVersionLabel()
    {
        GameObject anchor = profileSelectionPanel != null ? profileSelectionPanel : levelSelectionPanel;
        Canvas canvas = anchor != null ? anchor.GetComponentInParent<Canvas>(true) : null;
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;

        GameObject go = new GameObject("VersionLabel", typeof(RectTransform));
        go.layer = canvas.gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.BottomRight;
        label.raycastTarget = false;
        label.color = new Color(1f, 1f, 1f, 0.6f);

        // Usar o mesmo tipo de letra e uma escala proporcional a um texto do menu (funciona no canvas de PC e no de VR)
        float fontSize = 18f;
        if (txtCurrentProfile != null)
        {
            label.font = txtCurrentProfile.font;
            fontSize = txtCurrentProfile.fontSize * 0.6f;
        }
        label.fontSize = fontSize;
        rt.sizeDelta = new Vector2(fontSize * 8f, fontSize * 1.5f);
        rt.anchoredPosition = new Vector2(-fontSize * 0.5f, fontSize * 0.5f);
        rt.SetAsLastSibling();
        return label;
    }

    // ==========================================
    // LOGIN
    // ==========================================

    /// <summary>
    /// Entra com o perfil (cria-o se nao existir) e mostra a selecao de niveis.
    /// </summary>
    protected void ActivateProfile(string profileID)
    {
        PlayerData loadedData = SaveManager.LoadProfile(profileID);
        if (loadedData == null)
        {
            loadedData = PlayerData.CreateNew(profileID);
            SaveManager.SaveProfile(loadedData);
        }
        ProfileManager.Instance.SetActiveProfile(loadedData);

        if (txtCurrentProfile != null) txtCurrentProfile.text = loadedData.profileID;

        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(false);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(true);

        // Estrelas e desbloqueios sao por perfil: atualizar os botoes para este paciente
        InitializeAllLevels();
    }

    public void BackToProfileSelection()
    {
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(true);

        PopulateProfilesList();

        if (NewProfileInputField != null) NewProfileInputField.text = "";
    }

    // ==========================================
    // PROFILE LIST WITH LOGIN / EDIT / DELETE
    // ==========================================

    protected void PopulateProfilesList()
    {
        foreach (GameObject oldBtn in spawnedProfileButtons)
        {
            if (oldBtn != null) Destroy(oldBtn);
        }
        spawnedProfileButtons.Clear();

        if (profileButtonTemplate == null || profilesListContent == null)
        {
            Debug.LogWarning($"{LogTag} Profile template or content reference missing.");
            return;
        }

        if (profileButtonTemplate.activeSelf)
            profileButtonTemplate.SetActive(false);

        string[] savedIDs = SaveManager.GetAllProfileIDs();
        OnProfilesListPopulated(savedIDs != null ? savedIDs.Length : 0);

        if (savedIDs == null || savedIDs.Length == 0) return;

        foreach (string profileID in savedIDs)
        {
            GameObject newRow = Instantiate(profileButtonTemplate, profilesListContent);
            newRow.SetActive(true);
            newRow.name = "Row_Profile_" + profileID;

            string capturedID = profileID;

            Transform loginBtnT = newRow.transform.Find("Btn_LoginProfile");
            Transform editBtnT = newRow.transform.Find("Btn_EditProfile");
            Transform deleteBtnT = newRow.transform.Find("Btn_DeleteProfile");
            Transform inputEditT = newRow.transform.Find("InputEditName");

            Button loginBtn = null;
            TMP_Text loginText = null;
            if (loginBtnT != null)
            {
                loginBtn = loginBtnT.GetComponent<Button>();
                loginText = loginBtnT.GetComponentInChildren<TMP_Text>();

                if (loginText != null) loginText.text = profileID;

                if (loginBtn != null)
                {
                    loginBtn.onClick.RemoveAllListeners();
                    loginBtn.onClick.AddListener(() => LoginFromProfileList(capturedID));
                }
            }
            else
            {
                Debug.LogWarning($"{LogTag} Btn_LoginProfile not found in row '{newRow.name}'.");
            }

            TMP_InputField editInput = null;
            if (inputEditT != null)
            {
                editInput = inputEditT.GetComponent<TMP_InputField>();
                inputEditT.gameObject.SetActive(false);
            }

            if (editBtnT != null)
            {
                Button editBtn = editBtnT.GetComponent<Button>();
                if (editBtn != null && editInput != null && loginBtn != null)
                {
                    Button capturedLogin = loginBtn;
                    TMP_InputField capturedInput = editInput;
                    TMP_Text capturedLoginText = loginText;

                    editBtn.onClick.RemoveAllListeners();
                    editBtn.onClick.AddListener(() => StartRenameProfile(capturedID, capturedLogin, capturedInput, capturedLoginText));
                }
            }

            if (deleteBtnT != null)
            {
                Button deleteBtn = deleteBtnT.GetComponent<Button>();
                if (deleteBtn != null)
                {
                    deleteBtn.onClick.RemoveAllListeners();
                    deleteBtn.onClick.AddListener(() => RequestDeleteProfile(capturedID));
                }
            }

            spawnedProfileButtons.Add(newRow);
        }
    }

    // ==========================================
    // EDIT (RENAME) PROFILE
    // ==========================================

    protected void StartRenameProfile(string oldID, Button loginBtn, TMP_InputField editInput, TMP_Text loginText)
    {
        if (loginBtn == null || editInput == null) return;

        loginBtn.gameObject.SetActive(false);
        editInput.gameObject.SetActive(true);
        editInput.text = oldID;

        SetRowButtonsVisible(loginBtn, false);

        editInput.Select();
        editInput.ActivateInputField();

        editInput.onEndEdit.RemoveAllListeners();
        editInput.onEndEdit.AddListener((newName) =>
        {
            FinishRenameProfile(oldID, newName, loginBtn, editInput, loginText);
        });
    }

    protected void FinishRenameProfile(string oldID, string newName, Button loginBtn, TMP_InputField editInput, TMP_Text loginText)
    {
        if (editInput != null) editInput.gameObject.SetActive(false);
        if (loginBtn != null)
        {
            loginBtn.gameObject.SetActive(true);
            SetRowButtonsVisible(loginBtn, true);
        }

        string trimmed = newName != null ? newName.Trim() : "";
        if (string.IsNullOrEmpty(trimmed) || trimmed == oldID) return;

        if (SaveManager.RenameProfile(oldID, trimmed))
        {
            OnProfileRenamedOrDeleted(oldID, SaveManager.SanitizeProfileID(trimmed));
            PopulateProfilesList();
        }
        else
        {
            if (loginText != null) loginText.text = oldID;
            Debug.LogWarning($"{LogTag} Failed to rename '{oldID}' to '{trimmed}'.");
        }
    }

    private static void SetRowButtonsVisible(Button loginBtn, bool visible)
    {
        Transform row = loginBtn.transform.parent;
        if (row == null) return;
        Transform editBtnT = row.Find("Btn_EditProfile");
        Transform deleteBtnT = row.Find("Btn_DeleteProfile");
        if (editBtnT != null) editBtnT.gameObject.SetActive(visible);
        if (deleteBtnT != null) deleteBtnT.gameObject.SetActive(visible);
    }

    // ==========================================
    // DELETE PROFILE WITH CONFIRMATION
    // ==========================================

    public void RequestDeleteProfile(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return;

        profileToDelete = profileID;

        if (confirmDeletePanel != null)
        {
            if (txtConfirmMessage != null)
            {
                // key: confirm_delete_profile  (usa {0} para o nome do perfil)
                string template = L("confirm_delete_profile",
                    "Tem a certeza que quer apagar o perfil <b><color=#F87171>{0}</color></b>?\n\nEsta ação não pode ser desfeita.");
                txtConfirmMessage.text = string.Format(template, profileID);
            }
            confirmDeletePanel.SetActive(true);
        }
        else
        {
            DeleteProfile(profileID);
            profileToDelete = null;
        }
    }

    public void OnConfirmDeleteYes()
    {
        if (!string.IsNullOrEmpty(profileToDelete))
        {
            DeleteProfile(profileToDelete);
            profileToDelete = null;
        }
        if (confirmDeletePanel != null) confirmDeletePanel.SetActive(false);
    }

    public void OnConfirmDeleteNo()
    {
        profileToDelete = null;
        if (confirmDeletePanel != null) confirmDeletePanel.SetActive(false);
    }

    protected void DeleteProfile(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return;

        if (SaveManager.DeleteProfile(profileID))
        {
            OnProfileRenamedOrDeleted(profileID, null);
            PopulateProfilesList();
        }
        else
        {
            Debug.LogWarning($"{LogTag} Failed to delete profile '{profileID}'.");
        }
    }

    // ==========================================
    // HISTORY: PROFILE SELECTOR
    // ==========================================

    public void OpenHistoryProfileSelector()
    {
        if (historyProfileSelectorPanel == null)
        {
            OpenHistoryWithoutSelector();
            return;
        }

        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(false);
        historyProfileSelectorPanel.SetActive(true);

        if (mainTitle != null) mainTitle.SetActive(false);

        PopulateHistoryProfilesList();
    }

    protected void PopulateHistoryProfilesList()
    {
        foreach (GameObject oldBtn in spawnedHistoryProfileButtons)
        {
            if (oldBtn != null) Destroy(oldBtn);
        }
        spawnedHistoryProfileButtons.Clear();

        if (historyProfileButtonTemplate == null || historyProfilesListContent == null) return;

        if (historyProfileButtonTemplate.activeSelf)
            historyProfileButtonTemplate.SetActive(false);

        string[] savedIDs = SaveManager.GetAllProfileIDs();
        if (savedIDs == null || savedIDs.Length == 0) return;

        foreach (string profileID in savedIDs)
        {
            GameObject newRow = Instantiate(historyProfileButtonTemplate, historyProfilesListContent);
            newRow.SetActive(true);
            newRow.name = "Row_HistProfile_" + profileID;

            string capturedID = profileID;

            Transform selectBtnT = newRow.transform.Find("Btn_SelectProfile");
            Button selectBtn;
            TMP_Text selectText;

            if (selectBtnT != null)
            {
                selectBtn = selectBtnT.GetComponent<Button>();
                selectText = selectBtnT.GetComponentInChildren<TMP_Text>();
            }
            else
            {
                selectBtn = newRow.GetComponent<Button>();
                selectText = newRow.GetComponentInChildren<TMP_Text>();
            }

            if (selectText != null) selectText.text = profileID;

            if (selectBtn != null)
            {
                selectBtn.onClick.RemoveAllListeners();
                selectBtn.onClick.AddListener(() => OpenHistoryForProfile(capturedID));
            }

            spawnedHistoryProfileButtons.Add(newRow);
        }
    }

    public void CloseHistoryProfileSelector()
    {
        if (historyProfileSelectorPanel != null) historyProfileSelectorPanel.SetActive(false);
        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(true);
        if (mainTitle != null) mainTitle.SetActive(true);
    }

    // ==========================================
    // HISTORY SYSTEM
    // ==========================================

    public void OpenHistoryForProfile(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return;

        PlayerData data = SaveManager.LoadProfile(profileID);
        if (data == null) return;

        if (historyProfileSelectorPanel != null) historyProfileSelectorPanel.SetActive(false);
        OpenHistoryLevelsForData(data);
    }

    // Nome antigo usado pelo menu VR
    public void OpenHistoryLevelsForProfile(string profileID) => OpenHistoryForProfile(profileID);

    protected void OpenHistoryLevelsForData(PlayerData data)
    {
        if (data == null) return;

        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(false);
        if (historyLevelsPanel != null) historyLevelsPanel.SetActive(true);
        if (mainTitle != null) mainTitle.SetActive(false);

        if (txtHistorySubtitle != null)
        {
            // key: history_choose_level  (usa {0} para o nome do perfil)
            string template = L("history_choose_level",
                "Escolhe um nível para ver as tentativas de <color=#FCD34D>{0}</color>");
            txtHistorySubtitle.text = string.Format(template, data.profileID);
        }

        if (historyLevelButtons != null)
        {
            for (int i = 0; i < historyLevelButtons.Length; i++)
            {
                if (historyLevelButtons[i] == null) continue;
                ConfigureHistoryLevelButton(historyLevelButtons[i], data, "Level" + (i + 1));
            }
        }

        ConfigureFreestyleHistoryButton(data);
    }

    private void ConfigureHistoryLevelButton(Button btn, PlayerData data, string levelName)
    {
        int attemptsCount = SessionHistoryFormatter.CountAttempts(data, levelName);
        bool hasPlayedThisLevel = attemptsCount > 0;

        btn.interactable = hasPlayedThisLevel;

        Image bgImage = btn.GetComponent<Image>();
        if (bgImage != null)
        {
            bgImage.color = hasPlayedThisLevel ? historyHasRecordsColor : historyNoRecordsColor;
        }

        Transform attemptsTransform = btn.transform.Find("AttemptsText");
        TMP_Text attemptsText = attemptsTransform != null ? attemptsTransform.GetComponent<TMP_Text>() : null;
        if (attemptsText != null)
        {
            if (hasPlayedThisLevel)
            {
                string label = attemptsCount == 1 ? L("attempt_sing", "Tentativa") : L("attempt_plur", "Tentativas");
                attemptsText.text = $"{attemptsCount} {label}";
                attemptsText.color = new Color(1f, 1f, 1f, 0.85f);
            }
            else
            {
                attemptsText.text = L("no_records", "sem registos");
                attemptsText.color = new Color(1f, 1f, 1f, 0.4f);
            }
        }

        Transform numberTransform = btn.transform.Find("Number");
        TMP_Text numberText = numberTransform != null ? numberTransform.GetComponent<TMP_Text>() : null;
        if (numberText != null)
        {
            numberText.color = hasPlayedThisLevel ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.4f);
        }

        btn.onClick.RemoveAllListeners();
        if (hasPlayedThisLevel)
        {
            btn.onClick.AddListener(() => ShowAttemptsForLevel(data, levelName));
        }
    }

    protected void ConfigureFreestyleHistoryButton(PlayerData data)
    {
        if (btnHistFreestyle == null) return;

        int freestyleCount = SessionHistoryFormatter.CountAttempts(data, "Freestyle");
        bool hasFreestyleRecords = freestyleCount > 0;
        btnHistFreestyle.interactable = hasFreestyleRecords;

        if (txtHistFreestyleCount != null)
        {
            string text = hasFreestyleRecords
                ? $"{freestyleCount} {(freestyleCount == 1 ? L("session_sing", "sessão registada") : L("session_plur", "sessões registadas"))}"
                : L("no_sessions", "sem sessões registadas");

            if (ShowFreestyleModePrefix) text = $"{L("free_mode_label", "MODO LIVRE")} — {text}";
            txtHistFreestyleCount.text = text;
        }

        btnHistFreestyle.onClick.RemoveAllListeners();
        if (hasFreestyleRecords)
        {
            btnHistFreestyle.onClick.AddListener(() => ShowAttemptsForLevel(data, "Freestyle"));
        }
    }

    public void ShowAttemptsForLevel(PlayerData data, string levelName)
    {
        if (historyLevelsPanel != null) historyLevelsPanel.SetActive(false);
        if (historyAttemptsPanel != null) historyAttemptsPanel.SetActive(true);

        int totalAttempts = SessionHistoryFormatter.CountAttempts(data, levelName);

        if (txtPatientName != null) txtPatientName.text = data.profileID;
        if (txtLevelTitle != null) txtLevelTitle.text = levelName;

        if (txtAttemptsCount != null)
        {
            string label = totalAttempts == 1
                ? L("attempt_rec_sing", "Tentativa registada")
                : L("attempt_rec_plur", "Tentativas registadas");
            txtAttemptsCount.text = $"{totalAttempts} {label}";
        }

        if (txtHistoryDetails != null)
            txtHistoryDetails.text = SessionHistoryFormatter.BuildAttemptsText(data, levelName, L);
    }

    public void CloseHistory()
    {
        if (historyLevelsPanel != null) historyLevelsPanel.SetActive(false);
        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(true);

        PopulateProfilesList();

        if (mainTitle != null) mainTitle.SetActive(true);
    }

    public void BackToHistoryLevels()
    {
        if (historyAttemptsPanel != null) historyAttemptsPanel.SetActive(false);
        if (historyLevelsPanel != null) historyLevelsPanel.SetActive(true);
    }

    // ==========================================
    // EXPORTAR HISTORICO (CSV)
    // ==========================================

    /// <summary>
    /// Exporta o historico de todos os pacientes para um CSV (abre no Excel).
    /// Ligar ao OnClick de um botao "Exportar histórico".
    /// </summary>
    public void Button_ExportHistoryCsv()
    {
        string path = ClinicalExport.ExportAllProfiles();

        if (txtExportStatus != null)
        {
            txtExportStatus.text = path != null
                ? string.Format(L("export_done", "Histórico exportado para:\n{0}"), path)
                : L("export_failed", "Não foi possível exportar o histórico.");
        }

#if UNITY_STANDALONE || UNITY_EDITOR
        // No PC abre a pasta para o terapeuta encontrar logo o ficheiro
        if (path != null) Application.OpenURL("file:///" + System.IO.Path.GetDirectoryName(path).Replace("\\", "/"));
#endif
    }

    // ==========================================
    // MAIN GAME LEVELS LOGIC
    // ==========================================

    protected void InitializeAllLevels()
    {
        if (levelButtons == null || levelButtons.Length == 0) return;

        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] == null) continue;

            int levelID = i + 1;
            int currentStars = ProgressManager.GetBestStars(levelID);
            int prevStars = levelID > 1 ? ProgressManager.GetBestStars(levelID - 1) : 0;

            bool isFreeStyleLevel = (i == levelButtons.Length - 1);
            bool isUnlocked = (levelID == 1) || isFreeStyleLevel || (prevStars >= 1) || ProgressManager.IsUnlockAll();

            Button btn = levelButtons[i];
            Image bgImage = btn.GetComponent<Image>();
            TMP_Text levelText = btn.GetComponentInChildren<TMP_Text>();
            Transform starContainer = btn.transform.Find("StarContainer");

            btn.interactable = isUnlocked;

            if (isUnlocked)
            {
                if (bgImage != null) bgImage.color = isFreeStyleLevel ? freeStyleColor : unlockedBGColor;
                if (levelText != null) levelText.color = Color.white;

                if (starContainer != null)
                {
                    starContainer.gameObject.SetActive(true);
                    Image[] stars = starContainer.GetComponentsInChildren<Image>();
                    for (int s = 0; s < stars.Length; s++)
                    {
                        stars[s].color = s < currentStars ? starEarnedColor : starEmptyColor;
                    }
                }

                int captureID = levelID;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => LoadGameLevel(captureID));
            }
            else
            {
                if (bgImage != null) bgImage.color = lockedBGColor;
                if (levelText != null) levelText.color = Color.gray;
                if (starContainer != null) starContainer.gameObject.SetActive(false);
            }
        }
    }

    public void LoadGameLevel(int levelNumber)
    {
        string sceneName = "Level" + levelNumber;
        Debug.Log($"A carregar a cena: {sceneName} (Steering: {SteeringPreference.CurrentSteering})");
        SceneManager.LoadScene(sceneName);
    }

    public void ResetProgress()
    {
        ProgressManager.ResetProgress();
        InitializeAllLevels();
    }

    public void UnlockAllLevels()
    {
        ProgressManager.SetUnlockAll();
        InitializeAllLevels();
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
