using Godot;

public enum TalentNodeType
{
    Attribute,
    Skill,
    Passive,
    Status,
    Hybrid,
}

[GlobalClass]
public partial class TalentNodeResource : Resource
{
    [ExportGroup("Identidade")]
    [Export] public string NodeId { get; set; } = "";
    [Export] public string Nome { get; set; } = "Novo Talento";
    [Export(PropertyHint.MultilineText)] public string Descricao { get; set; } = "";

    [ExportGroup("Tipo")]
    [Export] public TalentNodeType NodeType { get; set; } = TalentNodeType.Attribute;

    [ExportGroup("Layout")]
    [Export] public Vector2 Posicao { get; set; } = new Vector2(-1, -1);

    [ExportGroup("Visual")]
    [Export] public Texture2D Icone { get; set; }
    [Export] public SkillResource HabilidadeAtiva { get; set; }

    [ExportGroup("Requisitos")]
    [Export] public string[] Requisitos { get; set; } = System.Array.Empty<string>();
    [Export] public int CustoPontos { get; set; } = 1;
    [Export] public int NivelMinimo { get; set; } = 1;

    [ExportGroup("Bônus de Atributos")]
    [Export(PropertyHint.Range, "-100,100,1")] public int BonusForca { get; set; }
    [Export(PropertyHint.Range, "-100,100,1")] public int BonusAgilidade { get; set; }
    [Export(PropertyHint.Range, "-100,100,1")] public int BonusDestreza { get; set; }
    [Export(PropertyHint.Range, "-100,100,1")] public int BonusInteligencia { get; set; }

    [ExportGroup("Bônus Percentuais")]
    [Export(PropertyHint.Range, "-100,100,0.1")] public float BonusDanoPercent { get; set; }
    [Export(PropertyHint.Range, "-100,100,0.1")] public float BonusVelocidadePercent { get; set; }
    [Export(PropertyHint.Range, "-100,100,0.1")] public float BonusVidaPercent { get; set; }
    [Export(PropertyHint.Range, "-100,100,0.1")] public float BonusManaPercent { get; set; }

    public bool TemRequisitos => Requisitos != null && Requisitos.Length > 0;

    public static string ObterRotuloTipo(TalentNodeType tipo)
    {
        return tipo switch
        {
            TalentNodeType.Attribute => "Atributo",
            TalentNodeType.Skill => "Habilidade",
            TalentNodeType.Passive => "Passiva",
            TalentNodeType.Status => "Status",
            TalentNodeType.Hybrid => "Híbrido",
            _ => "Desconhecido",
        };
    }

    public static Color ObterCorTipo(TalentNodeType tipo)
    {
        return tipo switch
        {
            TalentNodeType.Attribute => new Color(0.4f, 0.8f, 1.0f),
            TalentNodeType.Skill => new Color(1.0f, 0.6f, 0.2f),
            TalentNodeType.Passive => new Color(0.6f, 1.0f, 0.4f),
            TalentNodeType.Status => new Color(1.0f, 0.4f, 0.8f),
            TalentNodeType.Hybrid => new Color(1.0f, 1.0f, 0.4f),
            _ => new Color(1.0f, 1.0f, 1.0f),
        };
    }
}
