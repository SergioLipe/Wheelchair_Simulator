using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// Menu de definicoes da versao PC: qualidade grafica, resolucao, ecra inteiro e campo de visao.
///
/// Criado em codigo (pelo PCGraphicsQuality), por isso nao precisa de nada nas cenas.
/// O botao "Definicoes" aparece no canto inferior esquerdo no menu principal e com o jogo em pausa.
/// </summary>
public class PCSettingsMenu : MonoBehaviour
{
    private const float FovStep = 5f;

    private static readonly Color PanelColor = new Color(0.08f, 0.1f, 0.13f, 0.96f);
    private static readonly Color ButtonColor = new Color(0.2f, 0.24f, 0.3f, 1f);
    private static readonly Color AccentColor = new Color(0f, 0.6f, 1f, 1f);

    private Canvas canvas;
    private GameObject openButton;
    private GameObject panel;
    private Transform box;

    private TMP_Text qualityValue, resolutionValue, fullscreenValue, fovValue;
    private readonly List<TMP_Text> localizedLabels = new List<TMP_Text>();
    private readonly List<string> localizedKeys = new List<string>();
    private readonly List<string> localizedFallbacks = new List<string>();

    private List<Vector2Int> resolutions;

    // ==========================================
    // CICLO DE VIDA
    // ==========================================

    private void Start()
    {
        BuildUI();
        panel.SetActive(false);
        openButton.SetActive(false);
    }

    private void Update()
    {
        bool allowed = !XRSettings.isDeviceActive && (IsMainMenu() || IsGamePaused());

        if (!allowed && panel.activeSelf) panel.SetActive(false);
        bool showButton = allowed && !panel.activeSelf;
        if (openButton.activeSelf != showButton)
        {
            if (showButton) { EnsureEventSystem(); RefreshLabels(); }
            openButton.SetActive(showButton);
        }

        if (panel.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private static bool IsMainMenu()
    {
        string scene = SceneManager.GetActiveScene().name;
        return scene == "MainMenu" || scene.Contains("Menu");
    }

    private static bool IsGamePaused() => LevelManager_PC.Instance != null && LevelManager_PC.Instance.IsPaused;

    public void Open()
    {
        RefreshLabels();
        RefreshValues();
        panel.SetActive(true);
    }

    public void Close() => panel.SetActive(false);

    // ==========================================
    // ACOES
    // ==========================================

    private void ChangeQuality(int dir)
    {
        int t = Mathf.Clamp((int)PCGraphicsQuality.CurrentTier + dir, 0, (int)PCGraphicsQuality.Tier.Alta);
        PCGraphicsQuality.SelectTier((PCGraphicsQuality.Tier)t);
        RefreshValues();
    }

    private void ChangeResolution(int dir)
    {
        if (resolutions == null || resolutions.Count == 0) return;
        int i = Mathf.Clamp(CurrentResolutionIndex() + dir, 0, resolutions.Count - 1);
        Vector2Int r = resolutions[i];
        Screen.SetResolution(r.x, r.y, Screen.fullScreenMode);
        resolutionValue.text = $"{r.x} x {r.y}";
    }

    private void ToggleFullscreen()
    {
        bool full = Screen.fullScreenMode != FullScreenMode.Windowed;
        Screen.fullScreenMode = full ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        fullscreenValue.text = YesNo(!full);
    }

    private void ChangeFov(int dir)
    {
        float current = CurrentFov();
        float next = Mathf.Clamp(Mathf.Round((current + dir * FovStep) / FovStep) * FovStep,
                                 PCGraphicsQuality.MinFov, PCGraphicsQuality.MaxFov);
        PCGraphicsQuality.FieldOfView = next;
        if (Camera.main != null) Camera.main.fieldOfView = next;
        fovValue.text = $"{next:F0}°";
    }

    private static float CurrentFov()
    {
        float saved = PCGraphicsQuality.FieldOfView;
        if (saved > 0f) return saved;
        return Camera.main != null ? Camera.main.fieldOfView : 60f;
    }

    private int CurrentResolutionIndex()
    {
        int best = 0;
        for (int i = 0; i < resolutions.Count; i++)
            if (resolutions[i].x == Screen.width && resolutions[i].y == Screen.height) return i;
            else if (resolutions[i].x * resolutions[i].y <= Screen.width * Screen.height) best = i;
        return best;
    }

    private void RefreshValues()
    {
        resolutions = new List<Vector2Int>();
        foreach (Resolution r in Screen.resolutions)
        {
            Vector2Int v = new Vector2Int(r.width, r.height);
            if (!resolutions.Contains(v)) resolutions.Add(v);
        }
        resolutions.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));

        qualityValue.text = PCGraphicsQuality.TierName(PCGraphicsQuality.CurrentTier);
        resolutionValue.text = $"{Screen.width} x {Screen.height}";
        fullscreenValue.text = YesNo(Screen.fullScreenMode != FullScreenMode.Windowed);
        fovValue.text = $"{CurrentFov():F0}°";
    }

