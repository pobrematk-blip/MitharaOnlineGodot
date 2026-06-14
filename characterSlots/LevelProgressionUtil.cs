public static class LevelProgressionUtil
{
    public const int NivelInicial = 1;
    public const int PontosPorLevel = 5;
    public const int XpBase = 20;
    public const int XpIncrementoPorLevel = 12;

    /// <summary>XP necessária para sair do nível atual e ir ao próximo.</summary>
    public static int XpParaProximoLevel(int nivelAtual)
    {
        if (nivelAtual < 1) nivelAtual = 1;
        return XpBase + (nivelAtual - 1) * XpIncrementoPorLevel;
    }
}
