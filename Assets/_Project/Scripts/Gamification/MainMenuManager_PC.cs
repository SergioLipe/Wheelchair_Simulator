using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Menu principal da versao PC.
/// A logica comum (lista de perfis, historico, exportacao, niveis) esta no MainMenuManagerBase.
/// Aqui ficam so as partes do PC: campo + botao de login, dropdown de perfis (legado) e o cursor sempre visivel.
/// </summary>
public class MainMenuManager_PC : MainMenuManagerBase
{
    [Header("--- PC: Login ---")]
    public TMP_InputField inputFieldProfileID;
    public Button btnLogin;

    [Header("--- PC: History ---")]
    [Tooltip("Legado: dropdown de perfis (mantido por compatibilidade)")]
    public TMP_Dropdown dropdownProfiles;

    [Tooltip("Back button inside HistoryProfileSelectorPanel")]
    public Button btnBackFromHistorySelector;

    // Ultimo perfil com que o utilizador interagiu (para o historico sem painel de escolha)
    private string lastSelectedProfileID = null;

    protected override string LogTag => "[MainMenuManager_PC]";
    protected override TMP_InputField NewProfileInputField => inputFieldProfileID;

    // No PC o botao do modo livre ja tem o titulo; so mostra o numero de sessoes
    protected override bool ShowFreestyleModePrefix => false;

    private void Start()
    {
        // Force cursor visible immediately
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        StartCoroutine(KeepCursorVisible());

        if (HasActiveProfile)
        {
            lastSelectedProfileID = ProfileManager.Instance.currentPlayer.profileID;
        }

        if (btnLogin != null) btnLogin.onClick.AddListener(OnLoginButtonClicked);

        if (dropdownProfiles != null) dropdownProfiles.onValueChanged.AddListener(OnDropdownValueChanged);
        LoadDropdownProfiles();

        SetupCommonMenu(btnBackFromHistorySelector);
    }

    private IEnumerator KeepCursorVisible()
    {
        WaitForEndOfFrame wait = new WaitForEndOfFrame();
        while (true)
        {
            yield return wait;
            if (!Cursor.visible) Cursor.visible = true;
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        }
    }

    // ==========================================
    // LOGIN
    // ==========================================

    protected override void LoginFromProfileList(string profileID) => LoginWithProfileDirect(profileID);

    public void LoginWithProfileDirect(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return;

        lastSelectedProfileID = profileID;
        ActivateProfile(profileID);
    }

    public void OnLoginButtonClicked()
    {
        if (inputFieldProfileID == null) return;

        string typedID = SaveManager.SanitizeProfileID(inputFieldProfileID.text);
        if (string.IsNullOrEmpty(typedID))
        {
            Debug.LogWarning("Atenção: O ID não pode estar vazio!");
            return;
        }

        bool isNewProfile = !SaveManager.ProfileExists(typedID);
        lastSelectedProfileID = typedID;

        ActivateProfile(typedID);

        if (isNewProfile)
        {
            PopulateProfilesList();
            LoadDropdownProfiles();
        }

        inputFieldProfileID.text = "";
    }

    protected override void OnProfileRenamedOrDeleted(string oldID, string newID)
    {
        if (lastSelectedProfileID == oldID) lastSelectedProfileID = newID;
        LoadDropdownProfiles();
    }

    // ==========================================
    // HISTORY (sem painel de escolha de perfil)
    // ==========================================

    protected override void OpenHistoryWithoutSelector() => OpenHistoryLevels();

    public void OpenHistoryLevels()
    {
        string selectedID = null;

        if (!string.IsNullOrEmpty(lastSelectedProfileID))
        {
            selectedID = lastSelectedProfileID;
        }
        else if (dropdownProfiles != null && dropdownProfiles.interactable && dropdownProfiles.options.Count > 0)
        {
            selectedID = dropdownProfiles.options[dropdownProfiles.value].text;
        }
        else if (inputFieldProfileID != null && !string.IsNullOrEmpty(inputFieldProfileID.text.Trim()))
        {
            selectedID = inputFieldProfileID.text.Trim();
        }
        else
        {
            string[] ids = SaveManager.GetAllProfileIDs();
            if (ids != null && ids.Length > 0) selectedID = ids[0];
        }

        OpenHistoryForProfile(selectedID);
    }

    // ==========================================
    // DROPDOWN (legado)
    // ==========================================

    private void LoadDropdownProfiles()
    {
        if (dropdownProfiles == null) return;
        dropdownProfiles.ClearOptions();

        string[] savedIDs = SaveManager.GetAllProfileIDs();
        List<string> options = new List<string>();

        if (savedIDs == null || savedIDs.Length == 0)
        {
            options.Add(L("no_profiles_found", "Nenhum perfil encontrado"));
            dropdownProfiles.AddOptions(options);
            dropdownProfiles.interactable = false;
            if (btnViewHistory != null) btnViewHistory.interactable = false;
        }
        else
        {
            options.AddRange(savedIDs);
            dropdownProfiles.AddOptions(options);
            dropdownProfiles.interactable = true;
            if (btnViewHistory != null) btnViewHistory.interactable = true;
            OnDropdownValueChanged(0);
        }
    }

    private void OnDropdownValueChanged(int index)
    {
        if (!dropdownProfiles.interactable || dropdownProfiles.options.Count == 0) return;
        string selectedID = dropdownProfiles.options[index].text;

        if (inputFieldProfileID != null)
        {
            inputFieldProfileID.text = selectedID;
        }
    }
}
