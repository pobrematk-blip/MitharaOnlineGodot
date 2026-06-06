using Godot;

[GlobalClass]
public partial class RacaResource : Resource
{
    [Export] public string NomeRaca { get; set; } = "Humano";
    [Export(PropertyHint.MultilineText)] public string Descricao { get; set; } = "";

    [ExportGroup("Facção")]
    [Export] public FaccaoResource Faccao { get; set; }

    public string ObterNomeFaccao() => Faccao?.NomeFaccao ?? "(Sem facção)";

    [ExportGroup("Bônus de Atributos")]
    [Export] public int BonusForca { get; set; }
    [Export] public int BonusAgilidade { get; set; }
    [Export] public int BonusDestreza { get; set; }
    [Export] public int BonusInteligencia { get; set; }

    [ExportGroup("Bônus de Recursos")]
    [Export] public int BonusVidaMaxima { get; set; }
    [Export] public int BonusManaMaxima { get; set; }

    [ExportGroup("Bônus de Combate")]
    [Export] public float BonusDanoCritico { get; set; }
    [Export] public float BonusVelocidadeMovimento { get; set; }

    [ExportGroup("Visual LPC")]
    [Export] public Texture2D Spritesheet { get; set; }

    public string ObterNomeArquivoSprite()
    {
        return NomeRaca?.Trim() switch
        {
            "Dark Elfo" => "DarkElfo",
            "Morto Vivo" => "MortoVivo",
            _ => (NomeRaca ?? "Humano").Replace(" ", "")
        };
    }

    public Texture2D ObterSpritesheet()
    {
        if (Spritesheet != null) return Spritesheet;

        string caminho = LpcSpriteFramesBuilder.PastaSpritesRaca + ObterNomeArquivoSprite() + ".png";
        if (ResourceLoader.Exists(caminho))
            return ResourceLoader.Load<Texture2D>(caminho);

        return null;
    }

    public SpriteFrames CriarSpriteFrames(string prefixoAtaque)
    {
        var tex = ObterSpritesheet();
        return tex == null ? null : LpcSpriteFramesBuilder.Construir(tex, prefixoAtaque);
    }
}
