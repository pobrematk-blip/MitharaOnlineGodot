using Godot;
using System.Collections.Generic;

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
    [Export] public int SkillId { get; set; } = 0;
    [Export] public string Nome { get; set; } = "Nova Skill";
    [Export] public string Descricao { get; set; } = "Descricao da skill.";
    [Export] public Texture2D Icone { get; set; }
    [Export] public int Valor { get; set; } = 0;
    [Export] public int CustoMana { get; set; } = 0;
    [Export] public string ClasseRestrita { get; set; } = string.Empty;
    [Export] public string CenaInvocacao { get; set; } = string.Empty;
    [Export] public string EfeitoVisualPath { get; set; } = string.Empty;
    [Export] public string CenaEfeitoNoAlvo { get; set; } = string.Empty;
    [Export] public string CenaEfeitoNoPlayer { get; set; } = string.Empty;
    [Export] public string CenaProjetil { get; set; } = string.Empty;
    [Export] public string CenaLancamento { get; set; } = string.Empty;
    [Export] public string BuffType { get; set; } = string.Empty;
    [Export] public float Cooldown { get; set; } = 0f;
    [Export] public float Duracao { get; set; } = 0f;
    [Export] public SkillTargetType TargetType { get; set; } = SkillTargetType.Self;
    [Export] public bool Passive { get; set; } = false;
    [Export] public SkillEffectType EffectType { get; set; } = SkillEffectType.None;

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
    public string TargetEffectScenePath { get => string.IsNullOrWhiteSpace(CenaEfeitoNoAlvo) ? EfeitoVisualPath : CenaEfeitoNoAlvo; set { CenaEfeitoNoAlvo = value; EfeitoVisualPath = value; } }
    public string PlayerEffectScenePath { get => CenaEfeitoNoPlayer; set => CenaEfeitoNoPlayer = value; }
    public string ProjectileScenePath { get => CenaProjetil; set => CenaProjetil = value; }
    public string CastScenePath { get => CenaLancamento; set => CenaLancamento = value; }
    public bool IsPassive { get => Passive; set => Passive = value; }
    public float Duration { get => Duracao; set => Duracao = value; }
    public SkillTargetType GetTargetType() => TargetType;

    public string ObterDescricaoCompleta()
    {
        var linhas = new List<string>();
        if (!string.IsNullOrWhiteSpace(Nome))
            linhas.Add(Nome);

        AdicionarSecao(linhas, "Descricao", ObterDescricaoDetalhada());
        AdicionarSecao(linhas, "Efeito", ObterEfeitoDetalhado());
        AdicionarSecao(linhas, ObterRotuloImpacto(), ObterImpactoDetalhado());
        AdicionarSecao(linhas, "Custo e recarga", ObterCustoRecargaDetalhado());
        AdicionarSecao(linhas, "Requisitos", ObterRequisitosDetalhados());
        AdicionarSecao(linhas, "Progressao", Progressao);
        AdicionarSecao(linhas, "Observacoes", Observacoes);

        return string.Join("\n", linhas);
    }

    private string ObterDescricaoDetalhada()
    {
        string descricao = LimparTexto(Descricao);
        string contexto = GerarDescricaoContextual();

        if (string.IsNullOrWhiteSpace(descricao))
            return contexto;

        if (descricao.Length < 45 && !string.IsNullOrWhiteSpace(contexto))
            return $"{descricao}\n{contexto}";

        return descricao;
    }

    private string ObterEfeitoDetalhado()
    {
        var partes = new List<string>();
        AdicionarLinha(partes, "Tipo", string.IsNullOrWhiteSpace(Tipo) ? ObterTipoPorEfeito() : Tipo);
        AdicionarLinha(partes, "Alvo", ObterRotuloAlvo());
        AdicionarLinha(partes, "Efeito principal", EfeitoPrincipal);
        AdicionarLinha(partes, "Buff/Debuff", BuffDebuff);
        AdicionarLinha(partes, "Duracao", ObterDuracaoTexto());
        if (Passive)
            partes.Add("Passiva: fica ativa apos ser liberada.");
        return string.Join("\n", partes);
    }

    private string ObterImpactoDetalhado()
    {
        var partes = new List<string>();
        string impacto = LimparTexto(DanoEscala);
        if (!string.IsNullOrWhiteSpace(impacto))
            partes.Add(impacto);
        else if (Valor != 0)
            partes.Add(ObterValorComoTexto());
        else if (!string.IsNullOrWhiteSpace(BuffDebuff))
            partes.Add(BuffDebuff);
        else
            partes.Add(ObterImpactoPadrao());

        if (Valor != 0 && !ContemNumero(impacto))
            partes.Add($"Valor base configurado: {Valor}.");

        return string.Join("\n", partes);
    }

    private string ObterCustoRecargaDetalhado()
    {
        var partes = new List<string>
        {
            CustoMana > 0 ? $"Mana: {CustoMana}" : "Mana: sem custo",
            Cooldown > 0 ? $"Cooldown: {Cooldown:0.#}s" : "Cooldown: sem recarga",
        };

        string duracao = ObterDuracaoTexto();
        if (!string.IsNullOrWhiteSpace(duracao))
            partes.Add($"Duracao: {duracao}");

        return string.Join("\n", partes);
    }

    private string ObterRequisitosDetalhados()
    {
        var partes = new List<string>();
        AdicionarLinha(partes, "Classe", ClasseRestrita);
        if (NivelRequerido > 1)
            partes.Add($"Nivel requerido: {NivelRequerido}");
        AdicionarLinha(partes, "Especializacao", Especializacao);
        AdicionarLinha(partes, "Escopo", Escopo);
        return string.Join("\n", partes);
    }

    private string GerarDescricaoContextual()
    {
        string alvo = ObterRotuloAlvo().ToLowerInvariant();
        return EffectType switch
        {
            SkillEffectType.Heal => $"Restaura recursos do {alvo}, ajudando a recuperar folego durante o combate.",
            SkillEffectType.Dash => "Reposiciona o personagem rapidamente, criando distancia ou aproximando do alvo conforme a direcao escolhida.",
            SkillEffectType.Buff => "Aplica um beneficio temporario que melhora atributos ou desempenho em combate.",
            SkillEffectType.Invisibility => "Oculta o personagem temporariamente e prepara uma abertura para reposicionamento ou ataque surpresa.",
            SkillEffectType.Summon => "Invoca uma unidade aliada temporaria para auxiliar em combate.",
            SkillEffectType.Revive => "Traz um aliado derrotado de volta para a luta com parte dos recursos restaurados.",
            SkillEffectType.Damage => $"Causa dano ao {alvo} usando a escala e os modificadores descritos abaixo.",
            SkillEffectType.Stun => $"Causa controle no {alvo}, impedindo acoes por um curto periodo.",
            SkillEffectType.Bleed => $"Aplica sangramento no {alvo}, causando dano ao longo do tempo.",
            SkillEffectType.Shield => $"Cria uma protecao que absorve parte do dano recebido pelo {alvo}.",
            SkillEffectType.Silence => $"Impede temporariamente que o {alvo} conjure habilidades.",
            SkillEffectType.Slow => $"Reduz a velocidade do {alvo}, facilitando perseguir ou escapar.",
            SkillEffectType.Root => $"Imobiliza o {alvo}, impedindo movimentacao por curta duracao.",
            SkillEffectType.Taunt => "Forca inimigos afetados a focarem no usuario e aumenta a ameaca gerada.",
            SkillEffectType.Burn => $"Incendeia o {alvo}, causando dano continuo.",
            SkillEffectType.Freeze => $"Congela ou prende o {alvo}, limitando movimento e resposta.",
            SkillEffectType.Fear => $"Aplica medo no {alvo}, quebrando o controle normal da movimentacao.",
            SkillEffectType.Confusion => $"Confunde o {alvo}, atrapalhando sua resposta em combate.",
            SkillEffectType.Sleep => $"Coloca o {alvo} para dormir ate o efeito acabar ou ser quebrado.",
            SkillEffectType.Prison => $"Aprisiona o {alvo}, limitando deslocamento e abertura defensiva.",
            SkillEffectType.Invincibility => "Concede protecao extrema por curta duracao.",
            SkillEffectType.Blindness => $"Reduz a precisao do {alvo}, diminuindo sua confiabilidade ofensiva.",
            SkillEffectType.Reflect => "Reflete parte do dano recebido contra quem atacar.",
            SkillEffectType.Poison => $"Envenena o {alvo}, causando dano periodico e pressao prolongada.",
            SkillEffectType.Curse => $"Aplica uma maldicao no {alvo}, enfraquecendo atributos ou aumentando vulnerabilidade.",
            _ => "Executa o efeito descrito abaixo conforme as regras da habilidade."
        };
    }

    private string ObterRotuloImpacto()
    {
        if (EffectType == SkillEffectType.Heal)
            return "Cura";
        if (EffectType == SkillEffectType.Shield || EffectType == SkillEffectType.Invincibility || EffectType == SkillEffectType.Reflect)
            return "Defesa";
        if (!string.IsNullOrWhiteSpace(BuffDebuff) && string.IsNullOrWhiteSpace(DanoEscala))
            return "Buff/Debuff";
        return "Dano ou valor";
    }

    private string ObterValorComoTexto()
    {
        return EffectType switch
        {
            SkillEffectType.Heal => $"Cura base: {Valor}.",
            SkillEffectType.Shield => $"Absorcao base: {Valor}.",
            SkillEffectType.Buff => $"Bonus base: {Valor}.",
            SkillEffectType.Damage or SkillEffectType.Bleed or SkillEffectType.Burn or SkillEffectType.Poison => $"Dano base: {Valor}.",
            _ => $"Valor base: {Valor}."
        };
    }

    private string ObterImpactoPadrao()
    {
        return EffectType switch
        {
            SkillEffectType.Dash => "Nao causa dano direto; o valor principal e mobilidade.",
            SkillEffectType.Summon => "O impacto depende da invocacao chamada pela habilidade.",
            SkillEffectType.Revive => "Revive o alvo conforme a regra da habilidade.",
            SkillEffectType.Buff => "Aplica bonus conforme o efeito principal.",
            SkillEffectType.Shield => "Absorve dano conforme a escala defensiva configurada.",
            SkillEffectType.None => "Sem dano ou buff numerico configurado.",
            _ => "Usa o efeito principal configurado para definir o resultado."
        };
    }

    private string ObterTipoPorEfeito()
    {
        return EffectType switch
        {
            SkillEffectType.Heal => "Cura",
            SkillEffectType.Dash => "Mobilidade",
            SkillEffectType.Buff => "Buff",
            SkillEffectType.Invisibility => "Furtividade",
            SkillEffectType.Summon => "Invocacao",
            SkillEffectType.Revive => "Suporte",
            SkillEffectType.Damage => "Dano",
            SkillEffectType.Shield => "Defesa",
            SkillEffectType.Taunt => "Controle/Tanque",
            SkillEffectType.Bleed or SkillEffectType.Burn or SkillEffectType.Poison => "Dano ao longo do tempo",
            SkillEffectType.Stun or SkillEffectType.Silence or SkillEffectType.Slow or SkillEffectType.Root or SkillEffectType.Freeze or SkillEffectType.Fear or SkillEffectType.Confusion or SkillEffectType.Sleep or SkillEffectType.Prison or SkillEffectType.Blindness or SkillEffectType.Curse => "Controle",
            _ => "Habilidade"
        };
    }

    private string ObterRotuloAlvo()
    {
        return TargetType switch
        {
            SkillTargetType.Self => "proprio personagem",
            SkillTargetType.Ally => "aliado",
            SkillTargetType.Enemy => "inimigo",
            _ => "alvo"
        };
    }

    private string ObterDuracaoTexto()
    {
        if (Duracao > 0)
            return $"{Duracao:0.#}s";
        string texto = LimparTexto(DuracaoTexto);
        return string.IsNullOrWhiteSpace(texto) ? "instantaneo" : texto;
    }

    private static void AdicionarSecao(List<string> linhas, string rotulo, string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return;

        if (linhas.Count > 0)
            linhas.Add("");
        linhas.Add($"{rotulo}:");
        linhas.Add(valor);
    }

    private static void AdicionarLinha(List<string> linhas, string rotulo, string valor)
    {
        valor = LimparTexto(valor);
        if (!string.IsNullOrWhiteSpace(valor))
            linhas.Add($"{rotulo}: {valor}");
    }

    private static string LimparTexto(string valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.Trim();
    }

    private static bool ContemNumero(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return false;
        foreach (char c in valor)
        {
            if (char.IsDigit(c))
                return true;
        }
        return false;
    }
}
