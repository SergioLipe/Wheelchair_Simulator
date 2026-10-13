using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Qualidade grafica da versao PC (nao corre no Quest nem com headset ligado ao PC).
///
/// Cria-se sozinho no arranque, sem precisar de nada nas cenas. Tres niveis:
///  - Alta:  MSAA 4x, oclusao ambiente (SSAO), sombras 4096, pos-processamento completo, anisotropica 16x
///  - Media: MSAA 2x, sombras 2048, pos-processamento sem bloom, anisotropica 8x
///  - Baixa: FXAA, sombras 1024 mais curtas, so correcao de cor, anisotropica 4x
///
/// O nivel inicial e escolhido pela placa grafica (integradas -> Baixa) e fica guardado.
/// F9 muda de nivel. Se os FPS ficarem baixos durante alguns segundos, desce um nivel sozinho.
/// Linha de comandos: -gfxQuality Alta|Media|Baixa (ex.: para o benchmark). -gfxOff desliga tudo (aspeto original).
/// </summary>
public class PCGraphicsQuality : MonoBehaviour
{
    public enum Tier { Baixa = 0, Media = 1, Alta = 2 }

    private const string PrefKey = "pc_graphics_quality";
    private const KeyCode CycleKey = KeyCode.F9;

    // Rede de seguranca
    private const float WarmupSeconds = 5f;
    private const float WindowSeconds = 3f;
    private const int SlowWindowsToReduce = 2;
    private const float SlowFpsRatio = 0.8f; // abaixo de 80% do refresh do monitor (max. 60)

    private static PCGraphicsQuality instance;

    public static Tier CurrentTier => instance != null ? instance.tier : Tier.Alta;

    /// <summary>Muda o nivel (menu de definicoes).</summary>
    public static void SelectTier(Tier t) { if (instance != null) instance.SetTier(t, true, false); }

    private const string FovKey = "pc_fov";
    public const float MinFov = 50f, MaxFov = 100f;

    /// <summary>Campo de visao da camara do PC (graus). 0 = o valor da cena.</summary>
    public static float FieldOfView
    {
        get => PlayerPrefs.GetFloat(FovKey, 0f);
        set
        {
            PlayerPrefs.SetFloat(FovKey, value <= 0f ? 0f : Mathf.Clamp(value, MinFov, MaxFov));
            PlayerPrefs.Save();
            if (instance != null) instance.configuredCameras.Clear();
        }
    }

    private Tier tier;
    private VolumeProfile profile;
    private Bloom bloom;
    private readonly HashSet<int> configuredCameras = new HashSet<int>();

    private float measureStart;
    private int framesInWindow;
    private int slowWindows;

    private string notice;
    private float noticeUntil;
    private GUIStyle noticeStyle;

