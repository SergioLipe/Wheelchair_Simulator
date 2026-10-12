using NUnit.Framework;

public class LevelScoringTests
{
    // Limites: 3 estrelas <= 60 s, 0 colisoes, 2 deslizes; 2 estrelas <= 90 s, 2 colisoes, 5 deslizes
    private static int Stars(float t, int c, int s) => LevelScoring.CalculateStars(t, c, s, 60f, 0, 2, 90f, 2, 5);

    [Test] public void Perfect_ThreeStars() => Assert.AreEqual(3, Stars(45f, 0, 0));
    [Test] public void ExactlyOnThreeStarLimits_ThreeStars() => Assert.AreEqual(3, Stars(60f, 0, 2));
    [Test] public void OneCollision_TwoStars() => Assert.AreEqual(2, Stars(45f, 1, 0));
    [Test] public void Slow_TwoStars() => Assert.AreEqual(2, Stars(80f, 0, 0));
    [Test] public void TooSlow_OneStar() => Assert.AreEqual(1, Stars(120f, 0, 0));
    [Test] public void TooManyCollisions_OneStar() => Assert.AreEqual(1, Stars(30f, 3, 0));
    [Test] public void TooManySlides_OneStar() => Assert.AreEqual(1, Stars(30f, 0, 6));
}