    private void RefreshLabels()
    {
        for (int i = 0; i < localizedLabels.Count; i++)
            localizedLabels[i].text = PCGraphicsQuality.L(localizedKeys[i], localizedFallbacks[i]);
    }

    private static string YesNo(bool v) =>
        v ? PCGraphicsQuality.L("settings_yes", "Sim") : PCGraphicsQuality.L("settings_no", "Não");

    // ==========================================
    // CONSTRUCAO DA INTERFACE
    // ==========================================

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("PCSettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Botao para abrir (canto inferior esquerdo)
        openButton = CreateButton(canvas.transform, "settings_title", "Definições", Open, AccentColor);
        RectTransform ob = (RectTransform)openButton.transform;
        ob.anchorMin = ob.anchorMax = ob.pivot = Vector2.zero;
        ob.anchoredPosition = new Vector2(24f, 24f);
        ob.sizeDelta = new Vector2(240f, 64f);

        // Fundo escuro que bloqueia os cliques no menu de tras
        panel = new GameObject("SettingsBlocker", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        RectTransform blocker = (RectTransform)panel.transform;
        blocker.anchorMin = Vector2.zero;
        blocker.anchorMax = Vector2.one;
        blocker.offsetMin = blocker.offsetMax = Vector2.zero;

        // Painel
        box = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image)).transform;
        box.SetParent(panel.transform, false);
        box.GetComponent<Image>().color = PanelColor;
        ((RectTransform)box).sizeDelta = new Vector2(860f, 600f);

        TMP_Text title = CreateText(box, "settings_title", "Definições", 46f, FontStyles.Bold);
        Place(title.rectTransform, new Vector2(0f, 240f), new Vector2(800f, 70f));
        title.alignment = TextAlignmentOptions.Center;

        qualityValue = CreateRow("settings_quality", "Qualidade gráfica", 130f, () => ChangeQuality(-1), () => ChangeQuality(1));
        resolutionValue = CreateRow("settings_resolution", "Resolução", 40f, () => ChangeResolution(-1), () => ChangeResolution(1));
        fullscreenValue = CreateRow("settings_fullscreen", "Ecrã inteiro", -50f, ToggleFullscreen, ToggleFullscreen);
        fovValue = CreateRow("settings_fov", "Campo de visão", -140f, () => ChangeFov(-1), () => ChangeFov(1));

        GameObject close = CreateButton(box, "settings_close", "Fechar", Close, AccentColor);
        Place((RectTransform)close.transform, new Vector2(0f, -240f), new Vector2(260f, 70f));
    }

    private TMP_Text CreateRow(string key, string fallback, float y, UnityEngine.Events.UnityAction left, UnityEngine.Events.UnityAction right)
    {
        TMP_Text label = CreateText(box, key, fallback, 32f, FontStyles.Normal);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        Place(label.rectTransform, new Vector2(-190f, y), new Vector2(380f, 70f));

        GameObject l = CreateButton(box, null, "<", left, ButtonColor);
        Place((RectTransform)l.transform, new Vector2(60f, y), new Vector2(70f, 64f));

        TMP_Text value = CreateText(box, null, "", 32f, FontStyles.Bold);
        value.alignment = TextAlignmentOptions.Center;
        Place(value.rectTransform, new Vector2(215f, y), new Vector2(230f, 70f));

        GameObject r = CreateButton(box, null, ">", right, ButtonColor);
        Place((RectTransform)r.transform, new Vector2(370f, y), new Vector2(70f, 64f));
        return value;
    }

    private GameObject CreateButton(Transform parent, string key, string fallback, UnityEngine.Events.UnityAction onClick, Color color)
    {
        GameObject go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        Button b = go.GetComponent<Button>();
        b.onClick.AddListener(onClick);

        TMP_Text t = CreateText(go.transform, key, fallback, 30f, FontStyles.Bold);
        t.alignment = TextAlignmentOptions.Center;
        RectTransform tr = t.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        return go;
    }

    private TMP_Text CreateText(Transform parent, string key, string fallback, float size, FontStyles style)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = key != null ? PCGraphicsQuality.L(key, fallback) : fallback;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = Color.white;
        t.raycastTarget = false;
        if (key != null)
        {
            localizedLabels.Add(t);
            localizedKeys.Add(key);
            localizedFallbacks.Add(fallback);
        }
        return t;
    }

    private static void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