    // Valores originais dos assets (no Editor, alterar um asset em Play fica gravado no disco)
    private UniversalRenderPipelineAsset urp;
    private int origMsaa, origShadowRes;
    private float origShadowDistance;
    private AnisotropicFiltering origAniso;
    private int origVSync;
    private readonly List<KeyValuePair<ScriptableRendererFeature, bool>> origFeatures =
        new List<KeyValuePair<ScriptableRendererFeature, bool>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // Verificado em tempo de execucao: no Editor (mesmo com a plataforma Android ativa) tambem corre
        if (Application.platform == RuntimePlatform.Android || instance != null) return;
        if (HasArg("-gfxOff")) return; // para comparar com o aspeto original
        GameObject go = new GameObject("PCGraphicsQuality");
        DontDestroyOnLoad(go);
        go.AddComponent<PCGraphicsQuality>();
        go.AddComponent<PCSettingsMenu>();
    }

    private static bool XRActive => XRSettings.isDeviceActive;

    // ==========================================
    // CICLO DE VIDA
    // ==========================================

    private void Awake()
    {
        instance = this;
        urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        // So altera o asset do PC. No Editor com a plataforma Android, o asset ativo e o do Quest: nao lhe mexer
        if (urp != null && !urp.name.StartsWith("PC")) urp = null;
        SaveOriginals();

        tier = ChooseInitialTier();
        CreatePostProcessing();

        // VSync: evita gastar a placa grafica a fazer centenas de FPS que o monitor nao mostra
        if (!HasArg("-benchmark")) QualitySettings.vSyncCount = 1;

        ApplyTier();
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResetMeasurement();
    }

    private void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RestoreOriginals();
        if (profile != null) Destroy(profile);
        if (instance == this) instance = null;
    }

    private void OnApplicationQuit()
    {
        RestoreOriginals();
        QualitySettings.vSyncCount = origVSync;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        configuredCameras.Clear();
        ResetMeasurement();
    }

    private void Update()
    {
        if (XRActive) return;

        if (Input.GetKeyDown(CycleKey))
        {
            SetTier(tier == Tier.Baixa ? Tier.Alta : tier - 1, true);
            return;
        }

        CheckFrameRate();
    }

    // ==========================================
    // NIVEIS
    // ==========================================

    private Tier ChooseInitialTier()
    {
        string arg = ArgValue("-gfxQuality");
        if (!string.IsNullOrEmpty(arg) && System.Enum.TryParse(arg, true, out Tier fromArg)) return fromArg;

        int saved = PlayerPrefs.GetInt(PrefKey, -1);
        if (saved >= 0 && saved <= (int)Tier.Alta) return (Tier)saved;

        return DetectTier(SystemInfo.graphicsDeviceName, SystemInfo.graphicsMemorySize);
    }

    /// <summary>Nivel sugerido pela placa grafica (publico para os testes).</summary>
    public static Tier DetectTier(string gpuName, int memoryMB)
    {
        string n = (gpuName ?? "").ToLowerInvariant();

        // Graficas integradas (Intel UHD/Iris/HD, AMD "Radeon Graphics" dos processadores)
        bool integrated = n.Contains("uhd") || n.Contains("iris") || n.Contains("hd graphics")
                          || n.Contains("radeon(tm) graphics") || n.Contains("radeon graphics")
                          || n.Contains("vega") && !n.Contains("rx")
                          || n.Contains("microsoft basic");
        if (integrated) return Tier.Baixa;

        if (memoryMB > 0 && memoryMB < 3000) return Tier.Baixa;
        if (memoryMB > 0 && memoryMB < 6000) return Tier.Media;
        return Tier.Alta;
    }

    private void SetTier(Tier newTier, bool manual, bool showNotice = true)
    {
        tier = newTier;
        PlayerPrefs.SetInt(PrefKey, (int)tier);
        PlayerPrefs.Save();
        ApplyTier();
        ResetMeasurement();
        if (!showNotice) return; // o menu de definicoes ja mostra o nivel

        string label = L("gfx_quality", "Gráficos") + ": " + TierName(tier);
        if (!manual) label += "  (" + L("gfx_auto_reduced", "reduzido para manter a fluidez") + ")";
        ShowNotice(label);
    }

    private void ApplyTier()
    {
        configuredCameras.Clear(); // as cameras voltam a ser configuradas no proximo frame
        if (XRActive) { RestoreOriginals(); return; }

        if (urp != null)
        {
            urp.msaaSampleCount = tier == Tier.Alta ? 4 : tier == Tier.Media ? 2 : 1;
            urp.mainLightShadowmapResolution = tier == Tier.Alta ? 4096 : tier == Tier.Media ? 2048 : 1024;
            urp.shadowDistance = tier == Tier.Baixa ? Mathf.Min(origShadowDistance, 30f) : origShadowDistance;
        }

        SetFeatureActive<ScreenSpaceAmbientOcclusion>(tier == Tier.Alta);

        int aniso = tier == Tier.Alta ? 16 : tier == Tier.Media ? 8 : 4;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        Texture.SetGlobalAnisotropicFilteringLimits(aniso, aniso);

        if (bloom != null) bloom.active = tier == Tier.Alta;
    }

    // ==========================================
    // CAMERAS E POS-PROCESSAMENTO
    // ==========================================

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam.cameraType != CameraType.Game || cam.stereoEnabled || cam.targetTexture != null) return;
        if (XRActive || !configuredCameras.Add(cam.GetInstanceID())) return;

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (data == null || data.renderType != CameraRenderType.Base) return;

        data.renderPostProcessing = true;
        data.antialiasing = tier == Tier.Baixa ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
        data.antialiasingQuality = AntialiasingQuality.High;

        float fov = FieldOfView;
        if (fov > 0f) cam.fieldOfView = fov;
    }

    private void CreatePostProcessing()
    {
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "PC_PostProcessing";

        // Tonemapping neutro: as zonas muito claras deixam de "queimar" sem mudar as cores
        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.Neutral);

        // Um pouco mais de contraste e cor
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.contrast.Override(8f);
        color.saturation.Override(8f);

        // Bloom subtil (so no nivel Alta): brilho a volta do sol, candeeiros e reflexos
        bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(0.3f);
        bloom.scatter.Override(0.6f);
        bloom.highQualityFiltering.Override(false);

        GameObject volumeObject = new GameObject("PC_PostProcessingVolume");
        volumeObject.transform.SetParent(transform, false);
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = -10f; // volumes das cenas (se houver) tem prioridade
        volume.sharedProfile = profile;
    }

    // ==========================================
    // REDE DE SEGURANCA
    // ==========================================

    private void ResetMeasurement()
    {
        framesInWindow = 0;
        slowWindows = 0;
        measureStart = Time.realtimeSinceStartup + WarmupSeconds;
    }

    private void CheckFrameRate()
    {
        if (tier == Tier.Baixa) return;

        float now = Time.realtimeSinceStartup;
        if (now < measureStart) return;

        // Pausado, menu ou janela sem foco: nao conta
        if (Time.timeScale == 0f || !Application.isFocused)
        {
            framesInWindow = 0;
            measureStart = now;
            return;
        }

        framesInWindow++;
        float elapsed = now - measureStart;
        if (elapsed < WindowSeconds) return;

        float fps = framesInWindow / elapsed;
        framesInWindow = 0;
        measureStart = now;

        float refresh = (float)Screen.currentResolution.refreshRateRatio.value;
        float target = Mathf.Min(refresh > 1f ? refresh : 60f, 60f) * SlowFpsRatio;

        if (fps >= target) { slowWindows = 0; return; }
        if (++slowWindows < SlowWindowsToReduce) return;

        Debug.LogWarning($"[PCGraphics] FPS baixo ({fps:F0}) -> qualidade {TierName(tier - 1)}");
        SetTier(tier - 1, false);
    }

    // ==========================================
    // AVISO NO ECRA
    // ==========================================

    private void ShowNotice(string text)
    {
        notice = text;
        noticeUntil = Time.unscaledTime + 2.5f;
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(notice) || Time.unscaledTime > noticeUntil) return;

        if (noticeStyle == null)
        {
            noticeStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter };
            noticeStyle.normal.textColor = Color.white;
        }
        noticeStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.025f);

        float w = Screen.width * 0.45f, h = Screen.height * 0.06f;
        GUI.Box(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.08f, w, h), notice, noticeStyle);
    }

    // ==========================================
    // AUXILIARES
    // ==========================================

    public static string TierName(Tier t)
    {
        switch (t)
        {
            case Tier.Alta: return L("gfx_high", "Alta");
            case Tier.Media: return L("gfx_medium", "Média");
            default: return L("gfx_low", "Baixa");
        }
    }

    private void SetFeatureActive<T>(bool active) where T : ScriptableRendererFeature
    {
        if (urp == null) return;
        foreach (ScriptableRendererData data in urp.rendererDataList)
        {
            if (data == null) continue;
            foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                if (feature is T) feature.SetActive(active);
        }
    }

    private void SaveOriginals()
    {
        origAniso = QualitySettings.anisotropicFiltering;
        origVSync = QualitySettings.vSyncCount;
        if (urp == null) return;
        origMsaa = urp.msaaSampleCount;
        origShadowRes = urp.mainLightShadowmapResolution;
        origShadowDistance = urp.shadowDistance;

        origFeatures.Clear();
        foreach (ScriptableRendererData data in urp.rendererDataList)
        {
            if (data == null) continue;
            foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                if (feature != null) origFeatures.Add(new KeyValuePair<ScriptableRendererFeature, bool>(feature, feature.isActive));
        }
    }

    private void RestoreOriginals()
    {
        QualitySettings.anisotropicFiltering = origAniso;
        if (urp == null) return;
        urp.msaaSampleCount = origMsaa;
        urp.mainLightShadowmapResolution = origShadowRes;
        urp.shadowDistance = origShadowDistance;
        foreach (var pair in origFeatures)
            if (pair.Key != null) pair.Key.SetActive(pair.Value);
    }

    public static string L(string key, string fallback)
    {
        try
        {
            string v = LocalizationSettings.StringDatabase.GetLocalizedString("GameText", key);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }
        catch { return fallback; }
    }

    private static bool HasArg(string name)
    {
        foreach (string a in System.Environment.GetCommandLineArgs())
            if (a == name) return true;
        return false;
    }

    private static string ArgValue(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
