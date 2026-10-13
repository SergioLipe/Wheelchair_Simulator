using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Menu de definicoes do PC: aparece no menu principal e na pausa, muda a qualidade e o campo de visao.
/// Usa reflexao (esta assembly nao pode referenciar o Assembly-CSharp).
/// </summary>
public class PCSettingsMenuTests
{
    private const string QualityKey = "pc_graphics_quality";
    private const string FovKey = "pc_fov";

    private int savedQuality;
    private float savedFov;
    private bool hadQuality, hadFov;

    private static Type T(string name) =>
        Type.GetType(name + ", Assembly-CSharp") ?? throw new Exception("Tipo nao encontrado: " + name);

    private static object Field(object o, string name) =>
        o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(o);

    private static void Call(object o, string name, params object[] args) =>
        o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(o, args);

    private static object CurrentTier => T("PCGraphicsQuality").GetProperty("CurrentTier").GetValue(null);

    [SetUp]
    public void SavePrefs()
    {
        hadQuality = PlayerPrefs.HasKey(QualityKey);
        hadFov = PlayerPrefs.HasKey(FovKey);
        savedQuality = PlayerPrefs.GetInt(QualityKey);
        savedFov = PlayerPrefs.GetFloat(FovKey);
        LogAssert.ignoreFailingMessages = true;
    }

    [TearDown]
    public void RestorePrefs()
    {
        if (hadQuality) PlayerPrefs.SetInt(QualityKey, savedQuality); else PlayerPrefs.DeleteKey(QualityKey);
        if (hadFov) PlayerPrefs.SetFloat(FovKey, savedFov); else PlayerPrefs.DeleteKey(FovKey);
        PlayerPrefs.Save();
        Time.timeScale = 1f;
    }

    private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

    private static MonoBehaviour FindMenu()
    {
        UnityEngine.Object menu = UnityEngine.Object.FindFirstObjectByType(T("PCSettingsMenu"));
        Assert.IsNotNull(menu, "O PCSettingsMenu devia ser criado automaticamente no PC");
        return (MonoBehaviour)menu;
    }

    [UnityTest]
    public IEnumerator MainMenu_ShowsButton_AndChangesQualityAndFov()
    {
        SceneManager.LoadScene("MainMenu");
        yield return Frames(10);

        MonoBehaviour menu = FindMenu();
        GameObject openButton = (GameObject)Field(menu, "openButton");
        GameObject panel = (GameObject)Field(menu, "panel");
        Assert.IsTrue(openButton.activeSelf, "Botao Definicoes visivel no menu principal");
        Assert.IsFalse(panel.activeSelf);

        Call(menu, "Open");
        yield return null;
        Assert.IsTrue(panel.activeSelf, "Painel abre");
        Assert.IsFalse(openButton.activeSelf, "Botao escondido com o painel aberto");

        // Qualidade: ir ate Alta, descer um nivel
        Call(menu, "ChangeQuality", 1); Call(menu, "ChangeQuality", 1);
        Assert.AreEqual("Alta", CurrentTier.ToString());
        Call(menu, "ChangeQuality", -1);
        Assert.AreEqual("Media", CurrentTier.ToString());
        Assert.AreEqual(1, PlayerPrefs.GetInt(QualityKey), "Qualidade guardada");

        // Campo de visao
        float before = (float)T("PCSettingsMenu").GetMethod("CurrentFov", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Call(menu, "ChangeFov", 1);
        Assert.AreEqual(Mathf.Min(before + 5f, 100f), PlayerPrefs.GetFloat(FovKey), 0.01f);
        if (Camera.main != null) Assert.AreEqual(PlayerPrefs.GetFloat(FovKey), Camera.main.fieldOfView, 0.01f);

        Call(menu, "Close");
        yield return null;
        Assert.IsFalse(panel.activeSelf);
        Assert.IsTrue(openButton.activeSelf);
    }

    [UnityTest]
    public IEnumerator Level_ButtonOnlyWhilePaused()
    {
        SceneManager.LoadScene("Level1");
        yield return Frames(10);

        MonoBehaviour menu = FindMenu();
        GameObject openButton = (GameObject)Field(menu, "openButton");

        object levelManager = T("LevelManager_PC").GetProperty("Instance").GetValue(null);
        Assert.IsNotNull(levelManager, "LevelManager_PC no Level1");

        Call(levelManager, "ResumeGame");
        yield return Frames(2);
        Assert.IsFalse(openButton.activeSelf, "Sem botao durante o jogo");

        Call(levelManager, "PauseGame");
        yield return Frames(2);
        Assert.IsTrue(openButton.activeSelf, "Botao aparece na pausa");

        Call(levelManager, "ResumeGame");
        yield return Frames(2);
        Assert.IsFalse(openButton.activeSelf, "Botao desaparece ao retomar");
    }
}
