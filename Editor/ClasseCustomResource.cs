using Godot;

[GlobalClass]
public partial class ClasseCustomResource : Resource
{
    [ExportGroup("Identidade")]
    [Export] public string NomeClasse { get; set; } = "Nova Classe";
    [Export(PropertyHint.MultilineText)] public string Descricao { get; set; } = "";
    [Export] public Texture2D IconeClasse { get; set; }

    [ExportGroup("Aparência")]
    [Export] public SpritePresetResource SpritePreset { get; set; }
    [Export] public string PrefixoAnimacao { get; set; } = "mago";
    [Export] public SpriteFrames SpriteFramesOverride { get; set; }

    [ExportGroup("Raça")]
    [Export] public RacaResource Raca { get; set; }

    [ExportGroup("Atributos Base (mesmos da Character UI)")]
    [Export] public int PontosDisponiveis { get; set; } = 10;
    [Export] public int Forca { get; set; } = 10;
    [Export] public int Agilidade { get; set; } = 10;
    [Export] public int Destreza { get; set; } = 10;
    [Export] public int Inteligencia { get; set; } = 10;

    [ExportGroup("Vida e Mana")]
    [Export] public int VidaMaxima { get; set; } = 100;
    [Export] public int ManaMaxima { get; set; } = 50;

    [ExportGroup("Bônus extras (opcional)")]
    [Export] public float BonusDanoCritico { get; set; }
    [Export] public float VelocidadeMovimento { get; set; } = 185f;

    [ExportGroup("Animações")]
    [Export] public string PrefixoAnimacaoAtaque { get; set; } = "";
    [Export] public float AttackAnimSpeedScale { get; set; } = 3f;
    [Export] public string AnimacaoIdle { get; set; } = "idle";
    [Export] public string AnimacaoAndar { get; set; } = "walk";
    [Export] public string AnimacaoAtaque { get; set; } = "attack";

    [ExportGroup("Combate")]
    [Export] public bool UsaProjetil { get; set; }
    [Export] public PackedScene CenaDoProjetil { get; set; }
    [Export] public int DanoProjetilMin { get; set; } = 10;
    [Export] public int DanoProjetilMax { get; set; } = 15;
    [Export] public bool ProjetilDanoMagico { get; set; } = true;
    [Export] public float VelocidadeDoProjetil { get; set; } = 250f;
    [Export] public float AlcanceAtaqueMelee { get; set; } = 48f;

    [ExportGroup("Talentos")]
    [Export] public TalentTreeResource ArvoreTalentos { get; set; }

    [ExportGroup("Itens Iniciais")]
    [Export] public ItemResource[] ItensIniciais { get; set; } = System.Array.Empty<ItemResource>();

    /// <summary>IDs dos itens separados por vírgula (para o servidor).</summary>
    [Export] public string ItensIniciaisIds { get; set; } = "";

    public string ObterPrefixoAnimacao()
    {
        if (!string.IsNullOrWhiteSpace(PrefixoAnimacao))
            return PrefixoAnimacao.Trim().ToLower();
        if (SpritePreset != null && !string.IsNullOrWhiteSpace(SpritePreset.PrefixoAnimacao))
            return SpritePreset.PrefixoAnimacao.Trim().ToLower();
        return NomeClasse.Trim().ToLower().Replace(" ", "_");
    }

    /// <summary>Prefixo das animações {prefixo}_attack_* (mago, arqueiro, guerreiro, ladino…).</summary>
    public string ObterPrefixoAtaque()
    {
        if (!string.IsNullOrWhiteSpace(PrefixoAnimacaoAtaque))
            return PrefixoAnimacaoAtaque.Trim().ToLower();
        return ObterPrefixoAnimacao();
    }

    public bool AtaqueUsaMesmoPrefixoDoSprite()
    {
        return string.IsNullOrWhiteSpace(PrefixoAnimacaoAtaque)
            || PrefixoAnimacaoAtaque.Trim().ToLower() == ObterPrefixoAnimacao();
    }

    public SpriteFrames ObterSpriteFrames()
    {
        if (SpriteFramesOverride != null) return SpriteFramesOverride;
        if (SpritePreset?.SpriteFramesRecurso != null) return SpritePreset.SpriteFramesRecurso;
        return null;
    }

    /// <summary>Sprite da raça (LPC) + animações de ataque da classe.</summary>
    public SpriteFrames ObterSpriteFramesCompletos()
    {
        if (Raca != null)
        {
            var lpc = Raca.CriarSpriteFrames(ObterPrefixoAtaque());
            if (lpc != null && lpc.GetAnimationNames().Length > 0)
                return lpc;
        }
        return ObterSpriteFrames();
    }
}
