using NUnit.Framework;

/// <summary>
/// Escolha do nivel grafico inicial pela placa grafica.
/// </summary>
public class PCGraphicsQualityTests
{
    [TestCase("Intel(R) UHD Graphics 620", 1024)]
    [TestCase("Intel(R) Iris(R) Xe Graphics", 8192)]
    [TestCase("Intel(R) HD Graphics 4000", 2048)]
    [TestCase("AMD Radeon(TM) Graphics", 4096)]
    [TestCase("AMD Radeon Vega 8 Graphics", 2048)]
    [TestCase("Microsoft Basic Render Driver", 0)]
    public void IntegratedGpu_StartsOnLow(string gpu, int memory)
    {
        Assert.AreEqual(PCGraphicsQuality.Tier.Baixa, PCGraphicsQuality.DetectTier(gpu, memory));
    }

    [Test]
    public void SmallDedicatedGpu_StartsOnLowOrMedium()
    {
        Assert.AreEqual(PCGraphicsQuality.Tier.Baixa, PCGraphicsQuality.DetectTier("NVIDIA GeForce GT 1030", 2048));
        Assert.AreEqual(PCGraphicsQuality.Tier.Media, PCGraphicsQuality.DetectTier("NVIDIA GeForce GTX 1650", 4096));
    }

    [TestCase("NVIDIA GeForce RTX 5060 Ti", 16311)]
    [TestCase("AMD Radeon RX 6600", 8176)]
    [TestCase("Intel(R) Arc(TM) A750 Graphics", 8096)]
    public void DedicatedGpu_StartsOnHigh(string gpu, int memory)
    {
        Assert.AreEqual(PCGraphicsQuality.Tier.Alta, PCGraphicsQuality.DetectTier(gpu, memory));
    }
}
