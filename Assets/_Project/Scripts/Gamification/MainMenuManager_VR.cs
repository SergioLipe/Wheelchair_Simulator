using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Menu principal da versao VR.
/// A logica comum (lista de perfis, historico, exportacao, niveis) esta no MainMenuManagerBase.
/// Aqui ficam so as partes do VR: criar perfil, escolha do tipo de direcao e recentrar o canvas.
/// </summary>
public class MainMenuManager_VR : MainMenuManagerBase
{
    [Tooltip("Optional: text shown when there are no saved profiles yet")]
    public GameObject noProfilesPlaceholder;

    [Header("--- VR: Create New Profile ---")]
    public TMP_InputField inputFieldNewProfileID;
    public Button btnCreateProfile;

    [Header("--- VR: View History ---")]
    [Tooltip("VR: Button to close the history profile selector and go back to login")]
    public Button btnCloseHistoryProfileSelector;

    [Header("--- Steering Type Selection (LevelSelectionPanel) ---")]
    [Tooltip("Button to select Front Steering (Frontal)")]
    public Button btnSteeringFront;

    [Tooltip("Button to select Rear Steering (Traseira)")]
    public Button btnSteeringRear;

    [Header("--- Steering Button Visual States ---")]
    [Tooltip("Image color when the button IS the selected steering type")]
    public Color steeringSelectedColor = new Color(0.376f, 0.647f, 0.980f, 0.314f); // azul subtil 80a

    [Tooltip("Outline color when the button IS the selected steering type")]
    public Color steeringSelectedOutlineColor = new Color(0.376f, 0.647f, 0.980f, 0.863f); // azul 220a

    [Tooltip("Text color when the button IS the selected steering type")]
    public Color steeringSelectedTextColor = Color.white;

    [Tooltip("Image color when the button is NOT the selected steering type")]
    public Color steeringUnselectedColor = new Color(1f, 1f, 1f, 0.078f); // branco 20a

    [Tooltip("Outline color when the button is NOT the selected steering type")]
    public Color steeringUnselectedOutlineColor = new Color(1f, 1f, 1f, 0.314f); // branco 80a

    [Tooltip("Text color when the button is NOT the selected steering type")]
    public Color steeringUnselectedTextColor = new Color(1f, 1f, 1f, 0.706f); // branco 180a

    protected override string LogTag => "[MainMenuManager_VR]";
    protected override TMP_InputField NewProfileInputField => inputFieldNewProfileID;

    private void Start()
    {
        if (btnCreateProfile != null) btnCreateProfile.onClick.AddListener(OnCreateProfileClicked);

        if (btnSteeringFront != null)
        {
            btnSteeringFront.onClick.RemoveAllListeners();
            btnSteeringFront.onClick.AddListener(() => OnSteeringSelected(WheelController.SteeringType.FrontSteering));
        }
        if (btnSteeringRear != null)
        {
            btnSteeringRear.onClick.RemoveAllListeners();
            btnSteeringRear.onClick.AddListener(() => OnSteeringSelected(WheelController.SteeringType.RearSteering));
        }
        RefreshSteeringButtonsVisual();

        SetupCommonMenu(btnCloseHistoryProfileSelector);

        // Force recenter the VR canvas
        VRCanvasPositioner positioner = FindObjectOfType<VRCanvasPositioner>();
        if (positioner != null)
        {
            positioner.RecenterCanvas();
        }
    }

    protected override void OnProfilesListPopulated(int profileCount)
    {
        if (noProfilesPlaceholder != null) noProfilesPlaceholder.SetActive(profileCount == 0);
    }

    // ==========================================
    // STEERING TYPE SELECTION
    // ==========================================

    public void OnSteeringSelected(WheelController.SteeringType steeringType)
    {
        SteeringPreference.SetSteering(steeringType);
        RefreshSteeringButtonsVisual();
    }

    private void RefreshSteeringButtonsVisual()
    {
        bool frontSelected = SteeringPreference.CurrentSteering == WheelController.SteeringType.FrontSteering;
        bool rearSelected = SteeringPreference.CurrentSteering == WheelController.SteeringType.RearSteering;

        SetSteeringButtonVisual(btnSteeringFront, frontSelected);
        SetSteeringButtonVisual(btnSteeringRear, rearSelected);
    }

    private void SetSteeringButtonVisual(Button button, bool isSelected)
    {
        if (button == null) return;

        Image bgImage = button.GetComponent<Image>();
        if (bgImage != null)
        {
            bgImage.color = isSelected ? steeringSelectedColor : steeringUnselectedColor;
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = isSelected ? steeringSelectedOutlineColor : steeringUnselectedOutlineColor;
        }

        TMP_Text txt = button.GetComponentInChildren<TMP_Text>();
        if (txt != null)
        {
            txt.color = isSelected ? steeringSelectedTextColor : steeringUnselectedTextColor;
        }
    }

    // ==========================================
    // LOGIN / CREATE PROFILE
    // ==========================================

    protected override void LoginFromProfileList(string profileID) => LoginWithProfile(profileID);

    public void LoginWithProfile(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return;

        ActivateProfile(profileID);
        RefreshSteeringButtonsVisual();
    }

    public void OnCreateProfileClicked()
    {
        if (inputFieldNewProfileID == null) return;

        string typedID = SaveManager.SanitizeProfileID(inputFieldNewProfileID.text);
        if (string.IsNullOrEmpty(typedID))
        {
            Debug.LogWarning("Atenção: O ID não pode estar vazio!");
            return;
        }

        bool isNewProfile = !SaveManager.ProfileExists(typedID);

        ActivateProfile(typedID);

        if (isNewProfile) PopulateProfilesList();
        inputFieldNewProfileID.text = "";

        RefreshSteeringButtonsVisual();
    }
}
