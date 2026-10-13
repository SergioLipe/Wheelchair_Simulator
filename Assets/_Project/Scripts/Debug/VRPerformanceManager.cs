using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Qualidade de imagem no Quest: resolucao, foveated rendering, filtragem de texturas e refresh rate.
///
/// - Resolucao 100% (antes 80% nos niveis 7-11, o que deixava a imagem pixelada).
/// - Foveated rendering dinamico: as bordas so perdem resolucao quando a GPU precisa
///   (antes estava sempre no maximo, as bordas ficavam muito desfocadas).
/// - Filtragem anisotropica: chao e estradas nitidos a distancia.
/// - Rede de seguranca: se o jogo nao aguentar os FPS durante alguns segundos, baixa a resolucao
///   um pouco nesse nivel (volta ao valor normal no nivel seguinte).
///
/// Cada nivel pode ter os seus valores: o gestor persiste entre cenas, mas o de cada cena nova
/// passa-lhe as suas definicoes (antes ficavam as do primeiro nivel aberto na sessao).
/// </summary>
public class VRPerformanceManager : MonoBehaviour
{
    [Header("Render Scale")]
    [Tooltip("Resolucao das lentes (1 = nativa do Quest). Valores abaixo de 1 deixam a imagem pixelada.")]
    [Range(0.3f, 1.5f)]
    public float renderScale = 1.0f;

    [Header("Foveation")]
    [Tooltip("Nivel MAXIMO de foveated rendering (0-4). Com o modo dinamico, so chega la quando a GPU precisa.")]
    [Range(0, 4)]
    public int foveationLevel = 3;
    [Tooltip("Ajusta o foveated rendering a carga da GPU (bordas nitidas quando ha folga)")]
    public bool dynamicFoveation = true;

    [Header("Textures")]
    [Tooltip("Filtragem anisotropica forcada (chao/estradas nitidos a distancia). 0 = usar a definicao de cada textura.")]
    [Range(0, 16)]
    public int anisotropicLevel = 4;

    [Header("Refresh Rate")]
    public float targetRefreshRate = 72.0f;

    [Header("Safety Net")]
    [Tooltip("Se os FPS ficarem abaixo do alvo durante alguns segundos, baixa a resolucao em passos ate ao minimo")]
    public bool autoReduceWhenSlow = true;
    [Range(0.5f, 1f)] public float minRenderScale = 0.8f;

    private const float WarmupSeconds = 5f;    // ignorar o carregamento do nivel
    private const float WindowSeconds = 3f;    // janela de medicao dos FPS
    private const int SlowWindowsToReduce = 2; // janelas lentas seguidas antes de baixar
    private const float ReduceStep = 0.1f;
    private const float SlowFpsRatio = 0.93f;  // abaixo de ~67 FPS a 72 Hz

    private static VRPerformanceManager instance;

    private float activeRenderScale;
    private float measureStart;
    private int framesInWindow;
    private int slowWindows;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            // Nova cena: passa as definicoes desta cena para o gestor que ja existe
            instance.CopySettingsFrom(this);
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResetForScene();
    }

    void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    void Start() => Invoke(nameof(ApplyAll), 1.0f);

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetForScene();
        ApplyAll();
    }

    void OnApplicationFocus(bool hasFocus) { if (hasFocus) ApplyAll(); }

    private void CopySettingsFrom(VRPerformanceManager other)
    {
        renderScale = other.renderScale;
        foveationLevel = other.foveationLevel;
        dynamicFoveation = other.dynamicFoveation;
        anisotropicLevel = other.anisotropicLevel;
        targetRefreshRate = other.targetRefreshRate;
        autoReduceWhenSlow = other.autoReduceWhenSlow;
        minRenderScale = other.minRenderScale;
        ResetForScene();
        ApplyAll();
    }

    private void ResetForScene()
    {
        activeRenderScale = renderScale;
        slowWindows = 0;
        framesInWindow = 0;
        measureStart = Time.realtimeSinceStartup + WarmupSeconds;
    }

    public void ApplyAll()
    {
        ApplyTextureFiltering();

        #if !UNITY_EDITOR && UNITY_ANDROID
        ApplyRenderScale();

        // Refresh Rate
        try
        {
            OVRManager.display.displayFrequency = targetRefreshRate;
        }
        catch { }

        // Foveation
        try
        {
            OVRManager.fixedFoveatedRenderingLevel = (OVRManager.FixedFoveatedRenderingLevel)foveationLevel;
            OVRManager.useDynamicFoveatedRendering = dynamicFoveation;
        }
        catch { }

        if (Debug.isDebugBuild)
            Debug.Log($"[VRPerf] scale={activeRenderScale} foveation<={foveationLevel} dynamic={dynamicFoveation} aniso={anisotropicLevel}");
        #endif
    }

    private void ApplyTextureFiltering()
    {
        if (anisotropicLevel <= 0)
        {
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            return;
        }
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        Texture.SetGlobalAnisotropicFilteringLimits(anisotropicLevel, anisotropicLevel);
    }

    private void ApplyRenderScale()
    {
        // eyeTextureResolutionScale e o caminho oficial em XR; o URP renderScale fica igual para nao o sobrescrever
        XRSettings.eyeTextureResolutionScale = activeRenderScale;
        var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                  as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (urp != null) urp.renderScale = activeRenderScale;
    }

    void Update()
    {
        #if !UNITY_EDITOR && UNITY_ANDROID
        // Reaplica se algo sobrescreveu
        if (Time.frameCount % 60 == 0 && Mathf.Abs(XRSettings.eyeTextureResolutionScale - activeRenderScale) > 0.01f)
            ApplyRenderScale();

        if (autoReduceWhenSlow) CheckFrameRate();
        #endif
    }

    /// <summary>
    /// Mede os FPS medios em janelas de 3 s (com o jogo a correr). Se duas janelas seguidas
    /// ficarem abaixo do alvo, baixa a resolucao um passo (custa um pequeno engasgo, mas so acontece
    /// quando o jogo ja esta lento).
    /// </summary>
    private void CheckFrameRate()
    {
        float now = Time.realtimeSinceStartup;
        if (now < measureStart) return;

        // Pausado / menu aberto / headset tirado: nao conta
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

        if (fps >= targetRefreshRate * SlowFpsRatio) { slowWindows = 0; return; }
        if (++slowWindows < SlowWindowsToReduce || activeRenderScale <= minRenderScale + 0.001f) return;

        slowWindows = 0;
        activeRenderScale = Mathf.Max(minRenderScale, activeRenderScale - ReduceStep);
        ApplyRenderScale();
        Debug.LogWarning($"[VRPerf] FPS baixo ({fps:F0}) -> resolucao reduzida para {activeRenderScale:F1} neste nivel");
    }
}
