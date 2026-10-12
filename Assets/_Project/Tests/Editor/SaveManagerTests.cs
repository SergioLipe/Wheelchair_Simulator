using System.IO;
using NUnit.Framework;

/// <summary>
/// Testes do SaveManager. Usam uma pasta temporaria (DataDirectoryOverride),
/// por isso nunca tocam nos perfis reais.
/// </summary>
public class SaveManagerTests
{
    private string tempDir;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "WheelchairSimTests_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        SaveManager.DataDirectoryOverride = tempDir;
    }

    [TearDown]
    public void TearDown()
    {
        SaveManager.DataDirectoryOverride = null;
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }

    private string ProfilePath(string id) => Path.Combine(tempDir, id + "_profile.json");

    [TestCase("Ana", "Ana")]
    [TestCase("  João Silva  ", "João Silva")]
    [TestCase("Ana/Silva", "Ana_Silva")]
    [TestCase("a\\b", "a_b")]
    [TestCase("x:y*z?", "x_y_z_")]
    [TestCase("nome.", "nome")]
    [TestCase("", "")]
    [TestCase(null, "")]
    public void SanitizeProfileID_RemovesInvalidCharacters(string raw, string expected)
    {
        Assert.AreEqual(expected, SaveManager.SanitizeProfileID(raw));
    }

    [Test]
    public void SaveAndLoad_RoundTripsSessionsAndProgress()
    {
        PlayerData p = PlayerData.CreateNew("Paciente1");
        p.sessionHistory.Add(new SessionRecord { levelName = "Level3", completionTime = 42.5f, totalCollisions = 2, totalSlides = 1 });
        p.levelProgress.Add(new LevelProgress { levelID = 3, bestStars = 2 });

        Assert.IsTrue(SaveManager.SaveProfile(p));
        PlayerData loaded = SaveManager.LoadProfile("Paciente1");

        Assert.IsNotNull(loaded);
        Assert.AreEqual("Paciente1", loaded.profileID);
        Assert.AreEqual(1, loaded.sessionHistory.Count);
        Assert.AreEqual(42.5f, loaded.sessionHistory[0].completionTime);
        Assert.AreEqual(2, loaded.sessionHistory[0].totalCollisions);
        Assert.AreEqual(2, loaded.levelProgress[0].bestStars);
        Assert.IsTrue(loaded.legacyProgressMigrated);
    }

    [Test]
    public void Save_DoesNotLeaveTempFile()
    {
        SaveManager.SaveProfile(PlayerData.CreateNew("A"));
        SaveManager.SaveProfile(PlayerData.CreateNew("A"));
        Assert.IsFalse(File.Exists(ProfilePath("A") + ".tmp"));
        Assert.IsTrue(File.Exists(ProfilePath("A")));
    }

    [Test]
    public void Load_CorruptedFile_RecoversFromBackup()
    {
        PlayerData p = PlayerData.CreateNew("B");
        p.sessionHistory.Add(new SessionRecord { levelName = "Level1" });
        SaveManager.SaveProfile(p);
        SaveManager.SaveProfile(p); // segunda gravacao cria o .bak

        File.WriteAllText(ProfilePath("B"), "{ isto nao e json");

        PlayerData loaded = SaveManager.LoadProfile("B");
        Assert.IsNotNull(loaded, "Devia recuperar a partir do .bak");
        Assert.AreEqual(1, loaded.sessionHistory.Count);
    }

    [Test]
    public void Load_OldProfileWithoutNewFields_GetsDefaults()
    {
        File.WriteAllText(ProfilePath("Antigo"), "{\"profileID\":\"Antigo\",\"sessionHistory\":[]}");

        PlayerData loaded = SaveManager.LoadProfile("Antigo");
        Assert.IsNotNull(loaded);
        Assert.IsNotNull(loaded.levelProgress);
        Assert.IsNotNull(loaded.inputSettings);
        Assert.IsFalse(loaded.legacyProgressMigrated, "Perfis antigos devem ser migrados");
    }

    [Test]
    public void Load_MissingProfile_ReturnsNull()
    {
        Assert.IsNull(SaveManager.LoadProfile("NaoExiste"));
    }

    [Test]
    public void Delete_RemovesBackupToo()
    {
        SaveManager.SaveProfile(PlayerData.CreateNew("C"));
        SaveManager.SaveProfile(PlayerData.CreateNew("C"));
        Assert.IsTrue(SaveManager.DeleteProfile("C"));

        Assert.IsFalse(File.Exists(ProfilePath("C") + ".bak"));
        Assert.IsNull(SaveManager.LoadProfile("C"), "Perfil apagado nao pode voltar a partir do .bak");
    }

    [Test]
    public void Rename_MovesDataAndRemovesOld()
    {
        PlayerData p = PlayerData.CreateNew("Velho");
        p.sessionHistory.Add(new SessionRecord { levelName = "Level2" });
        SaveManager.SaveProfile(p);

        Assert.IsTrue(SaveManager.RenameProfile("Velho", "Novo"));
        Assert.IsNull(SaveManager.LoadProfile("Velho"));
        PlayerData loaded = SaveManager.LoadProfile("Novo");
        Assert.AreEqual("Novo", loaded.profileID);
        Assert.AreEqual(1, loaded.sessionHistory.Count);
    }

    [Test]
    public void Rename_ToExistingProfile_IsRefused()
    {
        SaveManager.SaveProfile(PlayerData.CreateNew("X"));
        SaveManager.SaveProfile(PlayerData.CreateNew("Y"));

        Assert.IsFalse(SaveManager.RenameProfile("X", "Y"));
        Assert.IsNotNull(SaveManager.LoadProfile("X"));
    }

    [Test]
    public void Rename_CaseOnly_IsRefusedAndKeepsData()
    {
        SaveManager.SaveProfile(PlayerData.CreateNew("ana"));

        Assert.IsFalse(SaveManager.RenameProfile("ana", "Ana"));
        Assert.IsNotNull(SaveManager.LoadProfile("ana"), "Nao pode apagar o perfil (Windows nao distingue maiusculas)");
    }

    [Test]
    public void Rename_SanitizesNewName()
    {
        SaveManager.SaveProfile(PlayerData.CreateNew("P"));
        Assert.IsTrue(SaveManager.RenameProfile("P", "Ana/Silva"));
        Assert.IsNotNull(SaveManager.LoadProfile("Ana_Silva"));
    }

    [Test]
    public void GetAllProfileIDs_IgnoresBackupAndTempFiles()
    {
        SaveManager.SaveProfile(PlayerData.CreateNew("D"));
        SaveManager.SaveProfile(PlayerData.CreateNew("D"));
        File.WriteAllText(ProfilePath("E") + ".tmp", "{}");

        CollectionAssert.AreEquivalent(new[] { "D" }, SaveManager.GetAllProfileIDs());
    }
}
