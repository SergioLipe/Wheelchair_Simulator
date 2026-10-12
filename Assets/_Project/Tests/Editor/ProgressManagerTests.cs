using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Testes do progresso por perfil. Usam uma pasta temporaria para os perfis e
/// guardam/repoem as chaves de PlayerPrefs que tocam.
/// </summary>
public class ProgressManagerTests
{
    private const int TestLevel = 49; // nivel que nao existe no jogo, para nao colidir com progresso real
    private string LegacyKey => "Level_" + TestLevel + "_Stars";

    private string tempDir;
    private GameObject managerObject;
    private ProfileManager previousInstance;
    private bool hadLegacyKey;
    private int previousLegacyValue;
    private bool hadUnlockAll;
    private int previousUnlockAll;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "WheelchairSimTests_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        SaveManager.DataDirectoryOverride = tempDir;

        hadLegacyKey = PlayerPrefs.HasKey(LegacyKey);
        previousLegacyValue = PlayerPrefs.GetInt(LegacyKey, 0);
        PlayerPrefs.DeleteKey(LegacyKey);

        // ResetProgress apaga "UnlockAll": guardar para repor no fim
        hadUnlockAll = PlayerPrefs.HasKey("UnlockAll");
        previousUnlockAll = PlayerPrefs.GetInt("UnlockAll", 0);

        // Em EditMode o Awake nao corre: definimos a instancia a mao
        previousInstance = ProfileManager.Instance;
        managerObject = new GameObject("TestProfileManager");
        ProfileManager.Instance = managerObject.AddComponent<ProfileManager>();
    }

    [TearDown]
    public void TearDown()
    {
        ProfileManager.Instance = previousInstance;
        Object.DestroyImmediate(managerObject);

        if (hadLegacyKey) PlayerPrefs.SetInt(LegacyKey, previousLegacyValue);
        else PlayerPrefs.DeleteKey(LegacyKey);

        if (hadUnlockAll) PlayerPrefs.SetInt("UnlockAll", previousUnlockAll);
        else PlayerPrefs.DeleteKey("UnlockAll");
        PlayerPrefs.Save();

        SaveManager.DataDirectoryOverride = null;
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }

    private PlayerData Login(PlayerData p)
    {
        ProfileManager.Instance.currentPlayer = p;
        return p;
    }

    [Test]
    public void RecordStars_KeepsBestResult()
    {
        Login(PlayerData.CreateNew("A"));

        ProgressManager.RecordStars(TestLevel, 2);
        ProgressManager.RecordStars(TestLevel, 1);
        Assert.AreEqual(2, ProgressManager.GetBestStars(TestLevel));

        ProgressManager.RecordStars(TestLevel, 3);
        Assert.AreEqual(3, ProgressManager.GetBestStars(TestLevel));
    }

    [Test]
    public void Progress_IsSeparatePerProfile()
    {
        PlayerData a = Login(PlayerData.CreateNew("A"));
        ProgressManager.RecordStars(TestLevel, 3);

        Login(PlayerData.CreateNew("B"));
        Assert.AreEqual(0, ProgressManager.GetBestStars(TestLevel), "O paciente B nao pode herdar o progresso do A");

        Login(a);
        Assert.AreEqual(3, ProgressManager.GetBestStars(TestLevel));
    }

    [Test]
    public void RecordStars_DoesNotWriteGlobalPlayerPrefs_WhenProfileActive()
    {
        Login(PlayerData.CreateNew("A"));
        ProgressManager.RecordStars(TestLevel, 3);
        Assert.IsFalse(PlayerPrefs.HasKey(LegacyKey));
    }

    [Test]
    public void OldProfile_MigratesLegacyProgressOnce()
    {
        PlayerPrefs.SetInt(LegacyKey, 2);

        PlayerData old = new PlayerData { profileID = "Antigo" }; // como um perfil gravado antes desta versao
        Login(old);

        Assert.AreEqual(2, ProgressManager.GetBestStars(TestLevel), "Nao pode perder o progresso que ja tinha");
        Assert.IsTrue(old.legacyProgressMigrated);

        // Depois da migracao, mudancas no progresso global ja nao afetam o perfil
        PlayerPrefs.SetInt(LegacyKey, 3);
        Assert.AreEqual(2, ProgressManager.GetBestStars(TestLevel));
    }

    [Test]
    public void NewProfile_DoesNotInheritLegacyProgress()
    {
        PlayerPrefs.SetInt(LegacyKey, 3);
        Login(PlayerData.CreateNew("Novo"));
        Assert.AreEqual(0, ProgressManager.GetBestStars(TestLevel));
    }

    [Test]
    public void ResetProgress_OnlyClearsActiveProfile()
    {
        PlayerData a = Login(PlayerData.CreateNew("A"));
        ProgressManager.RecordStars(TestLevel, 3);
        SaveManager.SaveProfile(a);

        PlayerData b = Login(PlayerData.CreateNew("B"));
        ProgressManager.RecordStars(TestLevel, 2);
        ProgressManager.ResetProgress();

        Assert.AreEqual(0, ProgressManager.GetBestStars(TestLevel));
        Assert.AreEqual(3, SaveManager.LoadProfile("A").levelProgress[0].bestStars);
    }

    [Test]
    public void NoProfile_FallsBackToPlayerPrefs()
    {
        ProfileManager.Instance.currentPlayer = null;
        ProgressManager.RecordStars(TestLevel, 2);
        Assert.AreEqual(2, ProgressManager.GetBestStars(TestLevel));
    }
}
