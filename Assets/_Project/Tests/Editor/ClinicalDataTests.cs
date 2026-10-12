using System.IO;
using System.Text;
using NUnit.Framework;

/// <summary>
/// Testes da exportacao CSV e do texto do historico de tentativas.
/// </summary>
public class ClinicalDataTests
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

    private static PlayerData SampleProfile()
    {
        PlayerData p = PlayerData.CreateNew("Ana");
        p.sessionHistory.Add(new SessionRecord
        {
            sessionDate = "2026-10-01 10:00", levelName = "Level3", completionTime = 75.4f,
            totalCollisions = 2, totalSlides = 1, starsEarned = 2,
            platform = "VR", steeringType = "RearSteering", inputMode = "Comando"
        });
        p.sessionHistory.Add(new SessionRecord
        {
            sessionDate = "2026-10-02 11:30", levelName = "Level3", completionTime = 20f,
            failed = true, failReason = "sinal_vermelho", platform = "PC", steeringType = "FrontSteering", inputMode = "Teclado"
        });
        p.sessionHistory.Add(new SessionRecord
        {
            sessionDate = "2026-10-03T09:15:00.0000000+01:00", levelName = "Freestyle", starsCollected = 4, starsTotal = 10
        });
        return p;
    }

    private static string[][] ParseRows(string csv)
    {
        string[] lines = csv.Split(new[] { "\r\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        string[][] rows = new string[lines.Length][];
        for (int i = 0; i < lines.Length; i++) rows[i] = lines[i].Split(';');
        return rows;
    }

    [Test]
    public void Csv_HasHeaderAndOneRowPerSession()
    {
        string[][] rows = ParseRows(ClinicalExport.BuildCsv(new[] { SampleProfile() }));
        Assert.AreEqual(4, rows.Length);
        Assert.AreEqual("Paciente", rows[0][0]);
        foreach (string[] r in rows) Assert.AreEqual(rows[0].Length, r.Length, "Todas as linhas devem ter o mesmo numero de colunas");
    }

    [Test]
    public void Csv_CompletedRow_HasTimeStarsAndDetails()
    {
        string[] r = ParseRows(ClinicalExport.BuildCsv(new[] { SampleProfile() }))[1];
        CollectionAssert.AreEqual(
            new[] { "Ana", "2026-10-01 10:00", "Level3", "Concluido", "", "75", "01:15", "2", "1", "2", "", "", "VR", "Traseira", "Comando" },
            r);
    }

    [Test]
    public void Csv_FailedRow_HasReasonAndNoStars()
    {
        string[] r = ParseRows(ClinicalExport.BuildCsv(new[] { SampleProfile() }))[2];
        Assert.AreEqual("Falhou", r[3]);
        Assert.AreEqual("sinal_vermelho", r[4]);
        Assert.AreEqual("", r[9]);
    }

    [Test]
    public void Csv_FreestyleRow_HasCollectedStarsAndIsoDateNormalized()
    {
        string[] r = ParseRows(ClinicalExport.BuildCsv(new[] { SampleProfile() }))[3];
        Assert.AreEqual("Modo livre", r[3]);
        Assert.AreEqual("4", r[10]);
        Assert.AreEqual("10", r[11]);
        StringAssert.StartsWith("2026-10-03", r[1]);
        Assert.AreEqual("", r[5], "Modo livre nao tem tempo");
    }

    [Test]
    public void Csv_EscapesSeparatorsAndQuotes()
    {
        Assert.AreEqual("\"a;b\"", ClinicalExport.Escape("a;b"));
        Assert.AreEqual("\"diz \"\"ola\"\"\"", ClinicalExport.Escape("diz \"ola\""));
        Assert.AreEqual("simples", ClinicalExport.Escape("simples"));
    }

    [Test]
    public void Csv_OldRecordsWithoutNewFields_StillExport()
    {
        PlayerData p = PlayerData.CreateNew("Antigo");
        p.sessionHistory.Add(new SessionRecord { sessionDate = "2025-01-01 09:00", levelName = "Level1", completionTime = 30f });
        string[] r = ParseRows(ClinicalExport.BuildCsv(new[] { p }))[1];
        Assert.AreEqual("Concluido", r[3]);
        Assert.AreEqual("", r[12]);
    }

    [Test]
    public void ExportAllProfiles_WritesUtf8BomFileWithAllPatients()
    {
        SaveManager.SaveProfile(SampleProfile());
        PlayerData b = PlayerData.CreateNew("João");
        b.sessionHistory.Add(new SessionRecord { sessionDate = "2026-10-05 15:00", levelName = "Level1", completionTime = 40f });
        SaveManager.SaveProfile(b);

        string path = ClinicalExport.ExportAllProfiles();
        Assert.IsNotNull(path);
        Assert.IsTrue(File.Exists(path));

        byte[] bytes = File.ReadAllBytes(path);
        Assert.AreEqual(0xEF, bytes[0], "Precisa de BOM UTF-8 para o Excel mostrar os acentos");
        string text = Encoding.UTF8.GetString(bytes);
        StringAssert.Contains("João", text);
        Assert.AreEqual(5, ParseRows(text.TrimStart('﻿')).Length);
    }

    [Test]
    public void History_MostRecentAttemptHasHighestNumber()
    {
        string text = SessionHistoryFormatter.BuildAttemptsText(SampleProfile(), "Level3");
        int idx2 = text.IndexOf("TENTATIVA 2");
        int idx1 = text.IndexOf("TENTATIVA 1");
        Assert.GreaterOrEqual(idx2, 0);
        Assert.Less(idx2, idx1, "A tentativa mais recente (2) deve aparecer primeiro");
    }

    [Test]
    public void History_ShowsFailureReasonAndDetails()
    {
        string text = SessionHistoryFormatter.BuildAttemptsText(SampleProfile(), "Level3", (k, f) => k == "sinal_vermelho" ? "Sinal vermelho!" : f);
        StringAssert.Contains("Falhou: Sinal vermelho!", text);
        StringAssert.Contains("VR · Traseira · Comando", text);
        StringAssert.Contains("★★☆", text);
    }

    [Test]
    public void History_FreestyleAliasesAreGroupedTogether()
    {
        PlayerData p = SampleProfile();
        p.sessionHistory.Add(new SessionRecord { sessionDate = "2026-10-04 10:00", levelName = "Level11", starsCollected = 10, starsTotal = 10 });
        Assert.AreEqual(2, SessionHistoryFormatter.CountAttempts(p, "Freestyle"));
        StringAssert.Contains("Completo!", SessionHistoryFormatter.BuildAttemptsText(p, "Freestyle"));
    }
}
