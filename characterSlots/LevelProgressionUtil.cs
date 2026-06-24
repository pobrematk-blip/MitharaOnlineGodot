public static class LevelProgressionUtil
{
    public const int NivelInicial = 1;
    public const int PontosPorLevel = 5;

    /// <summary>XP necessária para sair do nível atual e ir ao próximo (fórmula quadrática).</summary>
    public static int XpParaProximoLevel(int nivelAtual)
    {
        if (nivelAtual < 1) nivelAtual = 1;
        return 80 + nivelAtual * 15 + nivelAtual * nivelAtual * 2;
    }
}
