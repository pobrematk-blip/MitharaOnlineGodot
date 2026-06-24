using Godot;

public enum SkillEffectType
{
    None,
    Heal,
    Dash,
    Buff,
    Invisibility,
    Summon,
    Revive,
    Damage,
    Stun,
    Bleed,
    Shield,
    Silence,
    Slow,
    Root,
    Taunt,
    Burn,
    Freeze,
    Fear,
    Confusion,
    Sleep,
    Prison,
    Invincibility,
    Blindness,
    Reflect,
    Poison,
    Curse,
}

public enum SkillTargetType
{
    Self,
    Ally,
    Enemy,
}

[Tool]
public partial class SkillResource : Resource
{
    // Identificador do skill (usado pelo resto do projeto)
    [Export]
    public int SkillId { get; set; } = 0;

    // Nomes/descrições (pt-BR, usados pelo código existente)
    [Export]
    public string Nome { get; set; } = "Novo Skill";

    [Export]
    public string Descricao { get; set; } = "Descrição do skill.";

    [Export]
    public Texture2D Icone { get; set; }

    [Export]
    public int Valor { get; set; } = 0;

    [Export]
    public int CustoMana { get; set; } = 0;

    [Export]
    public string ClasseRestrita { get; set; } = string.Empty;

    [Export]
    public string CenaInvocacao { get; set; } = string.Empty;

    [Export]
    public string EfeitoVisualPath { get; set; } = string.Empty;

    [Export]
    public string BuffType { get; set; } = string.Empty;

    [Export]
    public float Cooldown { get; set; } = 0f;

    [Export]
    public float Duracao { get; set; } = 0f;

    [Export]
    public SkillTargetType TargetType { get; set; } = SkillTargetType.Self;

    [Export]
    public bool Passive { get; set; } = false;

    [Export]
    public SkillEffectType EffectType { get; set; } = SkillEffectType.None;

    // Aliases em inglês (compatibilidade com outros arquivos criados posteriormente)
    // Dados de catalogo usados pelo editor/importador de planilhas.
    [Export] public string Especializacao { get; set; } = string.Empty;
    [Export] public int NivelRequerido { get; set; } = 1;
    [Export] public int Ordem { get; set; } = 0;
    [Export] public string Escopo { get; set; } = string.Empty;
    [Export] public string Tipo { get; set; } = string.Empty;
    [Export] public string EfeitoPrincipal { get; set; } = string.Empty;
    [Export] public string DanoEscala { get; set; } = string.Empty;
    [Export] public string BuffDebuff { get; set; } = string.Empty;
    [Export] public string DuracaoTexto { get; set; } = string.Empty;
    [Export] public string Progressao { get; set; } = string.Empty;
    [Export] public string Observacoes { get; set; } = string.Empty;

    public string SkillName { get => Nome; set => Nome = value; }
    public string Description { get => Descricao; set => Descricao = value; }
    public string IconPath { get => Icone != null ? Icone.ResourcePath : string.Empty; set { if (!string.IsNullOrEmpty(value)) Icone = GD.Load<Texture2D>(value); } }
    public int Power { get => Valor; set => Valor = value; }
    public int ManaCost { get => CustoMana; set => CustoMana = value; }
    public string SummonScenePath { get => CenaInvocacao; set => CenaInvocacao = value; }
    public bool IsPassive { get => Passive; set => Passive = value; }
    public float Duration { get => Duracao; set => Duracao = value; }
    public SkillTargetType GetTargetType() => TargetType;
}
