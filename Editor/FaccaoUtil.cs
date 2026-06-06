public static class FaccaoUtil
{
    public const string IdSolari = "solari";
    public const string IdNoctori = "noctori";

    public static bool SaoAliados(FaccaoResource a, FaccaoResource b) =>
        a != null && b != null && a.MesmaFaccao(b);

    public static bool SaoRivais(FaccaoResource a, FaccaoResource b) =>
        a != null && b != null && a.EhRivalDe(b);
}
