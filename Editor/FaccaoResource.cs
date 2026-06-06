using Godot;

[GlobalClass]
public partial class FaccaoResource : Resource
{
    [Export] public string IdFaccao { get; set; } = "solari";
    [Export] public string NomeFaccao { get; set; } = "Solari";
    [Export(PropertyHint.MultilineText)] public string Descricao { get; set; } = "";
    [Export] public Color CorTema { get; set; } = Colors.Gold;
    [Export] public Texture2D Emblema { get; set; }

    public Texture2D ObterEmblema()
    {
        if (Emblema != null) return Emblema;

        string caminho = $"res://Faccoes/{NomeFaccao}.png";
        if (ResourceLoader.Exists(caminho))
            return ResourceLoader.Load<Texture2D>(caminho);

        return null;
    }

    public bool MesmaFaccao(FaccaoResource outra)
    {
        if (outra == null || string.IsNullOrEmpty(IdFaccao)) return false;
        return IdFaccao.Trim().ToLowerInvariant() == outra.IdFaccao.Trim().ToLowerInvariant();
    }

    public bool EhRivalDe(FaccaoResource outra) => outra != null && !MesmaFaccao(outra);
}
