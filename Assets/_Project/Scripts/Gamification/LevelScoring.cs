/// <summary>
/// Calculo das estrelas de um nivel (1 a 3), partilhado pelo LevelManager_PC e pelo LevelManagerVR.
/// 3 estrelas: dentro do tempo E das colisoes E dos deslizes para 3 estrelas; 2 estrelas: idem para 2; senao 1.
/// </summary>
public static class LevelScoring
{
    public static int CalculateStars(
        float time, int collisions, int slides,
        float timeFor3Stars, int maxCollisionsFor3Stars, int maxSlidesFor3Stars,
        float timeFor2Stars, int maxCollisionsFor2Stars, int maxSlidesFor2Stars)
    {
        if (time <= timeFor3Stars && collisions <= maxCollisionsFor3Stars && slides <= maxSlidesFor3Stars) return 3;
        if (time <= timeFor2Stars && collisions <= maxCollisionsFor2Stars && slides <= maxSlidesFor2Stars) return 2;
        return 1;
    }
}
